using System.Diagnostics;
using System.Diagnostics.Metrics;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Diagnostics;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests.Diagnostics;

/// <summary>
/// Tests for the generation-lifecycle diagnostics contract (Issue #21): a single root
/// <see cref="Activity"/> per generation call carrying the queued/model-ready/generation-started/
/// first-token/completed/cancelled/failed <see cref="ActivityEvent"/>s, matching metrics on
/// <see cref="LocalLLMsInstrumentation.Meter"/>, prompt/completion content excluded by default,
/// and cancellation classified distinctly from failure. Exercised both directly against the
/// <see cref="LocalLLMsInstrumentation"/> helpers and end-to-end through <see cref="LocalChatClient"/>
/// (the public <c>IChatClient</c> surface) using the existing NSubstitute-mocked
/// <see cref="IModelDownloader"/> seam — no real model download/load is required for these cases
/// because they exercise the pre-load (cancelled) and load-failure (invalid model directory) paths.
/// </summary>
public class GenerationLifecycleDiagnosticsTests
{
    // ──────────────────────────────────────────────
    // Contract constants
    // ──────────────────────────────────────────────

    [Fact]
    public void ActivitySourceName_IsElBrunoLocalLLMs()
    {
        Assert.Equal("ElBruno.LocalLLMs", LocalLLMsInstrumentation.ActivitySourceName);
    }

    [Fact]
    public void MeterName_IsElBrunoLocalLLMs()
    {
        Assert.Equal("ElBruno.LocalLLMs", LocalLLMsInstrumentation.MeterName);
    }

    // ──────────────────────────────────────────────
    // StartChatActivity — near-zero cost without a listener
    // ──────────────────────────────────────────────

    [Fact]
    public void StartChatActivity_WithoutListener_ReturnsNull()
    {
        // No ActivityListener registered for this source in this test -> StartActivity is a no-op.
        using var activity = LocalLLMsInstrumentation.StartChatActivity("test-model", streaming: false, ExecutionProvider.Cpu);

        Assert.Null(activity);
    }

    [Fact]
    public void StartChatActivity_WithListener_StampsBaseRequestTags()
    {
        using var _ = SubscribeAllDataListener();

        using var activity = LocalLLMsInstrumentation.StartChatActivity("phi-3.5-mini-instruct", streaming: true, ExecutionProvider.Cuda);

        Assert.NotNull(activity);
        Assert.Equal("chat phi-3.5-mini-instruct", activity!.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        AssertTag(activity, "gen_ai.system", "elbruno.local_llms");
        AssertTag(activity, "gen_ai.operation.name", "chat");
        AssertTag(activity, "gen_ai.request.model", "phi-3.5-mini-instruct");
        AssertTag(activity, "elbruno.execution_provider", "Cuda");
        AssertTag(activity, "elbruno.streaming", "True");
    }

    // ──────────────────────────────────────────────
    // Lifecycle event helpers — direct assertions on the extension methods
    // ──────────────────────────────────────────────

    [Fact]
    public void AddCompletedEvent_SetsOkStatus_AndTokenTags()
    {
        using var activity = new Activity("test").Start();

        activity.AddCompletedEvent(inputTokens: 12, outputTokens: 34);

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Contains(activity.Events, e => e.Name == "gen_ai.completed");
        AssertTag(activity, "gen_ai.usage.input_tokens", "12");
        AssertTag(activity, "gen_ai.usage.output_tokens", "34");
        AssertTag(activity, "gen_ai.outcome", "completed");
    }

    [Fact]
    public void AddCancelledEvent_SetsUnsetStatus_NotError()
    {
        using var activity = new Activity("test").Start();

        activity.AddCancelledEvent();

        // Cancellation must never surface as ActivityStatusCode.Error — that's reserved for failures.
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Contains(activity.Events, e => e.Name == "gen_ai.cancelled");
        AssertTag(activity, "gen_ai.outcome", "cancelled");
        AssertTag(activity, "error.type", "cancelled");
    }

    [Fact]
    public void AddFailedEvent_SetsErrorStatus_DistinctFromCancelled()
    {
        using var activity = new Activity("test").Start();

        activity.AddFailedEvent(new InvalidOperationException("boom"));

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains(activity.Events, e => e.Name == "gen_ai.failed");
        AssertTag(activity, "gen_ai.outcome", "error");
        AssertTag(activity, "error.type", typeof(InvalidOperationException).FullName!);
    }

    [Fact]
    public void AddFirstTokenEvent_AttachesTimeToFirstTokenTag()
    {
        using var activity = new Activity("test").Start();

        activity.AddFirstTokenEvent(TimeSpan.FromMilliseconds(250));

        var evt = Assert.Single(activity.Events, e => e.Name == "gen_ai.first_token");
        var tag = evt.Tags.Single(t => t.Key == "gen_ai.server.time_to_first_token");
        Assert.Equal(0.25, (double)tag.Value!, 3);
    }

    [Fact]
    public void LifecycleEvents_QueuedModelReadyGenerationStarted_UseContractNames()
    {
        using var activity = new Activity("test").Start();

        activity.AddQueuedEvent();
        activity.AddModelReadyEvent();
        activity.AddGenerationStartedEvent();

        Assert.Collection(activity.Events,
            e => Assert.Equal("gen_ai.queued", e.Name),
            e => Assert.Equal("gen_ai.model_ready", e.Name),
            e => Assert.Equal("gen_ai.generation_started", e.Name));
    }

    // ──────────────────────────────────────────────
    // Content capture — default off, opt-in via option or env var
    // ──────────────────────────────────────────────

    [Fact]
    public void ShouldCaptureContent_DefaultOptions_IsFalse()
    {
        Assert.False(LocalLLMsInstrumentation.ShouldCaptureContent(new LocalLLMsOptions()));
    }

    [Fact]
    public void ShouldCaptureContent_OptionEnabled_IsTrue()
    {
        Assert.True(LocalLLMsInstrumentation.ShouldCaptureContent(new LocalLLMsOptions { CaptureTelemetryContent = true }));
    }

    [Fact]
    public void ShouldCaptureContent_EnvironmentVariableOverride_IsTrue()
    {
        const string EnvVar = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";
        var original = Environment.GetEnvironmentVariable(EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(EnvVar, "true");
            Assert.True(LocalLLMsInstrumentation.ShouldCaptureContent(new LocalLLMsOptions()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVar, original);
        }
    }

    [Fact]
    public void AddPromptEvent_AttachesPromptText()
    {
        using var activity = new Activity("test").Start();

        activity.AddPromptEvent("what is the capital of France?");

        var evt = Assert.Single(activity.Events, e => e.Name == "gen_ai.prompt");
        Assert.Equal("what is the capital of France?", evt.Tags.Single(t => t.Key == "gen_ai.prompt").Value);
    }

    // ──────────────────────────────────────────────
    // GenerationResult — internal seam for buffered TTFT/token counts (D3)
    // ──────────────────────────────────────────────

    [Fact]
    public void GenerationResult_ExposesTextTokenCountsAndTimeToFirstToken()
    {
        var result = new GenerationResult("hello world", InputTokenCount: 4, OutputTokenCount: 2, TimeToFirstToken: TimeSpan.FromMilliseconds(42));

        Assert.Equal("hello world", result.Text);
        Assert.Equal(4, result.InputTokenCount);
        Assert.Equal(2, result.OutputTokenCount);
        Assert.Equal(TimeSpan.FromMilliseconds(42), result.TimeToFirstToken);
    }

    // ──────────────────────────────────────────────
    // End-to-end through IChatClient — cancellation vs failure classification
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_CancelledBeforeModelReady_EmitsQueuedThenCancelled_NeverError()
    {
        var downloader = Substitute.For<IModelDownloader>();
        await using var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var capture = SubscribeAllDataListener();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token));

        var activity = Assert.Single(capture.CompletedActivities);
        Assert.Collection(activity.Events,
            e => Assert.Equal("gen_ai.queued", e.Name),
            e => Assert.Equal("gen_ai.cancelled", e.Name));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        AssertTag(activity, "gen_ai.outcome", "cancelled");
    }

    [Fact]
    public async Task GetResponseAsync_ModelLoadFailure_EmitsQueuedThenFailed_WithErrorStatus()
    {
        var downloader = Substitute.For<IModelDownloader>();
        downloader.EnsureModelAsync(
            Arg.Any<ModelDefinition>(),
            Arg.Any<string?>(),
            Arg.Any<IProgress<ModelDownloadProgress>?>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(Path.GetTempPath()));

        await using var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        using var capture = SubscribeAllDataListener();

        // Path.GetTempPath() has no genai_config.json/model files -> real load failure, real exception.
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        var activity = Assert.Single(capture.CompletedActivities);
        Assert.Collection(activity.Events,
            e => Assert.Equal("gen_ai.queued", e.Name),
            e => Assert.Equal("gen_ai.failed", e.Name));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        AssertTag(activity, "gen_ai.outcome", "error");
        Assert.NotEqual("cancelled", GetTag(activity, "error.type"));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CancelledBeforeModelReady_EmitsQueuedThenCancelled()
    {
        var downloader = Substitute.For<IModelDownloader>();
        await using var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var capture = SubscribeAllDataListener();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token))
            {
            }
        });

        var activity = Assert.Single(capture.CompletedActivities);
        Assert.Collection(activity.Events,
            e => Assert.Equal("gen_ai.queued", e.Name),
            e => Assert.Equal("gen_ai.cancelled", e.Name));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task GetResponseAsync_CancelledScenario_RecordsGenerationCountMetric_WithCancelledOutcome()
    {
        var downloader = Substitute.For<IModelDownloader>();
        await using var client = new LocalChatClient(new LocalLLMsOptions(), downloader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var recordedOutcomes = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == LocalLLMsInstrumentation.MeterName && instrument.Name == "gen_ai.client.generation.count")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "gen_ai.outcome" && tag.Value is string outcome)
                {
                    recordedOutcomes.Add(outcome);
                }
            }
        });
        meterListener.Start();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token));

        Assert.Contains("cancelled", recordedOutcomes);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static void AssertTag(Activity activity, string key, string expectedValue)
    {
        var actual = GetTag(activity, key);
        Assert.Equal(expectedValue, actual);
    }

    private static string? GetTag(Activity activity, string key)
        => activity.TagObjects.FirstOrDefault(t => t.Key == key).Value?.ToString();

    private static ActivityCapture SubscribeAllDataListener() => new();

    /// <summary>
    /// Captures completed root activities emitted on <see cref="LocalLLMsInstrumentation.ActivitySourceName"/>
    /// for the lifetime of the instance.
    /// </summary>
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
}
