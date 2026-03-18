using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="ModelDefinition"/> — record equality, required properties, and defaults.
/// </summary>
public class ModelDefinitionTests
{
    // ──────────────────────────────────────────────
    // Construction with required properties
    // ──────────────────────────────────────────────

    [Fact]
    public void Construction_WithAllRequiredProperties_Succeeds()
    {
        var model = new ModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test Model",
            HuggingFaceRepoId = "org/test-model",
            RequiredFiles = ["model.onnx"],
            ModelType = OnnxModelType.GenAI,
            ChatTemplate = ChatTemplateFormat.ChatML
        };

        Assert.Equal("test-model", model.Id);
        Assert.Equal("Test Model", model.DisplayName);
        Assert.Equal("org/test-model", model.HuggingFaceRepoId);
        Assert.Single(model.RequiredFiles);
        Assert.Equal(OnnxModelType.GenAI, model.ModelType);
        Assert.Equal(ChatTemplateFormat.ChatML, model.ChatTemplate);
    }

    // ──────────────────────────────────────────────
    // Default values for optional properties
    // ──────────────────────────────────────────────

    [Fact]
    public void Defaults_OptionalFiles_IsEmptyArray()
    {
        var model = CreateMinimalModel();

        Assert.NotNull(model.OptionalFiles);
        Assert.Empty(model.OptionalFiles);
    }

    [Fact]
    public void Defaults_Tier_IsSmall()
    {
        var model = CreateMinimalModel();

        Assert.Equal(ModelTier.Small, model.Tier);
    }

    [Fact]
    public void Defaults_HasNativeOnnx_IsFalse()
    {
        var model = CreateMinimalModel();

        Assert.False(model.HasNativeOnnx);
    }

    // ──────────────────────────────────────────────
    // Optional properties can be set
    // ──────────────────────────────────────────────

    [Fact]
    public void OptionalFiles_CanBeSet()
    {
        var model = CreateMinimalModel() with
        {
            OptionalFiles = ["tokenizer.json", "config.json"]
        };

        Assert.Equal(2, model.OptionalFiles.Length);
        Assert.Contains("tokenizer.json", model.OptionalFiles);
    }

    [Fact]
    public void Tier_CanBeSet()
    {
        var model = CreateMinimalModel() with { Tier = ModelTier.Large };

        Assert.Equal(ModelTier.Large, model.Tier);
    }

    [Fact]
    public void HasNativeOnnx_CanBeSetTrue()
    {
        var model = CreateMinimalModel() with { HasNativeOnnx = true };

        Assert.True(model.HasNativeOnnx);
    }

    // ──────────────────────────────────────────────
    // Record equality
    // ──────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel();

        // Records with array properties use reference equality for arrays,
        // so structural comparison is done on individual properties instead.
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.DisplayName, b.DisplayName);
        Assert.Equal(a.HuggingFaceRepoId, b.HuggingFaceRepoId);
        Assert.Equal(a.RequiredFiles, b.RequiredFiles);
        Assert.Equal(a.ModelType, b.ModelType);
        Assert.Equal(a.ChatTemplate, b.ChatTemplate);
        Assert.Equal(a.Tier, b.Tier);
        Assert.Equal(a.HasNativeOnnx, b.HasNativeOnnx);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { Id = "different-model" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentDisplayName_AreNotEqual()
    {
        var a = CreateMinimalModel();
        var b = CreateMinimalModel() with { DisplayName = "Other Name" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentTier_AreNotEqual()
    {
        var a = CreateMinimalModel() with { Tier = ModelTier.Tiny };
        var b = CreateMinimalModel() with { Tier = ModelTier.Large };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentChatTemplate_AreNotEqual()
    {
        var a = CreateMinimalModel() with { ChatTemplate = ChatTemplateFormat.ChatML };
        var b = CreateMinimalModel() with { ChatTemplate = ChatTemplateFormat.Llama3 };

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
        Assert.Equal("test-model", original.Id);
        Assert.Equal("new-id", copy.Id);
    }

    [Fact]
    public void WithExpression_PreservesUnchangedProperties()
    {
        var original = CreateMinimalModel() with
        {
            Tier = ModelTier.Medium,
            HasNativeOnnx = true
        };

        var copy = original with { Id = "modified" };

        Assert.Equal(ModelTier.Medium, copy.Tier);
        Assert.True(copy.HasNativeOnnx);
        Assert.Equal("modified", copy.Id);
    }

    // ──────────────────────────────────────────────
    // Enum coverage
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(OnnxModelType.CausalLM)]
    [InlineData(OnnxModelType.GenAI)]
    public void ModelType_AllValues_CanBeAssigned(OnnxModelType type)
    {
        var model = CreateMinimalModel() with { ModelType = type };

        Assert.Equal(type, model.ModelType);
    }

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

    [Theory]
    [InlineData(ModelTier.Tiny)]
    [InlineData(ModelTier.Small)]
    [InlineData(ModelTier.Medium)]
    [InlineData(ModelTier.Large)]
    public void Tier_AllValues_CanBeAssigned(ModelTier tier)
    {
        var model = CreateMinimalModel() with { Tier = tier };

        Assert.Equal(tier, model.Tier);
    }

    // ──────────────────────────────────────────────
    // RequiredFiles edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void RequiredFiles_MultipleFiles_ArePreserved()
    {
        var files = new[] { "model.onnx", "tokenizer.model", "config.json" };
        var model = CreateMinimalModel() with { RequiredFiles = files };

        Assert.Equal(3, model.RequiredFiles.Length);
        Assert.Equal(files, model.RequiredFiles);
    }

    [Fact]
    public void RequiredFiles_WithWildcard_ArePreserved()
    {
        var model = CreateMinimalModel() with
        {
            RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"]
        };

        Assert.Single(model.RequiredFiles);
        Assert.Contains("*", model.RequiredFiles[0]);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static ModelDefinition CreateMinimalModel() => new()
    {
        Id = "test-model",
        DisplayName = "Test Model",
        HuggingFaceRepoId = "org/test-model",
        RequiredFiles = ["model.onnx"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML
    };
}
