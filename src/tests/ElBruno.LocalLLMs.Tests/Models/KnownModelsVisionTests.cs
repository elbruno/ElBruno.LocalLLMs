namespace ElBruno.LocalLLMs.Tests.Models;

/// <summary>
/// Tests that <see cref="KnownModels.Fara15_9B"/> is correctly defined
/// as a VisionGenAI model with the Fara chat template.
/// </summary>
[Trait("Category", "Fara")]
public class KnownModelsVisionTests
{
    [Fact]
    public void Fara15_9B_IsNotNull()
    {
        Assert.NotNull(KnownModels.Fara15_9B);
    }

    [Fact]
    public void Fara15_9B_ModelType_IsVisionGenAI()
    {
        Assert.Equal(OnnxModelType.VisionGenAI, KnownModels.Fara15_9B.ModelType);
    }

    [Fact]
    public void Fara15_9B_HasNativeOnnx_IsFalse()
    {
        Assert.False(KnownModels.Fara15_9B.HasNativeOnnx);
    }

    [Fact]
    public void Fara15_9B_ChatTemplate_IsFara()
    {
        Assert.Equal(ChatTemplateFormat.Fara, KnownModels.Fara15_9B.ChatTemplate);
    }

    [Fact]
    public void Fara15_9B_Tier_IsMedium()
    {
        Assert.Equal(ModelTier.Medium, KnownModels.Fara15_9B.Tier);
    }

    [Fact]
    public void Fara15_9B_SupportsToolCalling_IsFalse()
    {
        Assert.False(KnownModels.Fara15_9B.SupportsToolCalling);
    }

    [Fact]
    public void Fara15_9B_HuggingFaceRepoId_IsCorrect()
    {
        Assert.Equal("microsoft/Fara1.5-9B", KnownModels.Fara15_9B.HuggingFaceRepoId);
    }

    [Fact]
    public void Fara15_9B_AppearsIn_KnownModelsAll()
    {
        Assert.Contains(KnownModels.Fara15_9B, KnownModels.All);
    }

    [Fact]
    public void ChatTemplateFormat_Fara_EnumValueExists()
    {
        Assert.True(
            Enum.IsDefined(typeof(ChatTemplateFormat), "Fara"),
            "ChatTemplateFormat.Fara must be defined");
    }
}
