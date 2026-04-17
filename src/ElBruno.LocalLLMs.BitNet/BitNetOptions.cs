using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Configuration options for BitNetChatClient.
/// </summary>
public sealed class BitNetOptions
{
    /// <summary>
    /// The model definition from the BitNet catalog.
    /// Default: BitNetKnownModels.BitNet2B4T.
    /// </summary>
    public BitNetModelDefinition Model { get; set; } = BitNetKnownModels.BitNet2B4T;

    /// <summary>
    /// Path to the GGUF model file.
    /// When set, skips automatic download. When null, the model is
    /// automatically downloaded from HuggingFace (if <see cref="EnsureModelDownloaded"/> is true).
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Path to the directory containing the bitnet.cpp native library
    /// (llama.dll / libllama.so / libllama.dylib).
    /// If null, searches PATH / LD_LIBRARY_PATH / default locations.
    /// </summary>
    public string? NativeLibraryPath { get; set; }

    /// <summary>
    /// Custom directory for model cache.
    /// Default: %LOCALAPPDATA%/ElBruno/LocalLLMs/models (same as ONNX models).
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Whether to auto-download the GGUF model from HuggingFace if not cached. Default: true.
    /// Only used when <see cref="ModelPath"/> is null.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Maximum tokens to generate. Default: 2048.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for sampling. Default: 0.7.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Top-p nucleus sampling. Default: 0.9.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    /// Top-k sampling. Default: 40.
    /// </summary>
    public int TopK { get; set; } = 40;

    /// <summary>
    /// Repetition penalty. Default: 1.1.
    /// </summary>
    public float RepetitionPenalty { get; set; } = 1.1f;

    /// <summary>
    /// Number of CPU threads for inference.
    /// Default: Environment.ProcessorCount.
    /// </summary>
    public int ThreadCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Context window size in tokens. Default: 4096.
    /// </summary>
    public int ContextSize { get; set; } = 4096;

    /// <summary>
    /// Optional system prompt prepended to conversations.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Chat template format for prompt formatting.
    /// Default is resolved from the model definition.
    /// </summary>
    public ChatTemplateFormat? ChatTemplateOverride { get; set; }
}
