using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// End-to-end chat completion tests with a real model.
/// Gated by [Trait("Category", "Integration")] — requires a downloaded model and sufficient hardware.
/// Set environment variable RUN_INTEGRATION_TESTS=true to enable.
/// </summary>
[Trait("Category", "Integration")]
public class ChatCompletionTests : IAsyncDisposable
{
    private LocalChatClient? _client;

    // ──────────────────────────────────────────────
    // Basic completion
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetResponseAsync_SimpleQuestion_ReturnsResponse()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var response = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "What is 2 + 2? Answer with just the number.")
        ]);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Messages);
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [SkippableFact]
    public async Task GetResponseAsync_WithSystemMessage_ReturnsResponse()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var response = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.System, "You are a helpful math tutor. Be concise."),
            new ChatMessage(ChatRole.User, "What is the square root of 144?")
        ]);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.Contains("12", response.Text);
    }

    // ──────────────────────────────────────────────
    // Multi-turn conversation
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetResponseAsync_MultiTurn_MaintainsContext()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "My name is Alice."),
        };

        var response1 = await _client.GetResponseAsync(messages);
        Assert.NotNull(response1);

        messages.Add(new ChatMessage(ChatRole.Assistant, response1.Text ?? ""));
        messages.Add(new ChatMessage(ChatRole.User, "What is my name?"));

        var response2 = await _client.GetResponseAsync(messages);
        Assert.NotNull(response2);
        Assert.Contains("Alice", response2.Text ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Custom options
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetResponseAsync_WithCustomOptions_ReturnsResponse()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
        {
            Model = KnownModels.Phi35MiniInstruct,
            MaxSequenceLength = 512,
            Temperature = 0.1f,
            TopP = 0.5f
        });

        var response = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "Say hello.")
        ]);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);
    }

    // ──────────────────────────────────────────────
    // Cancellation
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetResponseAsync_CancellationRequested_Throws()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Tell me a very long story.")],
                cancellationToken: cts.Token);
        });
    }

    // ──────────────────────────────────────────────
    // Metadata
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task Metadata_IsPopulated()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        Assert.NotNull(_client.Metadata);
        Assert.Equal("elbruno-local-llms", _client.Metadata.ProviderName);
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [SkippableFact]
    public async Task GetResponseAsync_EmptyUserMessage_DoesNotThrow()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        var response = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "")
        ]);

        Assert.NotNull(response);
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
