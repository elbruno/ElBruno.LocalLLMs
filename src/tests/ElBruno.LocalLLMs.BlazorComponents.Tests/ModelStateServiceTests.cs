using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BlazorComponents.Extensions;
using ElBruno.LocalLLMs.BlazorComponents.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.BlazorComponents.Tests;

public sealed class ModelStateServiceTests
{
    private static readonly ModelDefinition TestModel = new()
    {
        Id = "test-model",
        DisplayName = "Test model",
        HuggingFaceRepoId = "test/repo",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML
    };

    [Fact]
    public void Registration_UsesSingletonLifetime()
    {
        var services = new ServiceCollection();

        services.AddLocalLLMsBlazorComponents();

        var descriptor = services.Single(service => service.ServiceType == typeof(ModelStateService));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public async Task Singleton_IsSharedAcrossChildScopes()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<ModelStateService>();
        var second = secondScope.ServiceProvider.GetRequiredService<ModelStateService>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ChildScopeDisposal_DoesNotCancelActiveDownload()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ModelStateService>();

        var download = service.StartDownloadAsync(TestModel);
        var token = await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        scope.Dispose();

        Assert.False(token.IsCancellationRequested);
        Assert.False(download.IsCompleted);

        downloader.Release();
        await download.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Download_CompletesAfterSimulatedCircuitDisconnect()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        var disconnectedScope = provider.CreateScope();
        var service = disconnectedScope.ServiceProvider.GetRequiredService<ModelStateService>();

        var download = service.StartDownloadAsync(TestModel);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        disconnectedScope.Dispose();

        downloader.Release();
        await download.WaitAsync(TimeSpan.FromSeconds(5));

        using var reconnectedScope = provider.CreateScope();
        var status = reconnectedScope.ServiceProvider
            .GetRequiredService<ModelStateService>()
            .GetStatus(TestModel);

        Assert.Equal(ModelDownloadState.Downloaded, status.State);
    }

    [Fact]
    public async Task CancelDownload_IsExplicitAndModelGlobal()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        using var ownerScope = provider.CreateScope();
        using var cancellingScope = provider.CreateScope();
        var service = ownerScope.ServiceProvider.GetRequiredService<ModelStateService>();
        var cancellingService = cancellingScope.ServiceProvider.GetRequiredService<ModelStateService>();

        var download = service.StartDownloadAsync(TestModel);
        var token = await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellingService.CancelDownload(TestModel);

        await download.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(token.IsCancellationRequested);
        Assert.True(downloader.CancellationObserved.Task.IsCompleted);
        Assert.Equal(ModelDownloadState.NotDownloaded, service.GetStatus(TestModel).State);
    }

    [Fact]
    public async Task ConcurrentStarts_ShareOneInFlightOperation()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        var service = provider.GetRequiredService<ModelStateService>();

        var first = service.StartDownloadAsync(TestModel);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var concurrentStarts = Enumerable.Range(0, 10)
            .Select(_ => service.StartDownloadAsync(TestModel))
            .ToArray();

        Assert.All(concurrentStarts, operation => Assert.Same(first, operation));
        Assert.Equal(1, downloader.EnsureModelCallCount);

        downloader.Release();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConcurrentStarts_CancelDownloadCancelsSharedOperation()
    {
        using var downloader = new BlockingModelDownloader();
        await using var provider = CreateProvider(downloader);
        var service = provider.GetRequiredService<ModelStateService>();

        var first = service.StartDownloadAsync(TestModel);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.StartDownloadAsync(TestModel);
        var startedToken = await downloader.Started.Task;

        service.CancelDownload(TestModel);

        await Task.WhenAll(
            first.WaitAsync(TimeSpan.FromSeconds(5)),
            second.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(first, second);
        Assert.Equal(1, downloader.EnsureModelCallCount);
        Assert.True(startedToken.IsCancellationRequested);
        Assert.True(downloader.CancellationObserved.Task.IsCompleted);
        Assert.Equal(ModelDownloadState.NotDownloaded, service.GetStatus(TestModel).State);
    }

    [Fact]
    public async Task RootProviderShutdown_CancelsActiveDownloads()
    {
        using var downloader = new BlockingModelDownloader();
        var provider = CreateProvider(downloader);
        var disposed = false;

        try
        {
            var service = provider.GetRequiredService<ModelStateService>();
            var download = service.StartDownloadAsync(TestModel);
            await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await provider.DisposeAsync();
            disposed = true;

            await downloader.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await download.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(downloader.CancellationObserved.Task.IsCompletedSuccessfully);
        }
        finally
        {
            if (!disposed)
                await provider.DisposeAsync();
        }
    }

    private static ServiceProvider CreateProvider(BlockingModelDownloader downloader)
    {
        var services = new ServiceCollection();
        services.AddLocalLLMsBlazorComponents();
        services.AddSingleton<IModelDownloader>(downloader);
        return services.BuildServiceProvider();
    }

    private sealed class BlockingModelDownloader : IModelDownloader, IDisposable
    {
        private readonly string _modelPath = Path.Combine(
            Path.GetTempPath(),
            "localllms-blazor-test-" + Guid.NewGuid().ToString("N"));

        public TaskCompletionSource<CancellationToken> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private int _ensureModelCallCount;

        public int EnsureModelCallCount => Volatile.Read(ref _ensureModelCallCount);

        public TaskCompletionSource<bool> CancellationObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource<bool> ReleaseSignal { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> EnsureModelAsync(
            ModelDefinition model,
            string? cacheDirectory = null,
            IProgress<ModelDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _ensureModelCallCount);
            Started.TrySetResult(cancellationToken);
            return WaitForReleaseAsync(cancellationToken);
        }

        public string GetCacheDirectory() => Path.GetDirectoryName(_modelPath)!;

        public Task DeleteModelAsync(
            ModelDefinition model,
            string? cacheDirectory = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Release() => ReleaseSignal.TrySetResult(true);

        private async Task<string> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(
                () => CancellationObserved.TrySetResult(true));

            await Task.WhenAny(ReleaseSignal.Task, CancellationObserved.Task)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_modelPath);
            await File.WriteAllTextAsync(
                Path.Combine(_modelPath, "genai_config.json"),
                "{}",
                CancellationToken.None).ConfigureAwait(false);
            return _modelPath;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_modelPath))
                    Directory.Delete(_modelPath, recursive: true);
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
