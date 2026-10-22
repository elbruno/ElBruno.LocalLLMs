using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElBruno.HuggingFace;
using ElBruno.LocalLLMs.Decisions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public sealed class KevAcquisitionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kev-acquisition-").FullName;
    private readonly Dictionary<string, byte[]> _assets;
    private readonly List<DownloadRequest> _requests = [];

    public KevAcquisitionTests()
    {
        _assets = KevModelFiles.PublishedFiles.ToDictionary(name => name, name => Encoding.UTF8.GetBytes("asset " + name));
        _assets["kev_config.json"] = JsonSerializer.SerializeToUtf8Bytes(new
        {
            format = "kev-0.8b-prefill-v1",
            checkpoint_revision = "bf75a6a8848ea6960ff2ed108d9ed44c2941174f",
            base_revision = "dc7cdfe2ee4154fa7e30f5b51ca41bfa40174e68",
            max_tokens = 8192,
            files = _assets.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant()),
        });
    }

    private KevDecisionOptions Options() => new() { CacheDirectory = _root };

    private async Task DownloadAsync(DownloadRequest request, CancellationToken token)
    {
        _requests.Add(request);
        Assert.Equal(new KevDecisionOptions().ModelRevision, request.Revision);
        Assert.Equal(request.Revision, request.ExpectedCommitSha);
        Assert.True(request.UseAtomicWrites);
        foreach (var name in request.RequiredFiles)
            await File.WriteAllBytesAsync(Path.Combine(request.LocalDirectory, name), _assets[name], token);
    }

    [Fact]
    public async Task FirstUse_DownloadsValidatedFilesAndNewCallerReusesCacheWithoutNetwork()
    {
        var directory = await KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        Assert.Equal(2, _requests.Count);
        Assert.Equal(new[] { "kev_config.json" }, _requests[0].RequiredFiles);
        Assert.Equal(KevModelFiles.PublishedFiles, _requests[1].RequiredFiles);
        Assert.True(File.Exists(Path.Combine(directory, ".kev-complete.json")));
        Assert.Equal(directory, await KevModelFiles.EnsureAsync(Options(), NoDownload));
    }

    [Fact]
    public async Task TruncatedCache_RepairsOnlyDamagedFileAndDoesNotTrustManifestAlone()
    {
        var directory = await KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        await File.WriteAllTextAsync(Path.Combine(directory, "model.onnx.data"), "bad");
        _requests.Clear();
        await KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        Assert.Equal(new[] { "model.onnx.data" }, Assert.Single(_requests).RequiredFiles);
        Assert.Equal(_assets["model.onnx.data"], File.ReadAllBytes(Path.Combine(directory, "model.onnx.data")));
    }

    [Fact]
    public async Task MissingWeights_TriggerDownloadEvenWhenGraphAndManifestExist()
    {
        var options = Options();
        var directory = options.ResolveModelDirectory();
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "decision.onnx", "kev_config.json" })
            await File.WriteAllBytesAsync(Path.Combine(directory, name), _assets[name]);
        await KevModelFiles.EnsureAsync(options, DownloadAsync);
        Assert.Contains("model.onnx.data", Assert.Single(_requests).RequiredFiles);
    }

    [Fact]
    public async Task ConcurrentClients_ShareOneCompletedTransfer()
    {
        var first = KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        var second = KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        var paths = await Task.WhenAll(first, second);
        Assert.Equal(paths[0], paths[1]);
        Assert.Equal(2, _requests.Count);
    }

    [Fact]
    public async Task CancelledTransfer_LeavesNoReceiptAndCanBeRetried()
    {
        var options = Options();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => KevModelFiles.EnsureAsync(options, async (request, token) =>
        {
            if (request.RequiredFiles.Contains("kev_config.json")) await DownloadAsync(request, token);
            else { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
        }, cancellation.Token));
        Assert.False(File.Exists(Path.Combine(options.ResolveModelDirectory(), ".kev-complete.json")));
        await KevModelFiles.EnsureAsync(options, DownloadAsync);
        await KevModelFiles.EnsureAsync(options, NoDownload);
    }

    [Fact]
    public async Task CorruptedTransfer_FailsExplicitlyWithoutCompletionAndRetries()
    {
        var options = Options();
        var exception = await Assert.ThrowsAsync<DecisionException>(() => KevModelFiles.EnsureAsync(options, async (request, token) =>
        {
            await DownloadAsync(request, token);
            if (request.RequiredFiles.Contains("model.onnx.data"))
                await File.WriteAllTextAsync(Path.Combine(request.LocalDirectory, "model.onnx.data"), "corrupt", token);
        }));
        Assert.Contains("SHA-256", exception.Message);
        Assert.False(File.Exists(Path.Combine(options.ResolveModelDirectory(), ".kev-complete.json")));
        await KevModelFiles.EnsureAsync(options, DownloadAsync);
    }

    [Fact]
    public async Task IncompatibleManifest_IsRejectedBeforeWeights()
    {
        _assets["kev_config.json"] = Encoding.UTF8.GetBytes("""{"format":"wrong","checkpoint_revision":"wrong","base_revision":"wrong","max_tokens":8192}""");
        await Assert.ThrowsAsync<DecisionException>(() => KevModelFiles.EnsureAsync(Options(), DownloadAsync));
        Assert.Single(_requests);
    }

    [Fact]
    public async Task NetworkFailure_ExplainsSourceAndLocalAlternative()
    {
        var exception = await Assert.ThrowsAsync<DecisionException>(() => KevModelFiles.EnsureAsync(Options(),
            (_, _) => throw new HttpRequestException("unreachable")));
        Assert.Contains("elbruno/kev-0.8b-onnx", exception.Message);
        Assert.Contains("ModelPath", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task ExplicitLocalPath_NeverDownloadsOrRepairsAndNeedsEveryRuntimeFile()
    {
        var options = new KevDecisionOptions { ModelPath = _root, ModelRepository = "", ModelRevision = "" };
        options.Validate();
        foreach (var name in KevModelFiles.RuntimeFiles)
            await File.WriteAllBytesAsync(Path.Combine(_root, name), _assets[name]);
        Assert.Equal(_root, await KevModelFiles.EnsureAsync(options, NoDownload));
        File.Delete(Path.Combine(_root, "model.onnx.data"));
        await Assert.ThrowsAsync<DecisionException>(() => KevModelFiles.EnsureAsync(options, NoDownload));
        Assert.False(File.Exists(Path.Combine(_root, "model.onnx.data")));
    }

    [Fact]
    public void DefaultsAndDI_AreLazyAndPreserveFirstProvider()
    {
        var options = new KevDecisionOptions();
        options.Validate();
        Assert.Equal("elbruno/kev-0.8b-onnx", options.ModelRepository);
        Assert.Equal(40, options.ModelRevision.Length);
        Assert.Equal(4, options.IntraOpNumThreads);
        Assert.Null(options.ModelPath);
        using var direct = new KevOnnxDecisionClient();
        var services = new ServiceCollection();
        services.AddLocalKevDecisions(configure => configure.CacheDirectory = _root);
        services.AddLocalDecisions();
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<KevOnnxDecisionClient>(), provider.GetRequiredService<IDecisionClient>());
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void Options_RejectInvalidSettingsAndSeparateSourceRevisions()
    {
        Assert.ThrowsAny<ArgumentException>(() => new KevOnnxDecisionClient(new KevDecisionOptions { ModelRevision = "main" }));
        Assert.ThrowsAny<ArgumentException>(() => new KevOnnxDecisionClient(new KevDecisionOptions { ModelRepository = "../wrong" }));
        Assert.ThrowsAny<ArgumentException>(() => new KevOnnxDecisionClient(new KevDecisionOptions { IntraOpNumThreads = 0 }));
        var first = Options();
        var second = Options();
        second.ModelRevision = new string('a', 40);
        Assert.NotEqual(first.ResolveModelDirectory(), second.ResolveModelDirectory());
        second.ModelRevision = first.ModelRevision;
        second.ModelRepository = "another/export";
        Assert.NotEqual(first.ResolveModelDirectory(), second.ResolveModelDirectory());
    }

    [Fact]
    public async Task InvalidReceipt_RevalidatesAssetsWithoutNetwork()
    {
        var directory = await KevModelFiles.EnsureAsync(Options(), DownloadAsync);
        File.WriteAllText(Path.Combine(directory, ".kev-complete.json"), """{"Files":null}""");
        await KevModelFiles.EnsureAsync(Options(), NoDownload);
    }

    [Fact]
    public async Task WaitingForAnotherProcess_CanBeCancelledWithoutTouchingFiles()
    {
        var options = Options();
        var directory = options.ResolveModelDirectory();
        Directory.CreateDirectory(directory);
        using var held = new FileStream(Path.Combine(directory, ".kev-download.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KevModelFiles.EnsureAsync(options, NoDownload, cancellation.Token));
        Assert.False(File.Exists(Path.Combine(directory, ".kev-complete.json")));
        Assert.Empty(_requests);
    }

    [Fact]
    public async Task PrecancelledAcquisition_DoesNotCreateTheCache()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KevModelFiles.EnsureAsync(Options(), NoDownload, new CancellationToken(true)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    private static Task NoDownload(DownloadRequest request, CancellationToken token) =>
        throw new InvalidOperationException("Network must not be used.");

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
