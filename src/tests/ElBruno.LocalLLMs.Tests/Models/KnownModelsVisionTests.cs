namespace ElBruno.LocalLLMs.Tests.Models;

/// <summary>
/// Tests that <see cref="KnownModels.Fara15_9B"/> is correctly defined
/// as a VisionGenAI model with the Fara chat template.
/// Note: Fara1.5-9B requires <see cref="LocalVisionChatClient"/>. The model definition stays
/// VisionGenAI. Use scripts/convert_fara_multimodal.py to produce the complete ONNX package
/// (vision encoder + embedding injector + text decoder) needed for end-to-end loading.
/// See issue #35 for the current export-support / validation work.
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
    public void Fara15_9B_HasNativeOnnx_IsTrue()
    {
        Assert.True(KnownModels.Fara15_9B.HasNativeOnnx);
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
        Assert.Equal("elbruno/Fara1.5-9B-onnx", KnownModels.Fara15_9B.HuggingFaceRepoId);
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
