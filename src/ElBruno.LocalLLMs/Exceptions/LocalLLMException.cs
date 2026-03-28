namespace ElBruno.LocalLLMs;

/// <summary>
/// Base exception for all ElBruno.LocalLLMs errors.
/// </summary>
public abstract class LocalLLMException : Exception
{
    /// <inheritdoc />
    protected LocalLLMException(string message) : base(message) { }

    /// <inheritdoc />
    protected LocalLLMException(string message, Exception innerException) : base(message, innerException) { }
}
