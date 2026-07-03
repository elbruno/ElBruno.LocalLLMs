using System.Diagnostics;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Diagnostics;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// End-to-end generation-lifecycle diagnostics tests (Issue #21) against a real model through
/// the public <see cref="IChatClient"/> surface. Verifies the ordered lifecycle
/// <see cref="ActivityEvent"/>s (queued → model ready → generation started → first token →
/// completed), plausible token counts, and a measurable time-to-first-token, for both the
/// buffered and streaming paths.
/// Gated by [Trait("Category", "Integration")] — requires a downloaded model and sufficient
/// hardware. Set environment variable RUN_INTEGRATION_TESTS=true to enable.
/// </summary>
[Trait("Category", "Integration")]
public class GenerationLifecycleIntegrationTests : IAsyncDisposable
{
    private LocalChatClient? _client;

    [SkippableFact]
    public async Task GetResponseAsync_RealModel_EmitsOrderedLifecycleWithMeasurableTtft()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        using var capture = new ActivityCapture();

        var response = await _client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "What is 2 + 2? Answer with just the number.")
        ]);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));

        var activity = Assert.Single(capture.CompletedActivities);

        // Ordered lifecycle stages — queued and model-ready must precede generation-started,
        // which must precede first-token, which must precede completed.
        var eventNames = activity.Events.Select(e => e.Name).ToList();
        Assert.Contains("gen_ai.queued", eventNames);
        Assert.Contains("gen_ai.model_ready", eventNames);
        Assert.Contains("gen_ai.generation_started", eventNames);
        Assert.Contains("gen_ai.first_token", eventNames);
        Assert.Contains("gen_ai.completed", eventNames);
        Assert.True(IsStrictlyOrdered(eventNames,
            "gen_ai.queued", "gen_ai.model_ready", "gen_ai.generation_started", "gen_ai.first_token", "gen_ai.completed"));

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);

        // Time-to-first-token must be measurable (present and non-negative).
        var firstTokenEvent = activity.Events.Single(e => e.Name == "gen_ai.first_token");
        var ttft = (double)firstTokenEvent.Tags.Single(t => t.Key == "gen_ai.server.time_to_first_token").Value!;
        Assert.True(ttft >= 0);

        // Token counts should be plausible (non-zero) for a real completion.
        var inputTokens = (int)GetTag(activity, "gen_ai.usage.input_tokens")!;
        var outputTokens = (int)GetTag(activity, "gen_ai.usage.output_tokens")!;
        Assert.True(inputTokens > 0);
        Assert.True(outputTokens > 0);

        // Prompt/completion text must NOT be attached by default.
        Assert.DoesNotContain(activity.Events, e => e.Name is "gen_ai.prompt" or "gen_ai.completion");
    }

    [SkippableFact]
    public async Task GetStreamingResponseAsync_RealModel_EmitsOrderedLifecycleWithMeasurableTtft()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        using var capture = new ActivityCapture();

        var tokenCount = 0;
        await foreach (var update in _client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.User, "Count from 1 to 5.")
        ]))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                tokenCount++;
            }
        }

        Assert.True(tokenCount > 0);

        var activity = Assert.Single(capture.CompletedActivities);
        var eventNames = activity.Events.Select(e => e.Name).ToList();
        Assert.True(IsStrictlyOrdered(eventNames,
            "gen_ai.queued", "gen_ai.model_ready", "gen_ai.generation_started", "gen_ai.first_token", "gen_ai.completed"));
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);

        var outputTokens = (int)GetTag(activity, "gen_ai.usage.output_tokens")!;
        Assert.True(outputTokens > 0);
        Assert.DoesNotContain(activity.Events, e => e.Name is "gen_ai.prompt" or "gen_ai.completion");
    }

    [SkippableFact]
    public async Task GetStreamingResponseAsync_RealModel_CancellationIsDistinctFromFailure()
    {
        SkipIfNotEnabled();

        _client = await LocalChatClient.CreateAsync();

        using var capture = new ActivityCapture();
        using var cts = new CancellationTokenSource();
        var tokenCount = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in _client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Tell me a very long story about dragons.")],
                cancellationToken: cts.Token))
            {
                tokenCount++;
                if (tokenCount >= 3)
                {
                    cts.Cancel();
                }
            }
        });

        var activity = Assert.Single(capture.CompletedActivities);
        Assert.Contains(activity.Events, e => e.Name == "gen_ai.cancelled");
        Assert.DoesNotContain(activity.Events, e => e.Name == "gen_ai.failed");
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static bool IsStrictlyOrdered(IReadOnlyList<string> events, params string[] expectedOrder)
    {
        var lastIndex = -1;
        foreach (var name in expectedOrder)
        {
            var index = -1;
            for (var i = lastIndex + 1; i < events.Count; i++)
            {
                if (events[i] == name)
                {
                    index = i;
                    break;
                }
            }

            if (index <= lastIndex)
            {
                return false;
            }
            lastIndex = index;
        }
        return true;
    }

    private static object? GetTag(Activity activity, string key)
        => activity.TagObjects.FirstOrDefault(t => t.Key == key).Value;

    private static void SkipIfNotEnabled()
    {
        var enabled = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        Skip.IfNot(string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
            "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");
    }

    /// <summary>Captures completed root activities emitted on the ElBruno.LocalLLMs ActivitySource.</summary>
    private sealed class ActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;

        public List<Activity> CompletedActivities { get; } = [];

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == LocalLLMsInstrumentation.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => CompletedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
