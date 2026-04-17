using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Downloads and caches BitNet GGUF models from HuggingFace using ElBruno.HuggingFace.Downloader.
/// </summary>
internal sealed class BitNetModelDownloader
{
    private readonly HuggingFaceDownloader _downloader;
    private readonly string _defaultCacheDirectory;

    public BitNetModelDownloader()
        : this(new HuggingFaceDownloader())
    {
    }

    internal BitNetModelDownloader(HuggingFaceDownloader downloader)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _defaultCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalLLMs", "models");
    }

    /// <summary>
    /// Gets the default cache directory for model storage.
    /// </summary>
    public string GetCacheDirectory() => _defaultCacheDirectory;

    /// <summary>
    /// Ensures the GGUF model file is available locally. Downloads from HuggingFace if needed.
    /// Returns the full path to the GGUF model file.
    /// </summary>
    public async Task<string> EnsureModelAsync(
        BitNetModelDefinition model,
        string? cacheDirectory = null,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var cacheDir = cacheDirectory ?? _defaultCacheDirectory;
        var modelDir = Path.Combine(cacheDir, SanitizeModelId(model.Id));
        var ggufPath = Path.Combine(modelDir, model.GgufFileName);

        if (File.Exists(ggufPath))
        {
            return ggufPath;
        }

        Directory.CreateDirectory(modelDir);

        IProgress<DownloadProgress>? hfProgress = null;
        if (progress is not null)
        {
            hfProgress = new Progress<DownloadProgress>(p =>
            {
                progress.Report(new ModelDownloadProgress(
                    FileName: p.CurrentFile ?? string.Empty,
                    BytesDownloaded: p.BytesDownloaded,
                    TotalBytes: p.TotalBytes,
                    PercentComplete: p.PercentComplete));
            });
        }

        var request = new DownloadRequest
        {
            RepoId = model.HuggingFaceRepoId,
            LocalDirectory = modelDir,
            RequiredFiles = [model.GgufFileName],
            Progress = hfProgress
        };

        await _downloader.DownloadFilesAsync(request, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(ggufPath))
        {
            throw new BitNetInferenceException(
                $"Download completed but GGUF file not found at '{ggufPath}'. " +
                $"The repository '{model.HuggingFaceRepoId}' may not contain '{model.GgufFileName}'.");
        }

        return ggufPath;
    }

    private static string SanitizeModelId(string modelId) =>
        modelId.Replace('/', '-').Replace('\\', '-');
}
