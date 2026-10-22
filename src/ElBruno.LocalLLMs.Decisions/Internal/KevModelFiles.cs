using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs.Decisions.Internal;

internal static class KevModelFiles
{
    internal static readonly string[] RuntimeFiles = ["decision.onnx", "model.onnx.data", "tokenizer.json", "kev_config.json"];
    internal static readonly string[] PublishedFiles =
        ["decision.onnx", "model.onnx.data", "tokenizer.json", "Kev-LICENSE", "Qwen-LICENSE", "Kev-model-card.md"];
    private const string ReceiptFile = ".kev-complete.json";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_locks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static Task<string> EnsureAsync(KevDecisionOptions options, CancellationToken cancellationToken) =>
        EnsureAsync(options, (request, token) => new HuggingFaceDownloader().DownloadFilesAsync(request, token), cancellationToken);

    internal static async Task<string> EnsureAsync(KevDecisionOptions options,
        Func<DownloadRequest, CancellationToken, Task> download, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = options.ResolveModelDirectory();
        if (options.ModelPath is not null)
        {
            RequireFiles(directory, RuntimeFiles);
            return directory;
        }
        var gate = s_locks.GetOrAdd(directory, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            await using var processLock = await LockDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
            if (IsComplete(directory, options)) return directory;
            File.Delete(Path.Combine(directory, ReceiptFile));
            // An uncommitted cache is never accepted based on the small graph or manifest alone.
            var manifestPath = Path.Combine(directory, "kev_config.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    using var existing = ReadManifest(manifestPath);
                }
                catch (Exception ex) when (ex is JsonException or DecisionException or KeyNotFoundException or InvalidOperationException)
                {
                    File.Delete(manifestPath);
                }
            }
            await DownloadMissingAsync(["kev_config.json"]).ConfigureAwait(false);
            using var manifest = ReadManifest(manifestPath);
            var hashes = manifest.RootElement.GetProperty("files");
            var missing = new List<string>();
            foreach (var name in PublishedFiles)
            {
                var expected = hashes.GetProperty(name).GetString();
                if (expected is null || expected.Length != 64 || expected.Any(character => !Uri.IsHexDigit(character)))
                    throw new DecisionException($"The Kev manifest has no valid SHA-256 for '{name}'.");
                var path = Path.Combine(directory, name);
                if (File.Exists(path) && !await MatchesAsync(path, expected, cancellationToken).ConfigureAwait(false))
                    File.Delete(path);
                if (!File.Exists(path)) missing.Add(name);
            }
            await DownloadMissingAsync(missing).ConfigureAwait(false);
            foreach (var name in missing)
            {
                var path = Path.Combine(directory, name);
                if (!File.Exists(path) || !await MatchesAsync(path, hashes.GetProperty(name).GetString()!, cancellationToken).ConfigureAwait(false))
                {
                    File.Delete(path);
                    throw new DecisionException($"The downloaded Kev file '{name}' failed SHA-256 validation. Retry the download.");
                }
            }
            RequireFiles(directory, PublishedFiles);
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = new CacheReceipt(options.ModelRepository, options.ModelRevision,
                PublishedFiles.Append("kev_config.json").ToDictionary(name => name, name => Stamp(Path.Combine(directory, name))));
            var temporary = Path.Combine(directory, ReceiptFile + ".tmp");
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(receipt), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, Path.Combine(directory, ReceiptFile), overwrite: true);
            return directory;

            Task DownloadMissingAsync(IReadOnlyList<string> names)
            {
                var absent = names.Where(name => !File.Exists(Path.Combine(directory, name))).ToArray();
                return absent.Length == 0 ? Task.CompletedTask : download(new DownloadRequest
                {
                    RepoId = options.ModelRepository,
                    Revision = options.ModelRevision,
                    ExpectedCommitSha = options.ModelRevision,
                    LocalDirectory = directory,
                    RequiredFiles = absent,
                    UseAtomicWrites = true,
                    ResumePartialDownloads = true,
                    Progress = options.DownloadProgress,
                }, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DecisionException)
        {
            throw new DecisionException($"The Kev model '{options.ModelRepository}@{options.ModelRevision}' could not be acquired " +
                $"in '{directory}'. Check network access and free disk space, retry, or set KevDecisionOptions.ModelPath to a complete local export.", ex);
        }
        finally { gate.Release(); }
    }

    private static bool IsComplete(string directory, KevDecisionOptions options)
    {
        var path = Path.Combine(directory, ReceiptFile);
        if (!File.Exists(path)) return false;
        try
        {
            var receipt = JsonSerializer.Deserialize<CacheReceipt>(File.ReadAllText(path));
            return receipt is not null && receipt.Files is not null &&
                receipt.Repository == options.ModelRepository && receipt.Revision == options.ModelRevision &&
                PublishedFiles.Append("kev_config.json").All(name =>
                    receipt.Files.TryGetValue(name, out var expected) && File.Exists(Path.Combine(directory, name)) &&
                    expected is not null && expected.Length > 0 && expected == Stamp(Path.Combine(directory, name)));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return false; // The receipt is only an optimization; revalidate every asset below.
        }
    }

    internal static JsonDocument ReadManifest(string path)
    {
        var document = JsonDocument.Parse(File.ReadAllText(path));
        try
        {
            var root = document.RootElement;
            if (root.GetProperty("format").GetString() != "kev-0.8b-prefill-v1" ||
                root.GetProperty("checkpoint_revision").GetString() != "bf75a6a8848ea6960ff2ed108d9ed44c2941174f" ||
                root.GetProperty("base_revision").GetString() != "dc7cdfe2ee4154fa7e30f5b51ca41bfa40174e68" ||
                root.GetProperty("max_tokens").GetInt32() != 8192)
                throw new DecisionException("Unsupported Kev export. Use the pinned Kev 0.8B CPU FP32 conversion.");
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void RequireFiles(string directory, IEnumerable<string> files)
    {
        var missing = files.Where(name => !File.Exists(Path.Combine(directory, name)) || new FileInfo(Path.Combine(directory, name)).Length == 0).ToArray();
        if (missing.Length != 0)
            throw new DecisionException($"The Kev export at '{directory}' is missing {string.Join(", ", missing)}. " +
                "Supply a complete local export, or omit ModelPath to enable automatic download. Advanced exports use scripts/convert_kev.py.");
    }

    private static async Task<bool> MatchesAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static FileStamp Stamp(string path)
    {
        var info = new FileInfo(path);
        return new FileStamp(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private static async Task<FileStream> LockDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(Path.Combine(directory, ".kev-download.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 11 or 32 or 33)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed record FileStamp(long Length, long LastWriteTicks);
    private sealed record CacheReceipt(string Repository, string Revision, Dictionary<string, FileStamp> Files);
}
