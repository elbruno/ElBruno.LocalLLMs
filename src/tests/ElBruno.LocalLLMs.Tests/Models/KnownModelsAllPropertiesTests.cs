using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests.Models;

/// <summary>
/// Comprehensive property tests for every model in <see cref="KnownModels"/>.
/// Each theory row covers one model: id, display name, HuggingFace repo, model type,
/// chat template, tier, HasNativeOnnx, and SupportsToolCalling.
///
/// These are data-driven so that adding a new model to KnownModels.cs forces a test update here.
/// </summary>
public class KnownModelsAllPropertiesTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // All models: id round-trip, non-empty display name and repo id
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_HaveNonEmptyId(ModelDefinition model)
    {
        Assert.False(string.IsNullOrWhiteSpace(model.Id));
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_HaveNonEmptyDisplayName(ModelDefinition model)
    {
        Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_HaveNonEmptyHuggingFaceRepoId(ModelDefinition model)
    {
        Assert.False(string.IsNullOrWhiteSpace(model.HuggingFaceRepoId));
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_HaveNonEmptyRequiredFiles(ModelDefinition model)
    {
        Assert.NotEmpty(model.RequiredFiles);
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_FindById_ReturnsModel(ModelDefinition model)
    {
        var found = KnownModels.FindById(model.Id);
        Assert.NotNull(found);
        Assert.Equal(model.Id, found!.Id);
    }

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_FindById_IsCaseInsensitive(ModelDefinition model)
    {
        var found = KnownModels.FindById(model.Id.ToUpperInvariant());
        Assert.NotNull(found);
        Assert.Equal(model.Id, found!.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-model property assertions (id, repo, type, template, tier, onnx, tools)
    // ──────────────────────────────────────────────────────────────────────────

    public record ModelSpec(
        ModelDefinition Model,
        string ExpectedId,
        string ExpectedRepo,
        OnnxModelType ExpectedType,
        ChatTemplateFormat ExpectedTemplate,
        ModelTier ExpectedTier,
        bool HasNativeOnnx,
        bool ToolCalling,
        bool VisionCapable);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedId(ModelSpec spec)
        => Assert.Equal(spec.ExpectedId, spec.Model.Id);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedHuggingFaceRepoId(ModelSpec spec)
        => Assert.Equal(spec.ExpectedRepo, spec.Model.HuggingFaceRepoId);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedModelType(ModelSpec spec)
        => Assert.Equal(spec.ExpectedType, spec.Model.ModelType);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedChatTemplate(ModelSpec spec)
        => Assert.Equal(spec.ExpectedTemplate, spec.Model.ChatTemplate);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedTier(ModelSpec spec)
        => Assert.Equal(spec.ExpectedTier, spec.Model.Tier);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedNativeOnnxFlag(ModelSpec spec)
        => Assert.Equal(spec.HasNativeOnnx, spec.Model.HasNativeOnnx);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedToolCallingFlag(ModelSpec spec)
        => Assert.Equal(spec.ToolCalling, spec.Model.SupportsToolCalling);

    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void Model_HasExpectedVisionCapableFlag(ModelSpec spec)
        => Assert.Equal(spec.VisionCapable, spec.Model.IsVisionCapable);

    // ──────────────────────────────────────────────────────────────────────────
    // Native ONNX models must have elbruno/* or well-known publisher repos
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(NativeOnnxModels))]
    public void NativeOnnxModels_HavePublishedRepo(ModelDefinition model)
    {
        // All native ONNX repos should be from a known publisher:
        // elbruno/, microsoft/, onnx-community/, google/, onnxruntime/
        var repo = model.HuggingFaceRepoId;
        var knownPrefixes = new[] { "elbruno/", "microsoft/", "onnx-community/", "google/", "onnxruntime/" };
        Assert.True(
            knownPrefixes.Any(p => repo.StartsWith(p, StringComparison.OrdinalIgnoreCase)),
            $"Model '{model.Id}' has HasNativeOnnx=true but repo '{repo}' is not from a known ONNX publisher.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MemberData sources
    // ──────────────────────────────────────────────────────────────────────────

    public static TheoryData<ModelDefinition> AllModels()
    {
        var data = new TheoryData<ModelDefinition>();
        foreach (var m in KnownModels.All)
            data.Add(m);
        return data;
    }

    public static TheoryData<ModelDefinition> NativeOnnxModels()
    {
        var data = new TheoryData<ModelDefinition>();
        foreach (var m in KnownModels.All.Where(m => m.HasNativeOnnx))
            data.Add(m);
        return data;
    }

    public static TheoryData<ModelSpec> ModelSpecs()
    {
        var data = new TheoryData<ModelSpec>();
        foreach (var s in AllSpecs)
            data.Add(s);
        return data;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Ground-truth spec table — one row per model
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly ModelSpec[] AllSpecs =
    [
        // ── Tiny tier ──────────────────────────────────────────────────────────
        new(KnownModels.TinyLlama11BChat,
            "tinyllama-1.1b-chat", "elbruno/TinyLlama-1.1B-Chat-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.ChatML, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.SmolLM2_17BInstruct,
            "smollm2-1.7b-instruct", "elbruno/SmolLM2-1.7B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.ChatML, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Qwen25_05BInstruct,
            "qwen2.5-0.5b-instruct", "elbruno/Qwen2.5-0.5B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Qwen25_05B_ToolCalling,
            "qwen2.5-0.5b-localllms-toolcalling", "elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Qwen25_05B_RAG,
            "qwen2.5-0.5b-localllms-rag", "elbruno/Qwen2.5-0.5B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Qwen25_05B_Instruct_FineTuned,
            "qwen2.5-0.5b-localllms-instruct", "elbruno/Qwen2.5-0.5B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Qwen25_15BInstruct,
            "qwen2.5-1.5b-instruct", "elbruno/Qwen2.5-1.5B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Gemma2BIT,
            "gemma-2b-it", "elbruno/Gemma-2B-IT-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma4E2BIT,
            "gemma-4-e2b-it", "google/gemma-4-E2B-it",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Tiny,
            HasNativeOnnx: false, ToolCalling: true, VisionCapable: false),

        new(KnownModels.StableLM2_16BChat,
            "stablelm-2-1.6b-chat", "elbruno/StableLM-2-1.6B-Chat-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.ChatML, ModelTier.Tiny,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        // ── Small tier ─────────────────────────────────────────────────────────
        new(KnownModels.Phi35MiniInstruct,
            "phi-3.5-mini-instruct", "microsoft/Phi-3.5-mini-instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Phi3, ModelTier.Small,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Qwen25_3BInstruct,
            "qwen2.5-3b-instruct", "elbruno/Qwen2.5-3B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Small,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Llama32_3BInstruct,
            "llama-3.2-3b-instruct", "elbruno/Llama-3.2-3B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Llama3, ModelTier.Small,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma2_2BIT,
            "gemma-2-2b-it", "elbruno/Gemma-2-2B-IT-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Small,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma4E4BIT,
            "gemma-4-e4b-it", "google/gemma-4-E4B-it",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Small,
            HasNativeOnnx: false, ToolCalling: true, VisionCapable: false),

        // ── Medium tier ────────────────────────────────────────────────────────
        new(KnownModels.Qwen25_7BInstruct,
            "qwen2.5-7b-instruct", "elbruno/Qwen2.5-7B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Qwen25Coder_7BInstruct,
            "qwen2.5-coder-7b-instruct", "elbruno/Qwen2.5-Coder-7B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Llama31_8BInstruct,
            "llama-3.1-8b-instruct", "elbruno/Llama-3.1-8B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Llama3, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Mistral7BInstructV03,
            "mistral-7b-instruct-v0.3", "elbruno/Mistral-7B-Instruct-v0.3-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Mistral, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma2_9BIT,
            "gemma-2-9b-it", "elbruno/Gemma-2-9B-IT-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Phi4,
            "phi-4", "microsoft/phi-4-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Phi3, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.DeepSeekR1DistillQwen14B,
            "deepseek-r1-distill-qwen-14b", "elbruno/DeepSeek-R1-Distill-Qwen-14B-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.DeepSeek, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.MistralSmall24BInstruct,
            "mistral-small-24b-instruct", "elbruno/Mistral-Small-24B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Mistral, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma4_12BIT,
            "gemma-4-12b-it", "google/gemma-4-12B-it",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Medium,
            HasNativeOnnx: false, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Fara15_9B,
            "fara1.5-9b", "elbruno/Fara1.5-9B-onnx",
            OnnxModelType.VisionGenAI, ChatTemplateFormat.Fara, ModelTier.Medium,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: true),

        // ── Large tier ─────────────────────────────────────────────────────────
        new(KnownModels.Qwen25_14BInstruct,
            "qwen2.5-14b-instruct", "elbruno/Qwen2.5-14B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Qwen25_32BInstruct,
            "qwen2.5-32b-instruct", "elbruno/Qwen2.5-32B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Llama33_70BInstruct,
            "llama-3.3-70b-instruct", "elbruno/Llama-3.3-70B-Instruct-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Llama3, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Mixtral8x7BInstructV01,
            "mixtral-8x7b-instruct-v0.1", "elbruno/Mixtral-8x7B-Instruct-v0.1-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Mistral, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.DeepSeekR1DistillLlama70B,
            "deepseek-r1-distill-llama-70b", "elbruno/DeepSeek-R1-Distill-Llama-70B-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.DeepSeek, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.CommandR35B,
            "command-r-35b", "elbruno/Command-R-35B-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.ChatML, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: false, VisionCapable: false),

        new(KnownModels.Gemma4_26BA4BIT,
            "gemma-4-26b-a4b-it", "google/gemma-4-26B-A4B-it",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Large,
            HasNativeOnnx: false, ToolCalling: true, VisionCapable: false),

        new(KnownModels.Gemma4_31BIT,
            "gemma-4-31b-it", "google/gemma-4-31B-it",
            OnnxModelType.GenAI, ChatTemplateFormat.Gemma, ModelTier.Large,
            HasNativeOnnx: false, ToolCalling: true, VisionCapable: false),

        // ── Agentic ────────────────────────────────────────────────────────────
        new(KnownModels.Qwen3_14BInstruct,
            "qwen3-14b-instruct", "onnx-community/Qwen3-14B-ONNX",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen3, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),

        new(KnownModels.MagenticBrain,
            "magentic-brain", "elbruno/MagenticBrain-onnx",
            OnnxModelType.GenAI, ChatTemplateFormat.Qwen3, ModelTier.Large,
            HasNativeOnnx: true, ToolCalling: true, VisionCapable: false),
    ];
}
