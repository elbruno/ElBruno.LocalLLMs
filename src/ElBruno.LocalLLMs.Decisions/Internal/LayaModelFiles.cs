using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// Resolves the four files a decision model needs, downloading them from HuggingFace on first use.
/// </summary>
/// <param name="ModelPath">The ONNX graph.</param>
/// <param name="ConfigPath">The checkpoint settings, including fitted temperatures.</param>
/// <param name="TokenizerPath">The tokenizer definition.</param>
internal sealed record LayaModelFiles(string ModelPath, string ConfigPath, string TokenizerPath)
{
    private static readonly SemaphoreSlim s_downloadLock = new(1, 1);

    /// <summary>
    /// Ensures the model files exist locally and returns their paths.
    /// </summary>
    /// <param name="options">The configured model source and cache location.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>The resolved file paths.</returns>
    public static async Task<LayaModelFiles> EnsureAsync(
        DecisionOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            return Locate(options.ModelPath!, mustExist: true);
        }

        var directory = Path.Combine(options.ResolveCacheDirectory(), Sanitize(options.ModelRepository));
        var candidate = TryLocate(directory);
        if (candidate is not null)
        {
            return candidate;
        }

        await s_downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have completed the download while this one waited.
            candidate = TryLocate(directory);
            if (candidate is not null)
            {
                return candidate;
            }

            Directory.CreateDirectory(directory);

            var downloader = new HuggingFaceDownloader();
            await downloader.DownloadFilesAsync(
                new DownloadRequest
                {
                    RepoId = options.ModelRepository,
                    LocalDirectory = directory,
                    RequiredFiles = options.ModelFiles.ToArray(),
                    OptionalFiles = options.OptionalModelFiles.ToArray(),
                },
                cancellationToken).ConfigureAwait(false);

            return Locate(directory, mustExist: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DecisionException)
        {
            throw new DecisionException(
                $"The decision model '{options.ModelRepository}' could not be downloaded. " +
                "Check network access, or set DecisionOptions.ModelPath to a directory you have " +
                "already populated.",
                ex);
        }
        finally
        {
            s_downloadLock.Release();
        }
    }

    private static LayaModelFiles? TryLocate(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            return Locate(directory, mustExist: false);
        }
        catch (DecisionException)
        {
            return null;
        }
    }

    private static LayaModelFiles Locate(string directory, bool mustExist)
    {
        if (!Directory.Exists(directory))
        {
            throw new DecisionException($"The model directory '{directory}' does not exist.");
        }

        var model = FindFirst(directory, "model.onnx", "laya.onnx", "onnx/model.onnx");
        var config = FindFirst(directory, "rl_agent_config.json", "config.json");
        var tokenizer = FindFirst(directory, "tokenizer/tokenizer.json", "tokenizer.json");

        if (model is null || config is null || tokenizer is null)
        {
            var missing = new List<string>();
            if (model is null) missing.Add("the ONNX graph (model.onnx)");
            if (config is null) missing.Add("the checkpoint config (rl_agent_config.json or config.json)");
            if (tokenizer is null) missing.Add("the tokenizer (tokenizer.json)");

            throw new DecisionException(
                $"The model directory '{directory}' is missing {string.Join(", ", missing)}." +
                (mustExist ? " The download may have been interrupted; delete the directory and retry." : string.Empty));
        }

        // A graph that uses external data is unusable without it, and the failure surfaces much
        // later as an opaque ONNX Runtime error, so check for it while the context is still clear.
        var externalData = model + ".data";
        if (mustExist && new FileInfo(model).Length < 50L * 1024 * 1024 && !File.Exists(externalData))
        {
            var siblings = Directory.EnumerateFiles(Path.GetDirectoryName(model)!, "*.onnx*data*").ToArray();
            if (siblings.Length == 0)
            {
                throw new DecisionException(
                    $"The ONNX graph at '{model}' is too small to contain weights and no companion " +
                    "'.onnx.data' file sits beside it. The download was probably interrupted; " +
                    "delete the directory and retry.");
            }
        }

        return new LayaModelFiles(model, config, tokenizer);
    }

    private static string? FindFirst(string directory, params string[] relativePaths)
    {
        foreach (var relative in relativePaths)
        {
            var full = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    private static string Sanitize(string repository)
    {
        var characters = repository.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), characters[i]) >= 0)
            {
                characters[i] = '_';
            }
        }

        return new string(characters);
    }
}
