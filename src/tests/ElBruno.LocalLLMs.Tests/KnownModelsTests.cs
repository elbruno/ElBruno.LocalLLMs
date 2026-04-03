using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="KnownModels"/> — the pre-defined model registry.
/// </summary>
public class KnownModelsTests
{
    // ──────────────────────────────────────────────
    // All collection
    // ──────────────────────────────────────────────

    [Fact]
    public void All_IsNotNull()
    {
        Assert.NotNull(KnownModels.All);
    }

    [Fact]
    public void All_IsNotEmpty()
    {
        Assert.NotEmpty(KnownModels.All);
    }

    [Fact]
    public void All_ContainsPhi35MiniInstruct()
    {
        Assert.Contains(KnownModels.All, m => m.Id == "phi-3.5-mini-instruct");
    }

    [Fact]
    public void All_ContainsPhi4()
    {
        Assert.Contains(KnownModels.All, m => m.Id == "phi-4");
    }

    [Fact]
    public void All_ContainsQwen25_05BInstruct()
    {
        Assert.Contains(KnownModels.All, m => m.Id == "qwen2.5-0.5b-instruct");
    }

    [Fact]
    public void All_HasAtLeastThreeModels()
    {
        Assert.True(KnownModels.All.Count >= 3);
    }

    [Fact]
    public void All_AllModelsHaveUniqueIds()
    {
        var ids = KnownModels.All.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void All_AllModelsHaveNonEmptyIds()
    {
        Assert.All(KnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
        });
    }

    [Fact]
    public void All_AllModelsHaveDisplayNames()
    {
        Assert.All(KnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
        });
    }

    [Fact]
    public void All_AllModelsHaveHuggingFaceRepoIds()
    {
        Assert.All(KnownModels.All, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
        });
    }

    [Fact]
    public void All_AllModelsHaveRequiredFiles()
    {
        Assert.All(KnownModels.All, model =>
        {
            Assert.NotNull(model.RequiredFiles);
            Assert.NotEmpty(model.RequiredFiles);
        });
    }

    [Fact]
    public void All_IsReadOnly()
    {
        // IReadOnlyList — cannot add/remove at compile time.
        // Runtime check: collection should not be mutable.
        var list = KnownModels.All;
        Assert.IsAssignableFrom<IReadOnlyList<ModelDefinition>>(list);
    }

    // ──────────────────────────────────────────────
    // FindById
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("phi-3.5-mini-instruct")]
    [InlineData("phi-4")]
    [InlineData("qwen2.5-0.5b-instruct")]
    public void FindById_KnownId_ReturnsModel(string modelId)
    {
        var model = KnownModels.FindById(modelId);

        Assert.NotNull(model);
        Assert.Equal(modelId, model!.Id);
    }

    [Theory]
    [InlineData("nonexistent-model")]
    [InlineData("")]
    public void FindById_UnknownId_ReturnsNull(string modelId)
    {
        var model = KnownModels.FindById(modelId);

        Assert.Null(model);
    }

    [Fact]
    public void FindById_IsCaseInsensitive()
    {
        var model = KnownModels.FindById("PHI-3.5-MINI-INSTRUCT");

        Assert.NotNull(model);
        Assert.Equal("phi-3.5-mini-instruct", model!.Id);
    }

    [Fact]
    public void FindById_AllModelsAreDiscoverable()
    {
        foreach (var expected in KnownModels.All)
        {
            var found = KnownModels.FindById(expected.Id);
            Assert.NotNull(found);
            Assert.Equal(expected.Id, found!.Id);
        }
    }

    // ──────────────────────────────────────────────
    // Specific model property checks
    // ──────────────────────────────────────────────

    [Fact]
    public void Phi35MiniInstruct_HasCorrectProperties()
    {
        var model = KnownModels.Phi35MiniInstruct;

        Assert.Equal("phi-3.5-mini-instruct", model.Id);
        Assert.Equal("Phi-3.5 mini instruct", model.DisplayName);
        Assert.Equal("microsoft/Phi-3.5-mini-instruct-onnx", model.HuggingFaceRepoId);
        Assert.Equal(OnnxModelType.GenAI, model.ModelType);
        Assert.Equal(ChatTemplateFormat.Phi3, model.ChatTemplate);
        Assert.Equal(ModelTier.Small, model.Tier);
        Assert.True(model.HasNativeOnnx);
        Assert.Equal("gpu/gpu-int4-awq-block-128", model.ModelSubPath);
        Assert.All(model.RequiredFiles, f => Assert.StartsWith("gpu/", f));
    }

    [Fact]
    public void Phi4_HasCorrectProperties()
    {
        var model = KnownModels.Phi4;

        Assert.Equal("phi-4", model.Id);
        Assert.Equal("Phi-4", model.DisplayName);
        Assert.Equal("microsoft/phi-4-onnx", model.HuggingFaceRepoId);
        Assert.Equal(OnnxModelType.GenAI, model.ModelType);
        Assert.Equal(ChatTemplateFormat.Phi3, model.ChatTemplate);
        Assert.Equal(ModelTier.Medium, model.Tier);
        Assert.True(model.HasNativeOnnx);
        Assert.Equal("gpu/gpu-int4-rtn-block-32", model.ModelSubPath);
        Assert.All(model.RequiredFiles, f => Assert.StartsWith("gpu/", f));
    }

    [Fact]
    public void Qwen25_05BInstruct_HasCorrectProperties()
    {
        var model = KnownModels.Qwen25_05BInstruct;

        Assert.Equal("qwen2.5-0.5b-instruct", model.Id);
        Assert.Equal("Qwen2.5-0.5B-Instruct", model.DisplayName);
        Assert.Equal("elbruno/Qwen2.5-0.5B-Instruct-onnx", model.HuggingFaceRepoId);
        Assert.Equal(OnnxModelType.GenAI, model.ModelType);
        Assert.Equal(ChatTemplateFormat.Qwen, model.ChatTemplate);
        Assert.Equal(ModelTier.Tiny, model.Tier);
        Assert.True(model.HasNativeOnnx);
    }

    // ──────────────────────────────────────────────
    // Gemma 4 models
    // ──────────────────────────────────────────────

    [Fact]
    public void Gemma4E2BIT_HasCorrectProperties()
    {
        var model = KnownModels.Gemma4E2BIT;

        Assert.Equal("gemma-4-e2b-it", model.Id);
        Assert.Equal("Gemma-4-E2B-IT", model.DisplayName);
        Assert.Equal(ChatTemplateFormat.Gemma, model.ChatTemplate);
        Assert.False(model.HasNativeOnnx);
        Assert.True(model.SupportsToolCalling);
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
    }

    [Fact]
    public void Gemma4E4BIT_HasCorrectProperties()
    {
        var model = KnownModels.Gemma4E4BIT;

        Assert.Equal("gemma-4-e4b-it", model.Id);
        Assert.Equal("Gemma-4-E4B-IT", model.DisplayName);
        Assert.Equal(ChatTemplateFormat.Gemma, model.ChatTemplate);
        Assert.False(model.HasNativeOnnx);
        Assert.True(model.SupportsToolCalling);
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
    }

    [Fact]
    public void Gemma4_26BA4BIT_HasCorrectProperties()
    {
        var model = KnownModels.Gemma4_26BA4BIT;

        Assert.Equal("gemma-4-26b-a4b-it", model.Id);
        Assert.Equal("Gemma-4-26B-A4B-IT", model.DisplayName);
        Assert.Equal(ChatTemplateFormat.Gemma, model.ChatTemplate);
        Assert.False(model.HasNativeOnnx);
        Assert.True(model.SupportsToolCalling);
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
    }

    [Fact]
    public void Gemma4_31BIT_HasCorrectProperties()
    {
        var model = KnownModels.Gemma4_31BIT;

        Assert.Equal("gemma-4-31b-it", model.Id);
        Assert.Equal("Gemma-4-31B-IT", model.DisplayName);
        Assert.Equal(ChatTemplateFormat.Gemma, model.ChatTemplate);
        Assert.False(model.HasNativeOnnx);
        Assert.True(model.SupportsToolCalling);
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
    }

    [Theory]
    [InlineData("gemma-4-e2b-it")]
    [InlineData("gemma-4-e4b-it")]
    [InlineData("gemma-4-26b-a4b-it")]
    [InlineData("gemma-4-31b-it")]
    public void FindById_Gemma4Models_ReturnsCorrectModel(string modelId)
    {
        var model = KnownModels.FindById(modelId);

        Assert.NotNull(model);
        Assert.Equal(modelId, model!.Id);
    }

    [Fact]
    public void All_ContainsAllGemma4Models()
    {
        Assert.Contains(KnownModels.All, m => m.Id == "gemma-4-e2b-it");
        Assert.Contains(KnownModels.All, m => m.Id == "gemma-4-e4b-it");
        Assert.Contains(KnownModels.All, m => m.Id == "gemma-4-26b-a4b-it");
        Assert.Contains(KnownModels.All, m => m.Id == "gemma-4-31b-it");
    }

    // ──────────────────────────────────────────────
    // Static field references
    // ──────────────────────────────────────────────

    [Fact]
    public void StaticFields_AreSameInstancesAsInAll()
    {
        Assert.Contains(KnownModels.Phi35MiniInstruct, KnownModels.All);
        Assert.Contains(KnownModels.Phi4, KnownModels.All);
        Assert.Contains(KnownModels.Qwen25_05BInstruct, KnownModels.All);
    }
}
