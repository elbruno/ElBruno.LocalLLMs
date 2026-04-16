namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Exception thrown when BitNet inference fails.
/// </summary>
public sealed class BitNetInferenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitNetInferenceException"/> class.
    /// </summary>
    public BitNetInferenceException(string message)
        : base(message)
    {
    }
}
