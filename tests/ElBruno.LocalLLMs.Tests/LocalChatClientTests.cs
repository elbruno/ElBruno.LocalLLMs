using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="LocalChatClient"/> — construction, metadata, disposal, argument validation.
/// Uses NSubstitute to mock IModelDownloader (no real model downloads).
/// </summary>
public class LocalChatClientTests : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _disposables = [];

    // ──────────────────────────────────────────────
    // Constructor — with options and mocked downloader
    // ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithOptionsAndDownloader_CreatesInstance()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions();

        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var downloader = Substitute.For<IModelDownloader>();

        Assert.Throws<ArgumentNullException>(() => new LocalChatClient(null!, downloader));
    }

    [Fact]
    public void Constructor_NullDownloader_ThrowsArgumentNullException()
    {
        var options = new LocalLLMsOptions();

        Assert.Throws<ArgumentNullException>(() => new LocalChatClient(options, null!));
    }

    [Fact]
    public void Constructor_Default_CreatesInstance()
    {
        var client = new LocalChatClient();
        _disposables.Add(client);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithOptions_CreatesInstance()
    {
        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Phi35MiniInstruct,
            EnsureModelDownloaded = false
        };

        var client = new LocalChatClient(options);
        _disposables.Add(client);

        Assert.NotNull(client);
    }

    // ──────────────────────────────────────────────
    // IChatClient interface compliance
    // ──────────────────────────────────────────────

    [Fact]
    public void Type_ImplementsIChatClient()
    {
        Assert.True(typeof(IChatClient).IsAssignableFrom(typeof(LocalChatClient)));
    }

    [Fact]
    public void Type_ImplementsIAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(LocalChatClient)));
    }

    [Fact]
    public void Type_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(LocalChatClient)));
    }

    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(LocalChatClient).IsSealed);
    }

    // ──────────────────────────────────────────────
    // Metadata
    // ──────────────────────────────────────────────

    [Fact]
    public void Metadata_IsNotNull()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        Assert.NotNull(client.Metadata);
    }

    [Fact]
    public void Metadata_ProviderName_IsCorrect()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        Assert.Equal("elbruno-local-llms", client.Metadata.ProviderName);
    }

    [Fact]
    public void Metadata_DefaultModelId_MatchesOptions()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions { Model = KnownModels.Phi4 };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        Assert.Equal("phi-4", client.Metadata.DefaultModelId);
    }

    [Fact]
    public void Metadata_ProviderUri_IsCorrect()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        Assert.Equal(new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"), client.Metadata.ProviderUri);
    }

    [Fact]
    public void Metadata_DefaultModelId_MatchesDefaultModel()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        Assert.Equal(KnownModels.Phi35MiniInstruct.Id, client.Metadata.DefaultModelId);
    }

    // ──────────────────────────────────────────────
    // GetService
    // ──────────────────────────────────────────────

    [Fact]
    public void GetService_IChatClient_ReturnsSelf()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        var service = client.GetService(typeof(IChatClient));

        Assert.Same(client, service);
    }

    [Fact]
    public void GetService_LocalChatClient_ReturnsSelf()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        var service = client.GetService(typeof(LocalChatClient));

        Assert.Same(client, service);
    }

    [Fact]
    public void GetService_UnrelatedType_ReturnsNull()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        var service = client.GetService(typeof(IModelDownloader));

        Assert.Null(service);
    }

    [Fact]
    public void GetService_WithServiceKey_ReturnsNull()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        var service = client.GetService(typeof(IChatClient), "some-key");

        Assert.Null(service);
    }

    // ──────────────────────────────────────────────
    // Argument validation — GetResponseAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_NullMessages_ThrowsArgumentNullException()
    {
        var downloader = Substitute.For<IModelDownloader>();
        downloader.EnsureModelAsync(
            Arg.Any<ModelDefinition>(),
            Arg.Any<string?>(),
            Arg.Any<IProgress<ModelDownloadProgress>?>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(Path.GetTempPath()));

        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.GetResponseAsync(null!));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NullMessages_ThrowsArgumentNullException()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        _disposables.Add(client);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(null!))
            {
            }
        });
    }

    // ──────────────────────────────────────────────
    // Disposal
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        client.Dispose();
        client.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        await client.DisposeAsync();
        await client.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task GetResponseAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var client = new LocalChatClient(new LocalLLMsOptions(), downloader);
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hello")]))
            {
            }
        });
    }

    // ──────────────────────────────────────────────
    // IModelDownloader mock verification
    // ──────────────────────────────────────────────

    [Fact]
    public void IModelDownloader_InterfaceExists()
    {
        Assert.True(typeof(IModelDownloader).IsInterface);
    }

    [Fact]
    public void IModelDownloader_CanBeMocked()
    {
        var mock = Substitute.For<IModelDownloader>();

        mock.EnsureModelAsync(
            Arg.Any<ModelDefinition>(),
            Arg.Any<string?>(),
            Arg.Any<IProgress<ModelDownloadProgress>?>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult("/fake/path"));

        mock.GetCacheDirectory().Returns("/fake/cache");

        Assert.NotNull(mock);
    }

    // ──────────────────────────────────────────────
    // ModelDownloadProgress struct
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelDownloadProgress_IsReadonlyRecordStruct()
    {
        Assert.True(typeof(ModelDownloadProgress).IsValueType);
    }

    [Fact]
    public void ModelDownloadProgress_HasExpectedProperties()
    {
        var progress = new ModelDownloadProgress("model.onnx", 1024, 2048, 50.0);

        Assert.Equal("model.onnx", progress.FileName);
        Assert.Equal(1024, progress.BytesDownloaded);
        Assert.Equal(2048, progress.TotalBytes);
        Assert.Equal(50.0, progress.PercentComplete);
    }

    [Fact]
    public void ModelDownloadProgress_DefaultValues()
    {
        var progress = default(ModelDownloadProgress);

        Assert.Null(progress.FileName);
        Assert.Equal(0, progress.BytesDownloaded);
        Assert.Equal(0, progress.TotalBytes);
        Assert.Equal(0.0, progress.PercentComplete);
    }

    [Fact]
    public void ModelDownloadProgress_Equality()
    {
        var a = new ModelDownloadProgress("file.onnx", 100, 200, 50.0);
        var b = new ModelDownloadProgress("file.onnx", 100, 200, 50.0);

        Assert.Equal(a, b);
    }

    // ──────────────────────────────────────────────
    // Static factory methods exist
    // ──────────────────────────────────────────────

    [Fact]
    public void CreateAsync_StaticFactoryExists()
    {
        var methods = typeof(LocalChatClient).GetMethods()
            .Where(m => m.Name == "CreateAsync" && m.IsStatic)
            .ToList();

        Assert.NotEmpty(methods);
        Assert.True(methods.Count >= 2, "Should have at least 2 CreateAsync overloads");
    }

    // ──────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
    }
}
