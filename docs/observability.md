# Observability

`ElBruno.LocalLLMs` now emits generation-lifecycle diagnostics through `ActivitySource`, `DiagnosticSource`, and `Meter` using the shared name `ElBruno.LocalLLMs`.

## What gets emitted

Each `IChatClient` request creates one root `Activity` with lifecycle events for:

- `gen_ai.queued`
- `gen_ai.model_ready`
- `gen_ai.generation_started`
- `gen_ai.first_token`
- `gen_ai.completed`
- `gen_ai.cancelled`
- `gen_ai.failed`

The library also records these metrics:

- `gen_ai.client.operation.duration`
- `gen_ai.client.time_to_first_token`
- `gen_ai.client.token.usage`
- `gen_ai.client.generation.count`

## Privacy defaults

Prompt and completion text are **excluded by default**.

To opt in:

```csharp
var options = new LocalLLMsOptions
{
    CaptureTelemetryContent = true
};
```

Or set:

```bash
OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

## OpenTelemetry wiring

```csharp
using ElBruno.LocalLLMs.Diagnostics;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(LocalLLMsInstrumentation.ActivitySourceName);
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(LocalLLMsInstrumentation.MeterName);
    });
```

## Aspire dashboard

When your app is already exporting OpenTelemetry data, Aspire can display these activities without any library-specific adapter. Register the source and meter above, then let your normal Aspire/OpenTelemetry pipeline export the telemetry.

## Semantics

- Cancellation is recorded as `gen_ai.cancelled` with `ActivityStatusCode.Unset`.
- Failures are recorded as `gen_ai.failed` with `ActivityStatusCode.Error`.
- Time-to-first-token is captured for both buffered and streaming generation paths.
