namespace ElBruno.LocalLLMs;

/// <summary>
/// Thrown when the requested model cannot be found or loaded.
/// </summary>
public sealed class ModelNotAvailableException : LocalLLMException
{
    /// <summary>Path to the model that was not found, if known.</summary>
    public string? ModelPath { get; }

    /// <inheritdoc />
    public ModelNotAvailableException(string message, string? modelPath = null, Exception? innerException = null)
        : base(message, innerException ?? new Exception())
    {
        ModelPath = modelPath;
    }
}
