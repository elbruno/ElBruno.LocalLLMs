namespace ElBruno.LocalLLMs;

/// <summary>
/// Describes everything needed to download, load, and use a specific LLM.
/// Models are data — adding a model means adding a record, not a class.
/// </summary>
public sealed record ModelDefinition
{
    /// <summary>Unique identifier for this model (e.g., "phi-3.5-mini-instruct").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>HuggingFace repository ID for download.</summary>
    public required string HuggingFaceRepoId { get; init; }

    /// <summary>
    /// Required files to download from the repo.
    /// Relative paths within the HuggingFace repo (e.g., "onnx/model.onnx").
    /// </summary>
    public required string[] RequiredFiles { get; init; }

    /// <summary>
    /// Optional files to attempt downloading (e.g., tokenizer configs).
    /// </summary>
    public string[] OptionalFiles { get; init; } = [];

    /// <summary>
    /// The ONNX GenAI model type for loading.
    /// </summary>
    public required OnnxModelType ModelType { get; init; }

    /// <summary>
    /// Chat template format (determines how messages are formatted).
    /// </summary>
    public required ChatTemplateFormat ChatTemplate { get; init; }

    /// <summary>Approximate model size category.</summary>
    public ModelTier Tier { get; init; } = ModelTier.Small;

    /// <summary>Whether this model has native ONNX weights on HuggingFace.</summary>
    public bool HasNativeOnnx { get; init; }

    /// <summary>
    /// Subdirectory within the downloaded files that contains the model.
    /// When set, the path returned by EnsureModelAsync points to this subdirectory.
    /// Used for repos that store multiple variants (e.g., cpu, cuda) in separate folders.
    /// </summary>
    public string? ModelSubPath { get; init; }

    /// <summary>
    /// Whether this model supports tool/function calling.
    /// Models that support tool calling can handle AITool/AIFunction in ChatOptions.
    /// </summary>
    public bool SupportsToolCalling { get; init; }
}
