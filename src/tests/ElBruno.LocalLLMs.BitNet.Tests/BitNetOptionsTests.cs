using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetOptions"/> — defaults, custom values, and null handling.
/// </summary>
public class BitNetOptionsTests
{
    // ──────────────────────────────────────────────
    // Default values
    // ──────────────────────────────────────────────

    [Fact]
    public void Defaults_Model_IsBitNet2B4T()
    {
        var options = new BitNetOptions();

        Assert.Equal(BitNetKnownModels.BitNet2B4T, options.Model);
    }

    [Fact]
    public void Defaults_ModelPath_IsNull()
    {
        var options = new BitNetOptions();

        Assert.Null(options.ModelPath);
    }

    [Fact]
    public void Defaults_NativeLibraryPath_IsNull()
    {
        var options = new BitNetOptions();

        Assert.Null(options.NativeLibraryPath);
    }

    [Fact]
    public void Defaults_MaxTokens_Is2048()
    {
        var options = new BitNetOptions();

        Assert.Equal(2048, options.MaxTokens);
    }

    [Fact]
    public void Defaults_Temperature_Is07()
    {
        var options = new BitNetOptions();

        Assert.Equal(0.7f, options.Temperature);
    }

    [Fact]
    public void Defaults_TopP_Is09()
    {
        var options = new BitNetOptions();

        Assert.Equal(0.9f, options.TopP);
    }

    [Fact]
    public void Defaults_TopK_Is40()
    {
        var options = new BitNetOptions();

        Assert.Equal(40, options.TopK);
    }

    [Fact]
    public void Defaults_RepetitionPenalty_Is11()
    {
        var options = new BitNetOptions();

        Assert.Equal(1.1f, options.RepetitionPenalty);
    }

    [Fact]
    public void Defaults_ThreadCount_IsProcessorCount()
    {
        var options = new BitNetOptions();

        Assert.Equal(Environment.ProcessorCount, options.ThreadCount);
    }

    [Fact]
    public void Defaults_ContextSize_Is4096()
    {
        var options = new BitNetOptions();

        Assert.Equal(4096, options.ContextSize);
    }

    [Fact]
    public void Defaults_SystemPrompt_IsNull()
    {
        var options = new BitNetOptions();

        Assert.Null(options.SystemPrompt);
    }

    [Fact]
    public void Defaults_ChatTemplateOverride_IsNull()
    {
        var options = new BitNetOptions();

        Assert.Null(options.ChatTemplateOverride);
    }

    // ──────────────────────────────────────────────
    // Custom values
    // ──────────────────────────────────────────────

    [Fact]
    public void Custom_Model_CanBeSet()
    {
        var options = new BitNetOptions
        {
            Model = BitNetKnownModels.Falcon3_1B
        };

        Assert.Equal(BitNetKnownModels.Falcon3_1B, options.Model);
    }

    [Fact]
    public void Custom_ModelPath_CanBeSet()
    {
        var options = new BitNetOptions
        {
            ModelPath = @"C:\models\bitnet\model.gguf"
        };

        Assert.Equal(@"C:\models\bitnet\model.gguf", options.ModelPath);
    }

    [Fact]
    public void Custom_NativeLibraryPath_CanBeSet()
    {
        var options = new BitNetOptions
        {
            NativeLibraryPath = @"C:\libs\bitnet"
        };

        Assert.Equal(@"C:\libs\bitnet", options.NativeLibraryPath);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(8192)]
    public void Custom_MaxTokens_CanBeSet(int maxTokens)
    {
        var options = new BitNetOptions { MaxTokens = maxTokens };

        Assert.Equal(maxTokens, options.MaxTokens);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    public void Custom_Temperature_CanBeSet(float temp)
    {
        var options = new BitNetOptions { Temperature = temp };

        Assert.Equal(temp, options.Temperature);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Custom_TopP_CanBeSet(float topP)
    {
        var options = new BitNetOptions { TopP = topP };

        Assert.Equal(topP, options.TopP);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Custom_TopK_CanBeSet(int topK)
    {
        var options = new BitNetOptions { TopK = topK };

        Assert.Equal(topK, options.TopK);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    [InlineData(2.0f)]
    public void Custom_RepetitionPenalty_CanBeSet(float penalty)
    {
        var options = new BitNetOptions { RepetitionPenalty = penalty };

        Assert.Equal(penalty, options.RepetitionPenalty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Custom_ThreadCount_CanBeSet(int threads)
    {
        var options = new BitNetOptions { ThreadCount = threads };

        Assert.Equal(threads, options.ThreadCount);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void Custom_ContextSize_CanBeSet(int size)
    {
        var options = new BitNetOptions { ContextSize = size };

        Assert.Equal(size, options.ContextSize);
    }

    [Fact]
    public void Custom_SystemPrompt_CanBeSet()
    {
        var options = new BitNetOptions
        {
            SystemPrompt = "You are a helpful assistant."
        };

        Assert.Equal("You are a helpful assistant.", options.SystemPrompt);
    }

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    public void Custom_ChatTemplateOverride_CanBeSet(ChatTemplateFormat format)
    {
        var options = new BitNetOptions { ChatTemplateOverride = format };

        Assert.Equal(format, options.ChatTemplateOverride);
    }

    // ──────────────────────────────────────────────
    // Null handling
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelPath_CanBeSetToNull()
    {
        var options = new BitNetOptions { ModelPath = "some/path" };
        options.ModelPath = null;

        Assert.Null(options.ModelPath);
    }

    [Fact]
    public void NativeLibraryPath_CanBeSetToNull()
    {
        var options = new BitNetOptions { NativeLibraryPath = "some/path" };
        options.NativeLibraryPath = null;

        Assert.Null(options.NativeLibraryPath);
    }

    [Fact]
    public void SystemPrompt_CanBeSetToNull()
    {
        var options = new BitNetOptions { SystemPrompt = "prompt" };
        options.SystemPrompt = null;

        Assert.Null(options.SystemPrompt);
    }

    [Fact]
    public void ChatTemplateOverride_CanBeSetToNull()
    {
        var options = new BitNetOptions { ChatTemplateOverride = ChatTemplateFormat.ChatML };
        options.ChatTemplateOverride = null;

        Assert.Null(options.ChatTemplateOverride);
    }

    // ──────────────────────────────────────────────
    // Mutation: options are mutable, verify round-trip
    // ──────────────────────────────────────────────

    [Fact]
    public void Options_AreFullyMutable()
    {
        var options = new BitNetOptions();

        options.Model = BitNetKnownModels.Falcon3_3B;
        options.ModelPath = @"C:\test\model.gguf";
        options.NativeLibraryPath = @"C:\libs";
        options.MaxTokens = 512;
        options.Temperature = 0.3f;
        options.TopP = 0.5f;
        options.TopK = 10;
        options.RepetitionPenalty = 1.5f;
        options.ThreadCount = 8;
        options.ContextSize = 2048;
        options.SystemPrompt = "Be concise.";
        options.ChatTemplateOverride = ChatTemplateFormat.Phi3;

        Assert.Equal(BitNetKnownModels.Falcon3_3B, options.Model);
        Assert.Equal(@"C:\test\model.gguf", options.ModelPath);
        Assert.Equal(@"C:\libs", options.NativeLibraryPath);
        Assert.Equal(512, options.MaxTokens);
        Assert.Equal(0.3f, options.Temperature);
        Assert.Equal(0.5f, options.TopP);
        Assert.Equal(10, options.TopK);
        Assert.Equal(1.5f, options.RepetitionPenalty);
        Assert.Equal(8, options.ThreadCount);
        Assert.Equal(2048, options.ContextSize);
        Assert.Equal("Be concise.", options.SystemPrompt);
        Assert.Equal(ChatTemplateFormat.Phi3, options.ChatTemplateOverride);
    }
}
