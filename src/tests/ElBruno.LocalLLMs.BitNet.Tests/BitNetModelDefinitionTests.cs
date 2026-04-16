using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetModelDefinition"/> — record equality, immutability, required props, defaults.
/// </summary>
public class BitNetModelDefinitionTests
{
    // ──────────────────────────────────────────────
    // Construction with required properties
    // ──────────────────────────────────────────────

    [Fact]
    public void Construction_WithAllRequiredProperties_Succeeds()
    {
        var model = CreateMinimalModel();

        Assert.Equal("test-bitnet", model.Id);
        Assert.Equal("Test BitNet", model.DisplayName);
        Assert.Equal("org/test-bitnet-gguf", model.HuggingFaceRepoId);
        Assert.Equal("model.gguf", model.GgufFileName);
        Assert.Equal(ChatTemplateFormat.ChatML, model.ChatTemplate);
        Assert.Equal(1.0, model.ParametersBillions);
    }

    // ──────────────────────────────────────────────
    // Default values for optional properties
    // ──────────────────────────────────────────────

    [Fact]
    public void Defaults_ContextLength_Is4096()
    {
        var model = CreateMinimalModel();

        Assert.Equal(4096, model.ContextLength);
    }

    [Fact]
    public void Defaults_ApproximateSizeMB_IsZero()
    {
        var model = CreateMinimalModel();

        Assert.Equal(0, model.ApproximateSizeMB);
    }

    [Fact]
    public void Defaults_RecommendedKernel_IsI2S()
    {
        var model = CreateMinimalModel();

        Assert.Equal(BitNetKernelType.I2_S, model.RecommendedKernel);
    }

    // ──────────────────────────────────────────────
    // Optional properties can be set
    // ──────────────────────────────────────────────

    [Fact]
    public void ContextLength_CanBeSet()
    {
        var model = CreateMinimalModel() with { ContextLength = 8192 };

        Assert.Equal(8192, model.ContextLength);
    }

    [Fact]
    public void ApproximateSizeMB_CanBeSet()
    {
        var model = CreateMinimalModel() with { ApproximateSizeMB = 650 };

        Assert.Equal(650, model.ApproximateSizeMB);
    }

    [Theory]
    [InlineData(BitNetKernelType.I2_S)]
    [InlineData(BitNetKernelType.TL1)]
    [InlineData(BitNetKernelType.TL2)]
    public void RecommendedKernel_AllValues_CanBeAssigned(BitNetKernelType kernel)
    {
        var model = CreateMinimalModel() with { RecommendedKernel = kernel };

        Assert.Equal(kernel, model.RecommendedKernel);
    }

    // ──────────────────────────────────────────────
    // Record equality
    // ──────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel();

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { Id = "different" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentDisplayName_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { DisplayName = "Other" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentHuggingFaceRepoId_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { HuggingFaceRepoId = "other/repo" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentGgufFileName_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { GgufFileName = "other.gguf" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentChatTemplate_AreNotEqual()
    {
        var a = CreateMinimalModel() with { ChatTemplate = ChatTemplateFormat.ChatML };
        var b = CreateMinimalModel() with { ChatTemplate = ChatTemplateFormat.Llama3 };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentParametersBillions_AreNotEqual()
    {
        var a = CreateMinimalModel() with { ParametersBillions = 1.0 };
        var b = CreateMinimalModel() with { ParametersBillions = 2.4 };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentContextLength_AreNotEqual()
    {
        var a = CreateMinimalModel() with { ContextLength = 2048 };
        var b = CreateMinimalModel() with { ContextLength = 4096 };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentRecommendedKernel_AreNotEqual()
    {
        var a = CreateMinimalModel() with { RecommendedKernel = BitNetKernelType.I2_S };
        var b = CreateMinimalModel() with { RecommendedKernel = BitNetKernelType.TL2 };

        Assert.NotEqual(a, b);
    }

    // ──────────────────────────────────────────────
    // Record with-expression (immutable copy)
    // ──────────────────────────────────────────────

    [Fact]
    public void WithExpression_CreatesNewInstance()
    {
        var original = CreateMinimalModel();
        var copy = original with { Id = "new-id" };

        Assert.NotSame(original, copy);
        Assert.Equal("test-bitnet", original.Id);
        Assert.Equal("new-id", copy.Id);
    }

    [Fact]
    public void WithExpression_PreservesUnchangedProperties()
    {
        var original = CreateMinimalModel() with
        {
            ContextLength = 8192,
            ApproximateSizeMB = 500,
            RecommendedKernel = BitNetKernelType.TL1
        };

        var copy = original with { Id = "modified" };

        Assert.Equal(8192, copy.ContextLength);
        Assert.Equal(500, copy.ApproximateSizeMB);
        Assert.Equal(BitNetKernelType.TL1, copy.RecommendedKernel);
        Assert.Equal("modified", copy.Id);
    }

    // ──────────────────────────────────────────────
    // Enum coverage for ChatTemplate
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    [InlineData(ChatTemplateFormat.Custom)]
    public void ChatTemplate_AllValues_CanBeAssigned(ChatTemplateFormat format)
    {
        var model = CreateMinimalModel() with { ChatTemplate = format };

        Assert.Equal(format, model.ChatTemplate);
    }

    // ──────────────────────────────────────────────
    // ParametersBillions edge cases
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(2.4)]
    [InlineData(3.3)]
    [InlineData(8.0)]
    public void ParametersBillions_AcceptsVariousValues(double value)
    {
        var model = CreateMinimalModel() with { ParametersBillions = value };

        Assert.Equal(value, model.ParametersBillions);
    }

    // ──────────────────────────────────────────────
    // Record is sealed
    // ──────────────────────────────────────────────

    [Fact]
    public void BitNetModelDefinition_IsSealed()
    {
        Assert.True(typeof(BitNetModelDefinition).IsSealed);
    }

    [Fact]
    public void BitNetModelDefinition_IsRecord()
    {
        // Records have a compiler-generated EqualityContract property
        var eqContract = typeof(BitNetModelDefinition).GetProperty(
            "EqualityContract",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(eqContract);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static BitNetModelDefinition CreateMinimalModel() => new()
    {
        Id = "test-bitnet",
        DisplayName = "Test BitNet",
        HuggingFaceRepoId = "org/test-bitnet-gguf",
        GgufFileName = "model.gguf",
        ChatTemplate = ChatTemplateFormat.ChatML,
        ParametersBillions = 1.0
    };
}
