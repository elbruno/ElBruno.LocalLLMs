using System.Net.Http.Headers;
using System.Text.Json;
using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Downloads and caches ONNX models from HuggingFace using ElBruno.HuggingFace.Downloader.
/// Resolves glob patterns in RequiredFiles before downloading.
/// </summary>
internal sealed class ModelDownloader : IModelDownloader
{
    private readonly HuggingFaceDownloader _downloader;
    private readonly string _defaultCacheDirectory;
    private static readonly Lazy<HttpClient> s_apiClient = new(CreateApiClient);

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

        // The actual model path may be a subdirectory for repos with multiple variants
        var modelPath = model.ModelSubPath is not null
            ? Path.Combine(modelDir, model.ModelSubPath.Replace('/', Path.DirectorySeparatorChar))
            : modelDir;

        // Check if already cached
        if (IsModelCached(model, modelDir, modelPath))
        {
            return modelPath;
        }

        Directory.CreateDirectory(modelDir);

        // Resolve glob patterns (e.g., "*" or "prefix/*") to actual file paths
        var resolvedRequired = await ResolveGlobPatternsAsync(
            model.HuggingFaceRepoId, model.RequiredFiles, cancellationToken).ConfigureAwait(false);

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
            RequiredFiles = resolvedRequired,
            OptionalFiles = model.OptionalFiles.Length > 0 ? model.OptionalFiles : null,
            Progress = hfProgress
        };

        await _downloader.DownloadFilesAsync(request, cancellationToken).ConfigureAwait(false);

        return modelPath;
    }

    /// <summary>
    /// Checks whether the model is already cached locally.
    /// For glob patterns, checks for genai_config.json in the model directory.
    /// For exact paths, checks each file individually.
    /// </summary>
    private static bool IsModelCached(ModelDefinition model, string modelDir, string modelPath)
    {
        if (!Directory.Exists(modelDir))
            return false;

        bool hasGlobs = Array.Exists(model.RequiredFiles, f => f.Contains('*'));
        if (hasGlobs)
        {
            // For glob patterns, check if the target model directory has genai_config.json
            return File.Exists(Path.Combine(modelPath, "genai_config.json"));
        }

        // For exact paths, check each required file
        return model.RequiredFiles.All(f =>
            File.Exists(Path.Combine(modelDir, f.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    /// Resolves glob patterns in RequiredFiles to actual file paths via the HuggingFace API.
    /// </summary>
    private static async Task<string[]> ResolveGlobPatternsAsync(
        string repoId, string[] patterns, CancellationToken cancellationToken)
    {
        bool hasGlobs = Array.Exists(patterns, p => p.Contains('*'));
        if (!hasGlobs)
            return patterns;

        // Fetch complete file list from the HuggingFace API
        var allFiles = await ListRepoFilesAsync(repoId, cancellationToken).ConfigureAwait(false);

        var resolved = new List<string>();
        foreach (var pattern in patterns)
        {
            if (!pattern.Contains('*'))
            {
                resolved.Add(pattern);
                continue;
            }

            if (pattern == "*")
            {
                // All files — exclude hidden/git metadata files
                resolved.AddRange(allFiles.Where(f => !f.StartsWith('.') && f != ".gitattributes"));
            }
            else if (pattern.EndsWith("/*", StringComparison.Ordinal))
            {
                // Directory glob — match all files under the prefix
                var prefix = pattern[..^1]; // "dir/subdir/*" → "dir/subdir/"
                resolved.AddRange(allFiles.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)));
            }
            else
            {
                resolved.Add(pattern);
            }
        }

        if (resolved.Count == 0)
        {
            throw new InvalidOperationException(
                $"No files matched the required patterns [{string.Join(", ", patterns)}] in repo '{repoId}'. " +
                "The repository may be empty or the patterns may not match any files.");
        }

        return resolved.ToArray();
    }

    /// <summary>
    /// Lists all files in a HuggingFace repository using the models API.
    /// </summary>
    private static async Task<string[]> ListRepoFilesAsync(
        string repoId, CancellationToken cancellationToken)
    {
        var url = $"https://huggingface.co/api/models/{repoId}";
        using var response = await s_apiClient.Value.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Model '{repoId}' was not found on HuggingFace (HTTP {(int)response.StatusCode}). " +
                "The model may not be published yet, the repository may be private, or the repo ID may be incorrect. " +
                "If the repo is private, set the HF_TOKEN environment variable.");
        }

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("siblings", out var siblings))
            return [];

        return siblings.EnumerateArray()
            .Select(s => s.GetProperty("rfilename").GetString()!)
            .Where(f => !string.IsNullOrEmpty(f))
            .ToArray();
    }

    private static HttpClient CreateApiClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ElBruno.LocalLLMs/1.0");

        var token = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static string SanitizeModelId(string modelId) =>
        modelId.Replace('/', '-').Replace('\\', '-');
}
