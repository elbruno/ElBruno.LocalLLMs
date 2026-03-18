namespace ElBruno.LocalLLMs;

/// <summary>
/// Configuration options for LocalChatClient.
/// </summary>
public sealed class LocalLLMsOptions
{
    /// <summary>
    /// The model to use. Provides HuggingFace repo, ONNX paths, and chat template.
    /// Default: KnownModels.Phi35MiniInstruct.
    /// </summary>
    public ModelDefinition Model { get; set; } = KnownModels.Phi35MiniInstruct;

    /// <summary>
    /// Path to a local model directory. When set, skips download entirely.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Custom directory for model cache.
    /// Default: %LOCALAPPDATA%/ElBruno/LocalLLMs/models
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Whether to auto-download the model if not cached. Default: true.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Execution provider selection. Default: CPU.
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; set; } = ExecutionProvider.Cpu;

    /// <summary>
    /// GPU device ID for CUDA/DirectML. Default: 0.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;

    /// <summary>
    /// Maximum sequence length for generation. Default: 2048.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 2048;

    /// <summary>
    /// Default temperature for generation. Default: 0.7.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Default top-p for generation. Default: 0.9.
    /// </summary>
    public float TopP { get; set; } = 0.9f;
}
