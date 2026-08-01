using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.BlazorComponents.Services;

/// <summary>
/// Snapshot of a model's current download/cache status.
/// </summary>
public sealed record ModelStatus
{
    /// <summary>The model definition.</summary>
    public required ModelDefinition Model { get; init; }

    /// <summary>Current lifecycle state.</summary>
    public ModelDownloadState State { get; init; } = ModelDownloadState.NotDownloaded;

    /// <summary>Download progress (only meaningful when State is Downloading).</summary>
    public ModelDownloadProgress? Progress { get; init; }

    /// <summary>Error message when State is Error.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Approximate size of the downloaded model in bytes.
    /// 0 when not downloaded or size could not be determined.
    /// </summary>
    public long CachedSizeBytes { get; init; }

    /// <summary>Full path to the local model directory. Null when not downloaded.</summary>
    public string? LocalPath { get; init; }
}
