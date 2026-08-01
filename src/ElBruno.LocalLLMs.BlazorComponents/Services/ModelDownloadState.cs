namespace ElBruno.LocalLLMs.BlazorComponents.Services;

/// <summary>
/// Represents the lifecycle state of a local model.
/// </summary>
public enum ModelDownloadState
{
    /// <summary>Model has not been downloaded to local cache.</summary>
    NotDownloaded,

    /// <summary>Model is currently being downloaded.</summary>
    Downloading,

    /// <summary>Model is cached locally and ready to load.</summary>
    Downloaded,

    /// <summary>Model download or deletion failed with an error.</summary>
    Error
}
