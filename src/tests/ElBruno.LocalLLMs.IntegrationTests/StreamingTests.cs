using System.Diagnostics;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Streaming token-by-token tests with a real model.
/// Gated by [Trait("Category", "Integration")] — requires a downloaded model and sufficient hardware.
/// </summary>
[Trait("Category", "Integration")]
public class StreamingTests : IAsyncDisposable
{
    private LocalChatClient? _client;

    // ──────────────────────────────────────────────
    // Basic streaming
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetStreamingResponseAsync_SimpleQuestion_YieldsTokens()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var tokens = new List<string>();

        await foreach (var update in _client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.User, "Count from 1 to 5.")
        ]))
        {
            if (update.Text is not null)
            {
                tokens.Add(update.Text);
            }
        }

        Assert.NotEmpty(tokens);
        Assert.True(tokens.Count > 1, "Streaming should yield multiple tokens");
    }

    [SkippableFact]
    public async Task GetStreamingResponseAsync_WithSystemMessage_YieldsTokens()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var tokens = new List<string>();

        await foreach (var update in _client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.System, "You are a poet. Be brief."),
            new ChatMessage(ChatRole.User, "Write a haiku about code.")
        ]))
        {
            if (update.Text is not null)
            {
                tokens.Add(update.Text);
            }
        }

        Assert.NotEmpty(tokens);
    }

    // ──────────────────────────────────────────────
    // Concatenated output matches expected content
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetStreamingResponseAsync_ConcatenatedTokens_FormValidResponse()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var fullResponse = "";

        await foreach (var update in _client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.User, "What is 2 + 2? Answer with just the number.")
        ]))
        {
            fullResponse += update.Text ?? "";
        }

        Assert.False(string.IsNullOrWhiteSpace(fullResponse));
    }

    // ──────────────────────────────────────────────
    // Cancellation during streaming
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetStreamingResponseAsync_CancellationDuringStream_StopsAtTokenBoundary_AndClientCanBeReused()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        using var cts = new CancellationTokenSource();
        var tokenCount = 0;
        Stopwatch? cancellationStopwatch = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in _client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Tell me a very long story about dragons.")],
                cancellationToken: cts.Token))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                tokenCount++;
                if (tokenCount == 3)
                {
                    cancellationStopwatch = Stopwatch.StartNew();
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(3, tokenCount);
        Assert.NotNull(cancellationStopwatch);
        Assert.True(cancellationStopwatch!.Elapsed <= TimeSpan.FromSeconds(2),
            $"Cancellation should stop promptly at a token boundary, but took {cancellationStopwatch.Elapsed.TotalMilliseconds:F0} ms.");

        var followUp = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "Say ready.")
        ]);

        Assert.False(string.IsNullOrWhiteSpace(followUp.Text));
    }

    // ──────────────────────────────────────────────
    // Streaming with custom options
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetStreamingResponseAsync_LowTemperature_YieldsTokens()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
        {
            Model = KnownModels.Phi35MiniInstruct,
            Temperature = 0.1f,
            MaxSequenceLength = 256
        });

        var tokens = new List<string>();

        await foreach (var update in _client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.User, "Say hello.")
        ]))
        {
            if (update.Text is not null)
            {
                tokens.Add(update.Text);
            }
        }

        Assert.NotEmpty(tokens);
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
