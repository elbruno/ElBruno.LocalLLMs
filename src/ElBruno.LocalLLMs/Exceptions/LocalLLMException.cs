namespace ElBruno.LocalLLMs;

/// <summary>
/// Base exception for all ElBruno.LocalLLMs errors.
/// </summary>
public abstract class LocalLLMException : Exception
{
    /// <summary>
    /// Additional context about the error (e.g., model path, provider, configuration).
    /// </summary>
    public IDictionary<string, object?> Context { get; } = new Dictionary<string, object?>();

    /// <inheritdoc />
    protected LocalLLMException(string message) : base(message) { }

    /// <inheritdoc />
    protected LocalLLMException(string message, Exception innerException) : base(message, innerException) { }
}
