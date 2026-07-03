# Streaming Cancellation

`LocalChatClient.GetStreamingResponseAsync()` supports cooperative cancellation for realtime
experiences such as voice barge-in.

## Semantics

- Cancellation is observed at token boundaries.
- The streaming path checks the `CancellationToken` before generation, immediately after each
  generated token, and again just before yielding the token to the caller.
- Once cancellation is observed, the stream stops with `OperationCanceledException`.
- No additional `ChatResponseUpdate` is emitted after cancellation has been observed.
- A cancelled request does not poison the client; the same `LocalChatClient` instance can serve
  the next request normally.

## Voice barge-in pattern

```csharp
using var cts = new CancellationTokenSource();

var streamingTask = Task.Run(async () =>
{
    try
    {
        await foreach (var update in client.GetStreamingResponseAsync(
            messages,
            cancellationToken: cts.Token))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                Console.Write(update.Text);
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected when the user interrupts the assistant.
    }
});

// User starts talking again -> barge in
cts.Cancel();
await streamingTask;

var followUp = await client.GetResponseAsync([
    new ChatMessage(ChatRole.User, "Continue with the new request.")
]);
```

## Latency expectations

Cancellation is designed to stop at the next token boundary rather than in the middle of native
token generation.

- Deterministic unit tests verify that no stale token is yielded after cancellation.
- The real-model integration test enforces a conservative upper bound of **2 seconds** from the
  cancellation request to stream termination.

The exact latency still depends on the active model, hardware, and token generation speed.

## Telemetry

Cancellation is surfaced through the normal observability contract:

- Activity event: `gen_ai.cancelled`
- Activity status: `ActivityStatusCode.Unset`
- Outcome tag / metric dimension: `gen_ai.outcome=cancelled`

See [observability.md](observability.md) for the full OpenTelemetry contract.
