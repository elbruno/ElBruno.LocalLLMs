namespace ElBruno.LocalLLMs;

/// <summary>
/// Reports model download progress.
/// </summary>
public readonly record struct ModelDownloadProgress(
    string FileName,
    long BytesDownloaded,
    long TotalBytes,
    double PercentComplete);
