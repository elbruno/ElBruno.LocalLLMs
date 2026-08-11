namespace ElBruno.LocalLLMs;

/// <summary>
/// Selects the hardware execution provider for ONNX Runtime.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>Automatic selection: DirectML, then CUDA, then CPU fallback on Windows; CUDA, then CPU on Linux.</summary>
    Auto,

    /// <summary>CPU execution (default, works everywhere).</summary>
    Cpu,

    /// <summary>NVIDIA CUDA GPU acceleration.</summary>
    Cuda,

    /// <summary>Windows DirectML GPU acceleration (AMD, Intel, NVIDIA).</summary>
    DirectML
}

