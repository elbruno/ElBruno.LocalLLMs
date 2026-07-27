namespace ElBruno.LocalLLMs;

/// <summary>
/// Downloads and caches ONNX models from HuggingFace.
/// Uses ElBruno.HuggingFace.Downloader internally.
/// </summary>
public interface IModelDownloader
{
    /// <summary>
    /// Ensures the model files are available locally. Downloads from HuggingFace if needed.
    /// Returns the local directory path containing the model files.
    /// </summary>
    Task<string> EnsureModelAsync(
        ModelDefinition model,
        string? cacheDirectory = null,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default cache directory for model storage.
    /// </summary>
    string GetCacheDirectory();

    /// <summary>
    /// Removes all cached files for the specified model.
    /// No-op if the model is not cached.
    /// </summary>
    Task DeleteModelAsync(
        ModelDefinition model,
        string? cacheDirectory = null,
        CancellationToken cancellationToken = default);
}
