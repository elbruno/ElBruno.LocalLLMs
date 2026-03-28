namespace ElBruno.LocalLLMs;

/// <summary>
/// Thrown when an execution provider fails to initialize or is not available.
/// </summary>
public sealed class ExecutionProviderException : LocalLLMException
{
    /// <summary>The provider that failed.</summary>
    public ExecutionProvider Provider { get; }

    /// <summary>Optional actionable suggestion for the caller.</summary>
    public string? Suggestion { get; }

    /// <inheritdoc />
    public ExecutionProviderException(string message, ExecutionProvider provider, Exception? innerException = null)
        : base(message, innerException ?? new Exception())
    {
        Provider = provider;
    }

    /// <inheritdoc />
    public ExecutionProviderException(string message, ExecutionProvider provider, string? suggestion, Exception? innerException = null)
        : base(message, innerException ?? new Exception())
    {
        Provider = provider;
        Suggestion = suggestion;
    }
}
