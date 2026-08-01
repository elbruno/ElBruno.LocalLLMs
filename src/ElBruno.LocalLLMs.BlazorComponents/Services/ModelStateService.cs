using System.Collections.Concurrent;
using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.BlazorComponents.Services;

/// <summary>
/// Scoped service that tracks download and cache state for all models.
/// Shared across all Blazor components in the same scope so a ModelStatusCard
/// and a LocalLLMHealthBadge both reflect the same reality.
/// </summary>
public sealed class ModelStateService : IAsyncDisposable
{
    private readonly IModelDownloader _downloader;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, ModelStatus> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _downloadCts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fires whenever any model's status changes. Subscribe for UI refresh.</summary>
    public event Action? OnStateChanged;

    /// <summary>Creates a new <see cref="ModelStateService"/> backed by the given downloader.</summary>
    public ModelStateService(IModelDownloader downloader)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _cacheDirectory = downloader.GetCacheDirectory();
    }

    // ── Public read API ───────────────────────────────────────────────────────

    /// <summary>Returns the current status for the given model, computing it if not yet cached.</summary>
    public ModelStatus GetStatus(ModelDefinition model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _states.GetOrAdd(model.Id, _ => ComputeStatus(model));
    }

    /// <summary>
    /// Refreshes the cached status for the given model by re-checking the filesystem.
    /// Call this after an external change (e.g., manual file deletion).
    /// </summary>
    public ModelStatus RefreshStatus(ModelDefinition model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var status = ComputeStatus(model);
        _states[model.Id] = status;
        return status;
    }

    // ── Download API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Starts downloading the model asynchronously, reporting progress through state updates.
    /// No-op if the model is already downloading or downloaded.
    /// </summary>
    public Task StartDownloadAsync(ModelDefinition model, CancellationToken callerToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var current = GetStatus(model);
        if (current.State is ModelDownloadState.Downloading or ModelDownloadState.Downloaded)
            return Task.CompletedTask;

        return DoDownloadAsync(model, callerToken);
    }

    /// <summary>Cancels an in-progress download for the given model.</summary>
    public void CancelDownload(ModelDefinition model)
    {
        if (_downloadCts.TryGetValue(model.Id, out var cts))
            cts.Cancel();
    }

    /// <summary>
    /// Deletes the model's local cache directory.
    /// </summary>
    public async Task DeleteModelAsync(ModelDefinition model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        CancelDownload(model);

        try
        {
            await _downloader.DeleteModelAsync(model, _cacheDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort; model directory may not exist
        }

        var refreshed = ComputeStatus(model);
        _states[model.Id] = refreshed;
        NotifyStateChanged();
    }

    // ── Cache directory helpers ───────────────────────────────────────────────

    /// <summary>Returns the default cache directory used by this service.</summary>
    public string CacheDirectory => _cacheDirectory;

    /// <summary>
    /// Returns the directory that would contain the model's files (may not exist yet).
    /// </summary>
    public string GetModelDirectory(ModelDefinition model)
    {
        var sanitized = SanitizePath(model.Id);
        return Path.Combine(_cacheDirectory, sanitized);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task DoDownloadAsync(ModelDefinition model, CancellationToken callerToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        _downloadCts[model.Id] = cts;

        SetState(model.Id, new ModelStatus
        {
            Model = model,
            State = ModelDownloadState.Downloading,
            Progress = new ModelDownloadProgress(string.Empty, 0, 0, 0)
        });

        try
        {
            var progress = new Progress<ModelDownloadProgress>(p =>
            {
                SetState(model.Id, new ModelStatus
                {
                    Model = model,
                    State = ModelDownloadState.Downloading,
                    Progress = p
                });
            });

            var path = await _downloader.EnsureModelAsync(
                model, _cacheDirectory, progress, cts.Token).ConfigureAwait(false);

            var size = ComputeDirectorySize(path);
            SetState(model.Id, new ModelStatus
            {
                Model = model,
                State = ModelDownloadState.Downloaded,
                LocalPath = path,
                CachedSizeBytes = size
            });
        }
        catch (OperationCanceledException)
        {
            // User cancelled — revert to NotDownloaded so card resets
            SetState(model.Id, ComputeStatus(model));
        }
        catch (Exception ex)
        {
            SetState(model.Id, new ModelStatus
            {
                Model = model,
                State = ModelDownloadState.Error,
                ErrorMessage = ex.Message
            });
        }
        finally
        {
            _downloadCts.TryRemove(model.Id, out _);
        }
    }

    private ModelStatus ComputeStatus(ModelDefinition model)
    {
        var modelDir = GetModelDirectory(model);
        var modelPath = model.ModelSubPath is not null
            ? Path.Combine(modelDir, model.ModelSubPath.Replace('/', Path.DirectorySeparatorChar))
            : modelDir;

        if (!Directory.Exists(modelPath))
            return new ModelStatus { Model = model, State = ModelDownloadState.NotDownloaded };

        // Check for genai_config.json as proof of a complete download
        bool hasConfig = File.Exists(Path.Combine(modelPath, "genai_config.json"));
        bool hasOnnx = Directory.EnumerateFiles(modelPath, "*.onnx", SearchOption.AllDirectories).Any();

        if (!hasConfig && !hasOnnx)
            return new ModelStatus { Model = model, State = ModelDownloadState.NotDownloaded };

        var size = ComputeDirectorySize(modelPath);
        return new ModelStatus
        {
            Model = model,
            State = ModelDownloadState.Downloaded,
            LocalPath = modelPath,
            CachedSizeBytes = size
        };
    }

    private static long ComputeDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch { return 0; }
    }

    private void SetState(string modelId, ModelStatus status)
    {
        _states[modelId] = status;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    /// <summary>Simple filesystem-safe normalisation matching what the downloader does.</summary>
    private static string SanitizePath(string modelId) =>
        string.Concat(modelId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Cancels any active downloads and frees managed resources.</summary>
    public ValueTask DisposeAsync()
    {
        foreach (var cts in _downloadCts.Values)
        {
            try { cts.Cancel(); cts.Dispose(); } catch { /* ignore */ }
        }
        _downloadCts.Clear();
        return ValueTask.CompletedTask;
    }
}
