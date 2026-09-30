# Local Decisions Guide

`ElBruno.LocalLLMs.Decisions` runs a **System One** decision model on your machine. Instead of
generating text, it returns a *typed* answer — a choice, a score, or a probability — together with
the full distribution behind it, in a single forward pass.

That makes it fast enough to sit on a request path and cheap enough to call on every message,
which is exactly where a chat model is the wrong tool.

## Decision model vs. chat model

| | Chat model (`IChatClient`) | Decision model (`IDecisionClient`) |
|---|---|---|
| Output | free text, token by token | one typed answer + probabilities |
| Passes | autoregressive, one token at a time | a single forward pass |
| Typical latency | seconds | tens to hundreds of milliseconds |
| Failure mode | plausible prose that is wrong | a flat distribution you can detect |
| Good for | writing, summarising, reasoning | routing, triage, moderation, intent |

The two compose well: use a decision model to decide *whether* and *where*, then hand the work to a
chat model when you actually need prose.

## Running the model

The package talks to [Laya](https://github.com/NandhaKishorM/laya) (Apache-2.0) over the Jev
System One wire protocol.

```bash
pip install "laya[serve]"
python -m laya.serve
```

That listens on `http://127.0.0.1:8000` and downloads the English checkpoint on first run.
Useful environment variables:

| Variable | Default | Purpose |
|---|---|---|
| `LAYA_HOST` / `LAYA_PORT` | `127.0.0.1` / `8000` | Bind address |
| `LAYA_MODELS` | all | `english`, `multilingual`, or `typed-decisions` |
| `LAYA_DEVICE` | auto | `cpu` or `cuda` |
| `LAYA_API_KEY` | none | Require a bearer token |
| `LAYA_MAX_CONCURRENT` | 16 | Concurrent request cap |

Check it is alive:

```bash
curl http://127.0.0.1:8000/health
```

## Installation

```bash
dotnet add package ElBruno.LocalLLMs.Decisions
```

## Quick start

```csharp
using ElBruno.LocalLLMs.Decisions;

using var client = new LayaDecisionClient();

ChoiceResult team = await client.ChooseAsync(
    "My invoice charged me twice and I want my money back.",
    new Dictionary<string, string?>
    {
        ["billing"]   = "Payment, invoice and refund problems",
        ["technical"] = "Bugs, outages and API errors",
        ["sales"]     = "Pricing, upgrades and new purchases"
    },
    "Which team should handle this support ticket?");

Console.WriteLine(team.Choice);       // billing
Console.WriteLine(team.Probability);  // 0.9824
```

## Ask several questions at once

Every question in a `DecisionRequest` is answered in the **same forward pass**, so four questions
cost roughly what one costs. Prefer batching over sequential calls.

```csharp
DecisionResult result = await client.EvaluateAsync(
    new DecisionRequest(ticket)
        .Choose("team", teams, "Which team should handle this?")
        .Score("urgency", new[] { "no rush", "normal", "urgent", "critical" })
        .Ask("refund", "The customer is asking for a refund.")
        .Ask("angry", "The customer is angry or frustrated."));

string  route   = result.Choice("team").Choice;
double  urgency = result.Score("urgency").Score;
bool    refund  = result.Probability("refund").IsTrue;
```

Read answers back with the same name you used to add the question. Reading a name that was not
asked throws `KeyNotFoundException`; reading it as the wrong type throws `InvalidOperationException`.

## The three question types

### `Choose` — pick one label

Returns the selected label plus a probability for every option.

**Option descriptions matter.** The model reads the option text, so a bare `"billing"` is weaker
than `"billing"` described as `"payment, invoice and refund problems"`. There is an overload that
takes just labels, but prefer the dictionary when accuracy matters.

### `Score` — rate against a rubric

Takes 2–10 ordered levels and returns a **fractional expected position**, not an index. A score of
`1.67` across four levels sits between "normal" and "urgent" and means the model is genuinely
between them.

Each level should be self-contained, because the model sees the level text rather than its
position. `"urgent, answer within an hour"` works better than `"3"`.

Use `Score` for a continuous reading and `MostLikelyLevel` when you need a single bucket — they can
differ when the distribution is skewed or bimodal.

### `Ask` — probability of a proposition

Returns a probability rather than a boolean, deliberately. `IsTrue` applies
`DecisionOptions.DecisionThreshold` (default `0.5`), and `AtThreshold(x)` re-applies a different
threshold without calling the model again.

Phrase the proposition so that "true" is unambiguous: `"The customer is asking for a refund."`
rather than `"Is this about refunds or something else?"`.

## Act on probabilities, not just labels

The highest-probability label is always returned, even when the model is torn. Gate on the
probability so ambiguous inputs fall through to a human instead of being confidently misrouted:

```csharp
string route = team.ChoiceOrNull(0.6) ?? "human-review";
```

## Calibration — read this before you trust a threshold

**Laya's public checkpoints are not reliably calibrated, and this is the single biggest risk in
using them.** Concretely, observed on the English checkpoint:

- Laya itself warns at startup that some checkpoints ship temperature values outside the valid
  range, and explicitly says to treat the affected confidences as uncalibrated.
- `Confidence` can be low on an answer whose distribution is actually sharp, and vice versa.
- The English checkpoint stays confident on non-Latin scripts while being wrong; use the
  multilingual checkpoint if your input is not English.
- Reported accuracy on some held-out tasks (moderation, for example) is close to chance.

None of that makes the model useless — routing and triage worked well in practice — but it does
mean:

1. **Fit `DecisionThreshold` on your own labelled examples.** The 0.5 default is a starting point,
   not a decision boundary.
2. **Prefer the probability over `IsTrue`** so you can re-tune later without re-running inference.
3. **Measure before you ship.** Treat the model as a component you evaluate, not an oracle.

## Dependency injection

```csharp
builder.Services.AddLocalDecisions(options =>
{
    options.Endpoint = new Uri("http://127.0.0.1:8000");
    options.DecisionThreshold = 0.7;
});
```

Then inject `IDecisionClient`. The client is thread-safe and registered as a singleton.

## Options

| Option | Default | Notes |
|---|---|---|
| `Endpoint` | `http://127.0.0.1:8000` | **Must be loopback.** A remote address is rejected so prompts cannot leave the machine by a config mistake. |
| `ApiKey` | empty | Only needed when the server was started with `LAYA_API_KEY`. |
| `Model` | `null` | Leave null to let Laya route per request. An unknown id degrades *silently* to routing, so a typo fails quietly. |
| `Timeout` | 30s | The first call after startup loads the checkpoint and is slower. |
| `DecisionThreshold` | `0.5` | Applied by `ProbabilityResult.IsTrue`. |

## Testing

Inject a fake transport through the `IJevDecisionClient` constructor overload — no server needed:

```csharp
var client = new LayaDecisionClient(myStubJevClient);
```

The same overload is available on `AddLocalDecisions`.

## Error handling

`DecisionException` is thrown when the server is unreachable, times out, or answers with a type
that does not match the question asked. The message names the endpoint and how to start the server.

```csharp
try
{
    var result = await client.EvaluateAsync(request);
}
catch (DecisionException ex)
{
    // Server down, still loading, or wrong port.
}
```

## Performance

Measured on CPU with the English checkpoint, 4 questions per request:

| | Latency |
|---|---|
| First call (checkpoint load) | several seconds |
| Warm, 4 questions, CPU | ~600 ms |

Latency scales with input length far more than with question count, which is why batching is close
to free.

## Sample

[`src/samples/LocalDecisions`](../src/samples/LocalDecisions/) triages three support tickets and
prints the routing decision, urgency score and both probabilities for each.

```bash
python -m laya.serve                                   # terminal 1
dotnet run --project src/samples/LocalDecisions        # terminal 2
```

## Limitations

- **Requires a running Python server.** There is no in-process ONNX path yet; the model runs in
  `laya-serve` and is reached over loopback HTTP.
- **Loopback only, by design.** Point it at a remote host and construction fails.
- **No model discovery.** Laya does not expose `/v1/models`.
- **English checkpoint is English-only in practice.** Use `LAYA_MODELS=multilingual` otherwise.
