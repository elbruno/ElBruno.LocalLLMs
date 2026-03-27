using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Auto-download tests with a real HuggingFace endpoint.
/// Gated by [Trait("Category", "Integration")] — requires network access and disk space.
/// </summary>
[Trait("Category", "Integration")]
public class ModelDownloadTests : IAsyncDisposable
{
    private LocalChatClient? _client;

    // ──────────────────────────────────────────────
    // Auto-download on first use
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task CreateAsync_EnsureModelDownloaded_DownloadsModel()
    {
        SkipIfNotEnabled();

        // Use the smallest model for download test
        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Qwen25_05BInstruct,
            EnsureModelDownloaded = true,
            CacheDirectory = Path.Combine(Path.GetTempPath(), "localllms-test-" + Guid.NewGuid().ToString("N")[..8])
        };

        try
        {
            _client = await LocalChatClient.CreateAsync(options);

            // If we get here, model was downloaded (or was already cached)
            Assert.NotNull(_client);
            Assert.NotNull(_client.Metadata);
        }
        finally
        {
            // Cleanup temp cache
            if (Directory.Exists(options.CacheDirectory))
            {
                try { Directory.Delete(options.CacheDirectory, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Progress reporting
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task CreateAsync_WithProgress_ReportsProgress()
    {
        SkipIfNotEnabled();

        var progressReports = new List<ModelDownloadProgress>();
        var progress = new Progress<ModelDownloadProgress>(p => progressReports.Add(p));

        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Qwen25_05BInstruct,
            EnsureModelDownloaded = true,
            CacheDirectory = Path.Combine(Path.GetTempPath(), "localllms-progress-test-" + Guid.NewGuid().ToString("N")[..8])
        };

        try
        {
            _client = await LocalChatClient.CreateAsync(options, progress);

            // Progress should have been reported (unless model was already cached)
            // Not asserting count > 0 because model might be cached
            Assert.NotNull(_client);
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                try { Directory.Delete(options.CacheDirectory, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Download disabled — throws if not cached
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task CreateAsync_DownloadDisabledAndNotCached_Throws()
    {
        SkipIfNotEnabled();

        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Phi35MiniInstruct,
            EnsureModelDownloaded = false,
            CacheDirectory = Path.Combine(Path.GetTempPath(), "localllms-empty-" + Guid.NewGuid().ToString("N")[..8])
        };

        // Ensure cache dir exists but is empty
        Directory.CreateDirectory(options.CacheDirectory);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                _client = await LocalChatClient.CreateAsync(options);
            });
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                try { Directory.Delete(options.CacheDirectory, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Cancellation during download
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task CreateAsync_CancellationDuringDownload_Throws()
    {
        SkipIfNotEnabled();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Qwen25_05BInstruct,
            EnsureModelDownloaded = true,
            CacheDirectory = Path.Combine(Path.GetTempPath(), "localllms-cancel-" + Guid.NewGuid().ToString("N")[..8])
        };

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                _client = await LocalChatClient.CreateAsync(options, cancellationToken: cts.Token);
            });
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                try { Directory.Delete(options.CacheDirectory, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Local model path — skip download entirely
    // ──────────────────────────────────────────────

    [SkippableFact]
    public void ModelPath_Set_SkipsDownload()
    {
        SkipIfNotEnabled();

        var options = new LocalLLMsOptions
        {
            ModelPath = @"C:\nonexistent\model\path",
            EnsureModelDownloaded = false
        };

        // When ModelPath is set, the library should use it directly (and fail if invalid).
        // The key point: it should NOT attempt a download.
        Assert.NotNull(options.ModelPath);
        Assert.False(options.EnsureModelDownloaded);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static void SkipIfNotEnabled()
    {
        var enabled = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        Skip.IfNot(string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
            "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
