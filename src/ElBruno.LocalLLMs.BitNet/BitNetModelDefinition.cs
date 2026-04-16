using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Describes a BitNet model — its source, format, and prompt template.
/// Parallel to ModelDefinition but for GGUF/bitnet.cpp models.
/// </summary>
public sealed record BitNetModelDefinition
{
    /// <summary>Unique identifier (e.g., "bitnet-b1.58-2b-4t").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>HuggingFace repository ID for GGUF download.</summary>
    public required string HuggingFaceRepoId { get; init; }

    /// <summary>
    /// Default GGUF filename within the repo (e.g., "ggml-model-i2_s.gguf").
    /// </summary>
    public required string GgufFileName { get; init; }

    /// <summary>
    /// Chat template format (ChatML, Llama3, etc.).
    /// Reuses the shared ChatTemplateFormat enum.
    /// </summary>
    public required ChatTemplateFormat ChatTemplate { get; init; }

    /// <summary>
    /// Parameter count in billions (e.g., 0.7, 2.4, 3.3, 8.0).
    /// </summary>
    public required double ParametersBillions { get; init; }

    /// <summary>
    /// Context window size supported by this model.
    /// </summary>
    public int ContextLength { get; init; } = 4096;

    /// <summary>
    /// Approximate model file size in MB.
    /// </summary>
    public int ApproximateSizeMB { get; init; }

    /// <summary>
    /// Recommended BitNet kernel type for optimal performance.
    /// </summary>
    public BitNetKernelType RecommendedKernel { get; init; } = BitNetKernelType.I2_S;
}
