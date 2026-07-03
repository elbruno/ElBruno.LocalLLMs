using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ElBruno.LocalLLMs.Diagnostics;

/// <summary>
/// Central OpenTelemetry/<see cref="DiagnosticSource"/> instrumentation surface for
/// ElBruno.LocalLLMs generation lifecycle diagnostics.
/// </summary>
/// <remarks>
/// One root <see cref="Activity"/> is created per <c>GetResponseAsync</c> /
/// <c>GetStreamingResponseAsync</c> call. Lifecycle stages (queued, model ready,
/// generation started, first token, completed, cancelled, failed) are recorded as
/// <see cref="ActivityEvent"/> entries on that activity, and duration/token-usage/outcome
/// are also recorded as metrics on <see cref="Meter"/>. Register the source/meter with
/// OpenTelemetry:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t =&gt; t.AddSource(LocalLLMsInstrumentation.ActivitySourceName))
///     .WithMetrics(m =&gt; m.AddMeter(LocalLLMsInstrumentation.MeterName));
/// </code>
/// Prompt and completion text are <b>never</b> attached unless
/// <see cref="LocalLLMsOptions.CaptureTelemetryContent"/> is <see langword="true"/> (or the
/// <c>OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</c> environment variable is set to
/// <c>true</c>) — this is opt-in by design to protect user privacy.
/// </remarks>
public static class LocalLLMsInstrumentation
{
    /// <summary>Name of the <see cref="ActivitySource"/> used for generation lifecycle tracing.</summary>
    public const string ActivitySourceName = "ElBruno.LocalLLMs";

    /// <summary>Name of the <see cref="Meter"/> used for generation lifecycle metrics.</summary>
    public const string MeterName = "ElBruno.LocalLLMs";

    /// <summary>Value used for the <c>gen_ai.system</c> tag on every root activity.</summary>
    internal const string SystemName = "elbruno.local_llms";

    /// <summary>Value used for the <c>gen_ai.operation.name</c> tag on every root activity.</summary>
    internal const string OperationChat = "chat";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> s_operationDuration = Meter.CreateHistogram<double>(
        "gen_ai.client.operation.duration", unit: "s", description: "Total duration of a generation call.");

    private static readonly Histogram<double> s_timeToFirstToken = Meter.CreateHistogram<double>(
        "gen_ai.client.time_to_first_token", unit: "s", description: "Time from generation start to the first produced token.");

    private static readonly Histogram<long> s_tokenUsage = Meter.CreateHistogram<long>(
        "gen_ai.client.token.usage", unit: "{token}", description: "Input/output token counts per generation call.");

    private static readonly Counter<long> s_generationCount = Meter.CreateCounter<long>(
        "gen_ai.client.generation.count", description: "Count of generation calls by outcome.");

    /// <summary>
    /// Lifecycle <see cref="ActivityEvent"/> names emitted on the root generation activity.
    /// This is the machine-readable contract other components (and tests) assert against.
    /// </summary>
    internal static class EventNames
    {
        internal const string Queued = "gen_ai.queued";
        internal const string ModelReady = "gen_ai.model_ready";
        internal const string GenerationStarted = "gen_ai.generation_started";
        internal const string FirstToken = "gen_ai.first_token";
        internal const string Completed = "gen_ai.completed";
        internal const string Cancelled = "gen_ai.cancelled";
        internal const string Failed = "gen_ai.failed";
        internal const string Prompt = "gen_ai.prompt";
        internal const string Completion = "gen_ai.completion";
    }

    /// <summary>Tag/attribute names used on the root generation activity.</summary>
    internal static class TagNames
    {
        internal const string System = "gen_ai.system";
        internal const string OperationName = "gen_ai.operation.name";
        internal const string RequestModel = "gen_ai.request.model";
        internal const string RequestMaxTokens = "gen_ai.request.max_tokens";
        internal const string RequestTemperature = "gen_ai.request.temperature";
        internal const string RequestTopP = "gen_ai.request.top_p";
        internal const string RequestTopK = "gen_ai.request.top_k";
        internal const string ResponseModel = "gen_ai.response.model";
        internal const string UsageInputTokens = "gen_ai.usage.input_tokens";
        internal const string UsageOutputTokens = "gen_ai.usage.output_tokens";
        internal const string ExecutionProvider = "elbruno.execution_provider";
        internal const string Streaming = "elbruno.streaming";
        internal const string TimeToFirstToken = "gen_ai.server.time_to_first_token";
        internal const string Outcome = "gen_ai.outcome";
        internal const string ErrorType = "error.type";
        internal const string Prompt = "gen_ai.prompt";
        internal const string Completion = "gen_ai.completion";
    }

    /// <summary>Values for the <see cref="TagNames.Outcome"/> tag / generation-count metric dimension.</summary>
    internal static class Outcomes
    {
        internal const string Completed = "completed";
        internal const string Cancelled = "cancelled";
        internal const string Error = "error";
    }

    /// <summary>
    /// Starts the single root <see cref="Activity"/> for a generation call ("chat {model}",
    /// OTel GenAI naming convention) and stamps the base request tags. Returns
    /// <see langword="null"/> when there is no listener (near-zero cost).
    /// </summary>
    internal static Activity? StartChatActivity(string modelId, bool streaming, ExecutionProvider provider)
    {
        var activity = ActivitySource.StartActivity($"chat {modelId}", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TagNames.System, SystemName);
        activity.SetTag(TagNames.OperationName, OperationChat);
        activity.SetTag(TagNames.RequestModel, modelId);
        activity.SetTag(TagNames.ExecutionProvider, provider.ToString());
        activity.SetTag(TagNames.Streaming, streaming);
        return activity;
    }

    /// <summary>
    /// Determines whether prompt/completion text may be attached to telemetry. Default is
    /// off; opt in via <see cref="LocalLLMsOptions.CaptureTelemetryContent"/> or the OTel
    /// convention environment variable <c>OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</c>.
    /// </summary>
    internal static bool ShouldCaptureContent(LocalLLMsOptions options)
    {
        if (options.CaptureTelemetryContent)
        {
            return true;
        }

        var envOverride = Environment.GetEnvironmentVariable("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT");
        return string.Equals(envOverride, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static void AddQueuedEvent(this Activity? activity)
        => activity?.AddEvent(new ActivityEvent(EventNames.Queued));

    internal static void AddModelReadyEvent(this Activity? activity)
        => activity?.AddEvent(new ActivityEvent(EventNames.ModelReady));

    internal static void AddGenerationStartedEvent(this Activity? activity)
        => activity?.AddEvent(new ActivityEvent(EventNames.GenerationStarted));

    internal static void AddFirstTokenEvent(this Activity? activity, TimeSpan timeToFirstToken)
    {
        if (activity is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection { [TagNames.TimeToFirstToken] = timeToFirstToken.TotalSeconds };
        activity.AddEvent(new ActivityEvent(EventNames.FirstToken, tags: tags));
    }

    internal static void AddCompletedEvent(this Activity? activity, int inputTokens, int outputTokens)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(TagNames.UsageInputTokens, inputTokens);
        activity.SetTag(TagNames.UsageOutputTokens, outputTokens);
        activity.SetTag(TagNames.Outcome, Outcomes.Completed);
        activity.AddEvent(new ActivityEvent(EventNames.Completed));
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    internal static void AddCancelledEvent(this Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(TagNames.Outcome, Outcomes.Cancelled);
        activity.SetTag(TagNames.ErrorType, Outcomes.Cancelled);
        activity.AddEvent(new ActivityEvent(EventNames.Cancelled));
        // Cancellation is not a failure — leave the activity status Unset rather than Error.
        activity.SetStatus(ActivityStatusCode.Unset);
    }

    internal static void AddFailedEvent(this Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        var errorType = exception.GetType().FullName ?? exception.GetType().Name;
        activity.SetTag(TagNames.Outcome, Outcomes.Error);
        activity.SetTag(TagNames.ErrorType, errorType);
        var tags = new ActivityTagsCollection { [TagNames.ErrorType] = errorType };
        activity.AddEvent(new ActivityEvent(EventNames.Failed, tags: tags));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    internal static void AddPromptEvent(this Activity? activity, string prompt)
    {
        if (activity is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection { [TagNames.Prompt] = prompt };
        activity.AddEvent(new ActivityEvent(EventNames.Prompt, tags: tags));
    }

    internal static void AddCompletionEvent(this Activity? activity, string completion)
    {
        if (activity is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection { [TagNames.Completion] = completion };
        activity.AddEvent(new ActivityEvent(EventNames.Completion, tags: tags));
    }

    /// <summary>
    /// Records the metrics side of a completed generation call (duration, TTFT when
    /// available, token usage, and the outcome counter). Cheap no-ops when there are no
    /// meter listeners.
    /// </summary>
    internal static void RecordMetrics(
        string modelId,
        ExecutionProvider provider,
        bool streaming,
        string outcome,
        TimeSpan duration,
        TimeSpan? timeToFirstToken,
        int? inputTokens,
        int? outputTokens)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TagNames.RequestModel, modelId),
            new(TagNames.ExecutionProvider, provider.ToString()),
        };

        s_operationDuration.Record(duration.TotalSeconds, tags);

        if (timeToFirstToken.HasValue)
        {
            s_timeToFirstToken.Record(timeToFirstToken.Value.TotalSeconds, tags);
        }

        if (inputTokens.HasValue)
        {
            s_tokenUsage.Record(inputTokens.Value, [.. tags, new KeyValuePair<string, object?>("gen_ai.token.type", "input")]);
        }

        if (outputTokens.HasValue)
        {
            s_tokenUsage.Record(outputTokens.Value, [.. tags, new KeyValuePair<string, object?>("gen_ai.token.type", "output")]);
        }

        s_generationCount.Add(1, [.. tags, new KeyValuePair<string, object?>(TagNames.Outcome, outcome)]);
    }
}
