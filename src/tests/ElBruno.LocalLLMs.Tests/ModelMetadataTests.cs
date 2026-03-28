using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="GenAIConfigParser"/> and <see cref="ModelMetadata"/>.
/// Uses temporary directories with genai_config.json files to test parsing.
/// </summary>
public class ModelMetadataTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string CreateTempModelDir(string configJson)
    {
        var dir = Path.Combine(Path.GetTempPath(), "localllms-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        File.WriteAllText(Path.Combine(dir, "genai_config.json"), configJson);
        return dir;
    }

    // ──────────────────────────────────────────────
    // ModelMetadata record
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelMetadata_IsSealed()
    {
        Assert.True(typeof(ModelMetadata).IsSealed);
    }

    [Fact]
    public void ModelMetadata_DefaultValues()
    {
        var meta = new ModelMetadata();

        Assert.Equal(0, meta.MaxSequenceLength);
        Assert.Equal(0, meta.ConfigMaxSequenceLength);
        Assert.Null(meta.ModelName);
        Assert.Null(meta.VocabSize);
    }

    [Fact]
    public void ModelMetadata_InitProperties()
    {
        var meta = new ModelMetadata
        {
            MaxSequenceLength = 2048,
            ConfigMaxSequenceLength = 4096,
            ModelName = "phi3",
            VocabSize = 32000
        };

        Assert.Equal(2048, meta.MaxSequenceLength);
        Assert.Equal(4096, meta.ConfigMaxSequenceLength);
        Assert.Equal("phi3", meta.ModelName);
        Assert.Equal(32000, meta.VocabSize);
    }

    [Fact]
    public void ModelMetadata_RecordEquality()
    {
        var a = new ModelMetadata { MaxSequenceLength = 128, ConfigMaxSequenceLength = 128, ModelName = "test", VocabSize = 1000 };
        var b = new ModelMetadata { MaxSequenceLength = 128, ConfigMaxSequenceLength = 128, ModelName = "test", VocabSize = 1000 };

        Assert.Equal(a, b);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — full config
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_FullConfig_ReturnsAllFields()
    {
        var config = """
        {
            "model": {
                "type": "phi3",
                "context_length": 4096,
                "vocab_size": 32064
            },
            "search": {
                "max_length": 2048
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        // search.max_length takes priority
        Assert.Equal(2048, result.MaxSequenceLength);
        Assert.Equal(2048, result.ConfigMaxSequenceLength);
        Assert.Equal("phi3", result.ModelName);
        Assert.Equal(32064, result.VocabSize);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — max_length resolution
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_SearchMaxLength_TakesPriority()
    {
        var config = """
        {
            "model": {
                "context_length": 8192,
                "max_length": 4096
            },
            "search": {
                "max_length": 1024
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(1024, result.MaxSequenceLength);
    }

    [Fact]
    public void TryParse_NoSearchSection_FallsBackToContextLength()
    {
        var config = """
        {
            "model": {
                "context_length": 8192,
                "vocab_size": 32000
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(8192, result.MaxSequenceLength);
    }

    [Fact]
    public void TryParse_NoContextLength_FallsBackToModelMaxLength()
    {
        var config = """
        {
            "model": {
                "max_length": 512,
                "vocab_size": 30000
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(512, result.MaxSequenceLength);
    }

    [Fact]
    public void TryParse_NoLengthInfo_ReturnsZero()
    {
        var config = """
        {
            "model": {
                "type": "qwen2"
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(0, result.MaxSequenceLength);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — model name resolution
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_NoModelType_FallsBackToDirectoryName()
    {
        var config = """
        {
            "search": {
                "max_length": 128
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.NotNull(result.ModelName);
        // Model name is the temp directory name
        Assert.Equal(Path.GetFileName(dir), result.ModelName);
    }

    [Fact]
    public void TryParse_ModelType_UsedAsName()
    {
        var config = """
        {
            "model": {
                "type": "llama"
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal("llama", result.ModelName);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — vocab size
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_NoVocabSize_ReturnsNull()
    {
        var config = """
        {
            "model": {
                "type": "test"
            }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Null(result.VocabSize);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — error handling
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_NullPath_ReturnsNull()
    {
        Assert.Null(GenAIConfigParser.TryParse(null!));
    }

    [Fact]
    public void TryParse_EmptyPath_ReturnsNull()
    {
        Assert.Null(GenAIConfigParser.TryParse(""));
    }

    [Fact]
    public void TryParse_NonExistentDir_ReturnsNull()
    {
        Assert.Null(GenAIConfigParser.TryParse(@"C:\nonexistent\path\that\does\not\exist"));
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsNull()
    {
        var dir = CreateTempModelDir("not valid json {{{");

        Assert.Null(GenAIConfigParser.TryParse(dir));
    }

    [Fact]
    public void TryParse_EmptyJson_ReturnsMetadata()
    {
        var dir = CreateTempModelDir("{}");
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(0, result.MaxSequenceLength);
        Assert.Equal(Path.GetFileName(dir), result.ModelName);
        Assert.Null(result.VocabSize);
    }

    // ──────────────────────────────────────────────
    // GenAIConfigParser — effective limit (options clamping)
    // ──────────────────────────────────────────────

    [Fact]
    public void TryParse_OptionsSmaller_ClamsMaxSequenceLength()
    {
        var config = """
        {
            "search": { "max_length": 131072 }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir, optionsMaxSequenceLength: 2048);

        Assert.NotNull(result);
        Assert.Equal(2048, result.MaxSequenceLength);
        Assert.Equal(131072, result.ConfigMaxSequenceLength);
    }

    [Fact]
    public void TryParse_ConfigSmaller_ConfigWins()
    {
        var config = """
        {
            "search": { "max_length": 512 }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir, optionsMaxSequenceLength: 2048);

        Assert.NotNull(result);
        Assert.Equal(512, result.MaxSequenceLength);
        Assert.Equal(512, result.ConfigMaxSequenceLength);
    }

    [Fact]
    public void TryParse_NoOptionsMaxLength_UsesConfigValue()
    {
        var config = """
        {
            "search": { "max_length": 4096 }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir);

        Assert.NotNull(result);
        Assert.Equal(4096, result.MaxSequenceLength);
        Assert.Equal(4096, result.ConfigMaxSequenceLength);
    }

    [Fact]
    public void TryParse_ConfigZero_OptionsProvided_UsesOptions()
    {
        var config = """
        {
            "model": { "type": "test" }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir, optionsMaxSequenceLength: 2048);

        Assert.NotNull(result);
        Assert.Equal(2048, result.MaxSequenceLength);
        Assert.Equal(0, result.ConfigMaxSequenceLength);
    }

    [Fact]
    public void TryParse_ConfigMaxSequenceLength_PreservesRawValue()
    {
        var config = """
        {
            "model": { "context_length": 131072 },
            "search": { "max_length": 131072 }
        }
        """;

        var dir = CreateTempModelDir(config);
        var result = GenAIConfigParser.TryParse(dir, optionsMaxSequenceLength: 2048);

        Assert.NotNull(result);
        Assert.Equal(131072, result.ConfigMaxSequenceLength);
        Assert.Equal(2048, result.MaxSequenceLength);
    }

    // ──────────────────────────────────────────────
    // LocalChatClient.ModelInfo — before initialization
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelInfo_BeforeInitialization_IsNull()
    {
        var downloader = NSubstitute.Substitute.For<IModelDownloader>();
        using var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        Assert.Null(client.ModelInfo);
    }

    // ──────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
