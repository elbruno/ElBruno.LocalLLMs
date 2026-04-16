using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetKnownModels"/> — the pre-defined BitNet model catalog.
/// </summary>
public class BitNetKnownModelsTests
{
    // ──────────────────────────────────────────────
    // All collection
    // ──────────────────────────────────────────────

    [Fact]
    public void All_IsNotNull()
    {
        Assert.NotNull(BitNetKnownModels.All);
    }

    [Fact]
    public void All_IsNotEmpty()
    {
        Assert.NotEmpty(BitNetKnownModels.All);
    }

    [Fact]
    public void All_ContainsExactlyFiveModels()
    {
        Assert.Equal(5, BitNetKnownModels.All.Count);
    }

    [Fact]
    public void All_ContainsBitNet2B4T()
    {
        Assert.Contains(BitNetKnownModels.All, m => m.Id == "bitnet-b1.58-2b-4t");
    }

    [Fact]
    public void All_ContainsBitNet07B()
    {
        Assert.Contains(BitNetKnownModels.All, m => m.Id == "bitnet-b1.58-0.7b");
    }

    [Fact]
    public void All_ContainsBitNet3B()
    {
        Assert.Contains(BitNetKnownModels.All, m => m.Id == "bitnet-b1.58-3b");
    }

    [Fact]
    public void All_ContainsFalcon3_1B()
    {
        Assert.Contains(BitNetKnownModels.All, m => m.Id == "falcon3-1b-instruct-1.58bit");
    }

    [Fact]
    public void All_ContainsFalcon3_3B()
    {
        Assert.Contains(BitNetKnownModels.All, m => m.Id == "falcon3-3b-instruct-1.58bit");
    }

    [Fact]
    public void All_AllModelsHaveUniqueIds()
    {
        var ids = BitNetKnownModels.All.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void All_AllModelsHaveNonEmptyIds()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
        });
    }

    [Fact]
    public void All_AllModelsHaveDisplayNames()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
        });
    }

    [Fact]
    public void All_AllModelsHaveHuggingFaceRepoIds()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
        });
    }

    [Fact]
    public void All_AllModelsHaveGgufFileNames()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.GgufFileName));
        });
    }

    [Fact]
    public void All_AllModelsHavePositiveParametersBillions()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.True(model.ParametersBillions > 0,
                $"Model {model.Id} has ParametersBillions={model.ParametersBillions}");
        });
    }

    [Fact]
    public void All_AllModelsHavePositiveContextLength()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.True(model.ContextLength > 0,
                $"Model {model.Id} has ContextLength={model.ContextLength}");
        });
    }

    [Fact]
    public void All_IsReadOnly()
    {
        var list = BitNetKnownModels.All;
        Assert.IsAssignableFrom<IReadOnlyList<BitNetModelDefinition>>(list);
    }

    // ──────────────────────────────────────────────
    // Default model
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultModel_IsBitNet2B4T()
    {
        // BitNetOptions defaults to BitNet2B4T
        var options = new BitNetOptions();
        Assert.Same(BitNetKnownModels.BitNet2B4T, options.Model);
    }

    // ──────────────────────────────────────────────
    // FindById
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("bitnet-b1.58-2b-4t")]
    [InlineData("bitnet-b1.58-0.7b")]
    [InlineData("bitnet-b1.58-3b")]
    [InlineData("falcon3-1b-instruct-1.58bit")]
    [InlineData("falcon3-3b-instruct-1.58bit")]
    public void FindById_KnownId_ReturnsModel(string modelId)
    {
        var model = BitNetKnownModels.FindById(modelId);

        Assert.NotNull(model);
        Assert.Equal(modelId, model!.Id);
    }

    [Theory]
    [InlineData("nonexistent-model")]
    [InlineData("")]
    [InlineData("bitnet")]
    public void FindById_UnknownId_ReturnsNull(string modelId)
    {
        var model = BitNetKnownModels.FindById(modelId);

        Assert.Null(model);
    }

    [Fact]
    public void FindById_IsCaseInsensitive()
    {
        var model = BitNetKnownModels.FindById("BITNET-B1.58-2B-4T");

        Assert.NotNull(model);
        Assert.Equal("bitnet-b1.58-2b-4t", model!.Id);
    }

    [Fact]
    public void FindById_MixedCase_ReturnsModel()
    {
        var model = BitNetKnownModels.FindById("Falcon3-1B-Instruct-1.58bit");

        Assert.NotNull(model);
        Assert.Equal("falcon3-1b-instruct-1.58bit", model!.Id);
    }

    [Fact]
    public void FindById_AllModelsAreDiscoverable()
    {
        foreach (var expected in BitNetKnownModels.All)
        {
            var found = BitNetKnownModels.FindById(expected.Id);
            Assert.NotNull(found);
            Assert.Equal(expected.Id, found!.Id);
        }
    }

    // ──────────────────────────────────────────────
    // HuggingFace repo ID validation
    // ──────────────────────────────────────────────

    [Fact]
    public void All_HuggingFaceRepoIds_ContainSlash()
    {
        Assert.All(BitNetKnownModels.All, model =>
        {
            Assert.Contains("/", model.HuggingFaceRepoId);
        });
    }

    // ──────────────────────────────────────────────
    // Specific model property checks
    // ──────────────────────────────────────────────

    [Fact]
    public void BitNet2B4T_HasCorrectProperties()
    {
        var model = BitNetKnownModels.BitNet2B4T;

        Assert.Equal("bitnet-b1.58-2b-4t", model.Id);
        Assert.Equal("BitNet b1.58 2B-4T", model.DisplayName);
        Assert.Equal("microsoft/BitNet-b1.58-2B-4T-gguf", model.HuggingFaceRepoId);
        Assert.Equal("ggml-model-i2_s.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.Llama3, model.ChatTemplate);
        Assert.Equal(2.4, model.ParametersBillions);
        Assert.Equal(4096, model.ContextLength);
        Assert.Equal(400, model.ApproximateSizeMB);
    }

    [Fact]
    public void BitNet07B_HasCorrectProperties()
    {
        var model = BitNetKnownModels.BitNet07B;

        Assert.Equal("bitnet-b1.58-0.7b", model.Id);
        Assert.Equal("BitNet b1.58 0.7B", model.DisplayName);
        Assert.Equal("1bitLLM/bitnet_b1_58-large", model.HuggingFaceRepoId);
        Assert.Equal("ggml-model-i2_s.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.Llama3, model.ChatTemplate);
        Assert.Equal(0.7, model.ParametersBillions);
        Assert.Equal(2048, model.ContextLength);
        Assert.Equal(150, model.ApproximateSizeMB);
    }

    [Fact]
    public void BitNet3B_HasCorrectProperties()
    {
        var model = BitNetKnownModels.BitNet3B;

        Assert.Equal("bitnet-b1.58-3b", model.Id);
        Assert.Equal("BitNet b1.58 3B", model.DisplayName);
        Assert.Equal("1bitLLM/bitnet_b1_58-3B", model.HuggingFaceRepoId);
        Assert.Equal("ggml-model-i2_s.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.Llama3, model.ChatTemplate);
        Assert.Equal(3.3, model.ParametersBillions);
        Assert.Equal(4096, model.ContextLength);
        Assert.Equal(650, model.ApproximateSizeMB);
    }

    [Fact]
    public void Falcon3_1B_HasCorrectProperties()
    {
        var model = BitNetKnownModels.Falcon3_1B;

        Assert.Equal("falcon3-1b-instruct-1.58bit", model.Id);
        Assert.Equal("Falcon3 1B Instruct 1.58-bit", model.DisplayName);
        Assert.Equal("tiiuae/Falcon3-1B-Instruct-1.58bit", model.HuggingFaceRepoId);
        Assert.Equal("ggml-model-i2_s.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.ChatML, model.ChatTemplate);
        Assert.Equal(1.0, model.ParametersBillions);
        Assert.Equal(8192, model.ContextLength);
        Assert.Equal(200, model.ApproximateSizeMB);
    }

    [Fact]
    public void Falcon3_3B_HasCorrectProperties()
    {
        var model = BitNetKnownModels.Falcon3_3B;

        Assert.Equal("falcon3-3b-instruct-1.58bit", model.Id);
        Assert.Equal("Falcon3 3B Instruct 1.58-bit", model.DisplayName);
        Assert.Equal("tiiuae/Falcon3-3B-Instruct-1.58bit", model.HuggingFaceRepoId);
        Assert.Equal("ggml-model-i2_s.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.ChatML, model.ChatTemplate);
        Assert.Equal(3.0, model.ParametersBillions);
        Assert.Equal(8192, model.ContextLength);
        Assert.Equal(600, model.ApproximateSizeMB);
    }

    // ──────────────────────────────────────────────
    // Static field references
    // ──────────────────────────────────────────────

    [Fact]
    public void StaticFields_AreSameInstancesAsInAll()
    {
        Assert.Contains(BitNetKnownModels.BitNet2B4T, BitNetKnownModels.All);
        Assert.Contains(BitNetKnownModels.BitNet07B, BitNetKnownModels.All);
        Assert.Contains(BitNetKnownModels.BitNet3B, BitNetKnownModels.All);
        Assert.Contains(BitNetKnownModels.Falcon3_1B, BitNetKnownModels.All);
        Assert.Contains(BitNetKnownModels.Falcon3_3B, BitNetKnownModels.All);
    }
}
