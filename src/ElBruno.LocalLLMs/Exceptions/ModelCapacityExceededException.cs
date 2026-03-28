namespace ElBruno.LocalLLMs;

/// <summary>
/// Thrown when the input exceeds the model's maximum token capacity.
/// </summary>
public sealed class ModelCapacityExceededException : LocalLLMException
{
    /// <summary>Number of tokens in the input that caused the error.</summary>
    public int InputTokenCount { get; }

    /// <summary>Maximum tokens the model supports.</summary>
    public int MaxTokens { get; }

    /// <inheritdoc />
    public ModelCapacityExceededException(string message, int inputTokenCount, int maxTokens, Exception? innerException = null)
        : base(message, innerException ?? new Exception())
    {
        InputTokenCount = inputTokenCount;
        MaxTokens = maxTokens;
    }
}
