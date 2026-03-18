namespace ElBruno.LocalLLMs;

/// <summary>
/// ONNX model loading strategy.
/// </summary>
public enum OnnxModelType
{
    /// <summary>Standard causal language model (decoder-only).</summary>
    CausalLM,

    /// <summary>ONNX Runtime GenAI model (uses GenAI API directly).</summary>
    GenAI
}
