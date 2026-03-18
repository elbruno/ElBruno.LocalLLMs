using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Downloads and caches ONNX models from HuggingFace using ElBruno.HuggingFace.Downloader.
/// </summary>
internal sealed class ModelDownloader : IModelDownloader
{
    private readonly HuggingFaceDownloader _downloader;
    private readonly string _defaultCacheDirectory;

    public ModelDownloader()
        : this(new HuggingFaceDownloader())
    {
    }

    internal ModelDownloader(HuggingFaceDownloader downloader)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _defaultCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalLLMs", "models");
    }

    public string GetCacheDirectory() => _defaultCacheDirectory;

    public async Task<string> EnsureModelAsync(
        ModelDefinition model,
        string? cacheDirectory = null,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var cacheDir = cacheDirectory ?? _defaultCacheDirectory;
        var modelDir = Path.Combine(cacheDir, SanitizeModelId(model.Id));

        // Check if already cached — all required files present
        if (_downloader.AreFilesAvailable(model.RequiredFiles, modelDir))
        {
            return modelDir;
        }

        Directory.CreateDirectory(modelDir);

        // Map our progress type to the HuggingFace progress type
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
            RequiredFiles = model.RequiredFiles,
            OptionalFiles = model.OptionalFiles.Length > 0 ? model.OptionalFiles : null,
            Progress = hfProgress
        };

        await _downloader.DownloadFilesAsync(request, cancellationToken).ConfigureAwait(false);

        return modelDir;
    }

    private static string SanitizeModelId(string modelId) =>
        modelId.Replace('/', '-').Replace('\\', '-');
}
