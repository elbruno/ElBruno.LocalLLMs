namespace ElBruno.LocalLLMs;

/// <summary>
/// Pre-defined model definitions for supported LLMs.
/// These are the models the library knows how to download, configure, and run.
/// </summary>
public static class KnownModels
{
    /// <summary>Qwen2.5-0.5B-Instruct — tiny edge model.</summary>
    public static readonly ModelDefinition Qwen25_05BInstruct = new()
    {
        Id = "qwen2.5-0.5b-instruct",
        DisplayName = "Qwen2.5-0.5B-Instruct",
        HuggingFaceRepoId = "Qwen/Qwen2.5-0.5B-Instruct",
        RequiredFiles = ["onnx/model.onnx"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = false
    };

    /// <summary>Phi-3.5 mini instruct — recommended starting point (small).</summary>
    public static readonly ModelDefinition Phi35MiniInstruct = new()
    {
        Id = "phi-3.5-mini-instruct",
        DisplayName = "Phi-3.5 mini instruct",
        HuggingFaceRepoId = "microsoft/Phi-3.5-mini-instruct-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Small,
        HasNativeOnnx = true
    };

    /// <summary>Phi-4 — medium-sized production model.</summary>
    public static readonly ModelDefinition Phi4 = new()
    {
        Id = "phi-4",
        DisplayName = "Phi-4",
        HuggingFaceRepoId = "microsoft/phi-4-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>
    /// Returns all known model definitions.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> All { get; } =
    [
        Qwen25_05BInstruct,
        Phi35MiniInstruct,
        Phi4,
    ];

    /// <summary>
    /// Finds a model by its ID string. Returns null if not found.
    /// </summary>
    public static ModelDefinition? FindById(string modelId) =>
        All.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
}
