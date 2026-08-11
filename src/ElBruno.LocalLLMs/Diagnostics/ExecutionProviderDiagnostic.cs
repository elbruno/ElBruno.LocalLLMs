namespace ElBruno.LocalLLMs.Diagnostics;

/// <summary>
/// Availability status for an execution-provider diagnostic.
/// </summary>
public enum ExecutionProviderDiagnosticStatus
{
    /// <summary>The provider could not be confirmed from diagnostics alone.</summary>
    Unknown = 0,

    /// <summary>The provider was confirmed available.</summary>
    Available = 1,

    /// <summary>The provider was confirmed unavailable.</summary>
    Unavailable = 2
}

/// <summary>
/// Readiness information for a single execution provider.
/// </summary>
public sealed record ExecutionProviderDiagnostic
{
    /// <summary>The execution provider being described.</summary>
    public ExecutionProvider Provider { get; init; }

    /// <summary>Whether the provider passed the current environment preflight checks.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Detailed readiness status. Defaults to <see cref="ExecutionProviderDiagnosticStatus.Unavailable"/>
    /// so older callers that only populate <see cref="IsAvailable"/> keep the previous semantics.
    /// </summary>
    public ExecutionProviderDiagnosticStatus Status { get; init; } = ExecutionProviderDiagnosticStatus.Unavailable;

    /// <summary>
    /// A short reason when the provider is unavailable or unknown.
    /// Null when the provider is available.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Optional actionable suggestion for resolving an unavailable provider.
    /// </summary>
    public string? Suggestion { get; init; }
}
