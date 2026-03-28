namespace ElBruno.LocalLLMs.Progress;

/// <summary>
/// Reports progress during inference (token generation).
/// </summary>
public sealed record InferenceProgressUpdate
{
    /// <summary>Token index (0-based) in the current generation.</summary>
    public int TokenIndex { get; init; }

    /// <summary>The generated token text.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Total tokens generated so far.</summary>
    public int TotalTokens { get; init; }

    /// <summary>Elapsed time since inference started.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Tokens per second throughput.</summary>
    public double TokensPerSecond => TotalTokens > 0 && Elapsed.TotalSeconds > 0
        ? TotalTokens / Elapsed.TotalSeconds
        : 0;
}
