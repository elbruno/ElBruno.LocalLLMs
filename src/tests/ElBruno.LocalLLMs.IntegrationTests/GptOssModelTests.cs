using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Real-model tests for GPT-OSS 20B ONNX (Harmony format).
///
/// These are gated behind <c>RUN_GPTOSS_TESTS=true</c> in addition to the usual
/// integration switch: the CPU INT4 artifact is a multi-gigabyte download and
/// mixture-of-experts inference on CPU is slow, so this must never run in CI by
/// accident. Point <c>GPTOSS_MODEL_PATH</c> at an already-downloaded model
/// directory to skip the download entirely.
/// </summary>
[Trait("Category", "Integration")]
public class GptOssModelTests : IAsyncDisposable
{
    private LocalChatClient? _client;

    [SkippableFact]
    public async Task GptOss_LoadsAndGenerates()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync(BuildOptions());

        var response = await _client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is the capital of France? Answer in one word.")],
            new ChatOptions { MaxOutputTokens = 64 });

        var text = response.Text;

        Assert.False(string.IsNullOrWhiteSpace(text));

        // The chain-of-thought must never reach the caller.
        Assert.DoesNotContain("<|channel|>", text);
        Assert.DoesNotContain("<|message|>", text);
        Assert.DoesNotContain("analysis", text, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task GptOss_Streaming_EmitsOnlyFinalChannel()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync(BuildOptions());

        var chunks = new List<string>();
        await foreach (var update in _client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Name one primary colour.")],
            new ChatOptions { MaxOutputTokens = 64 }))
        {
            chunks.Add(update.Text);
        }

        var combined = string.Concat(chunks);

        Assert.False(string.IsNullOrWhiteSpace(combined));
        Assert.DoesNotContain("<|", combined);
    }

    [SkippableFact]
    public async Task GptOss_ReasoningEffort_IsAccepted()
    {
        SkipIfNotEnabled();

        var options = BuildOptions();
        options.ReasoningEffort = ReasoningEffort.Low;

        _client = await LocalChatClient.CreateAsync(options);

        var response = await _client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Say hello.")],
            new ChatOptions { MaxOutputTokens = 32 });

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static LocalLLMsOptions BuildOptions()
    {
        var options = new LocalLLMsOptions
        {
            Model = KnownModels.GptOss20B,
            MaxSequenceLength = 2048
        };

        var localPath = Environment.GetEnvironmentVariable("GPTOSS_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            options.ModelPath = localPath;
            options.EnsureModelDownloaded = false;
        }
        else
        {
            options.EnsureModelDownloaded = true;
        }

        return options;
    }

    private static void SkipIfNotEnabled()
    {
        var integration = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        Skip.IfNot(string.Equals(integration, "true", StringComparison.OrdinalIgnoreCase),
            "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");

        var gptOss = Environment.GetEnvironmentVariable("RUN_GPTOSS_TESTS");
        Skip.IfNot(string.Equals(gptOss, "true", StringComparison.OrdinalIgnoreCase),
            "GPT-OSS tests disabled (multi-GB download). Set RUN_GPTOSS_TESTS=true to enable.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
