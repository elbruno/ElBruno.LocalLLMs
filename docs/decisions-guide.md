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

There is nothing to install and nothing to start. The package runs
[Laya](https://github.com/NandhaKishorM/laya) (Apache-2.0) **in-process on ONNX Runtime** — no
Python, no server, no network call at inference time.

The model weights are downloaded from HuggingFace the first time a client is used (about 800 MB)
and cached under `%LOCALAPPDATA%\ElBruno\LocalLLMs\decisions`, so the first call is slow and the
rest are not.

> Laya's authors publish PyTorch weights only, so every ONNX export of Laya is community-produced.
> The default, [`elbruno/laya-onnx`](https://huggingface.co/elbruno/laya-onnx), mirrors the
> [`inferenceprince/laya-onnx`](https://huggingface.co/inferenceprince/laya-onnx) export unmodified
> and was verified to reproduce the numbers that export's model card reports. Point at a different
> repository if you would rather pin your own:
>
> ```csharp
> options.ModelRepository = "your-org/your-laya-export";
> ```

To avoid the download entirely, point at a directory you have already populated with the ONNX
graph, the checkpoint config and the tokenizer:

```csharp
options.ModelPath = @"D:\models\laya";
```

## Installation

```bash
dotnet add package ElBruno.LocalLLMs.Decisions
```

## Quick start

```csharp
using ElBruno.LocalLLMs.Decisions;

using var client = new LayaOnnxDecisionClient();

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

- Some checkpoints ship temperature values outside the valid range. The English checkpoint's
  `choice:11+` bucket is `0.1006`, which sharpens the logits roughly tenfold. This package clamps
  such values into Laya's own `0.5`–`5.0` range and reports it per answer via
  `CalibrationClamped` — but **clamping bounds the damage, it does not fix the calibration**.
  See [Clamped calibration buckets](#clamped-calibration-buckets) below for what that leaves you with.
- `Confidence` can be low on an answer whose distribution is actually sharp, and vice versa.
- The English checkpoint stays confident on non-Latin scripts while being wrong; use a
  multilingual export if your input is not English.
- Reported accuracy on some held-out tasks (moderation, for example) is close to chance.

None of that makes the model useless — routing and triage worked well in practice — but it does
mean:

1. **Fit `DecisionThreshold` on your own labelled examples.** The 0.5 default is a starting point,
   not a decision boundary.
2. **Prefer the probability over `IsTrue`** so you can re-tune later without re-running inference.
3. **Measure before you ship.** Treat the model as a component you evaluate, not an oracle.

### Clamped calibration buckets

Laya fits a separate temperature for each `(question type, option count)` bucket, keyed as
`choice:2`, `choice:3-5`, `choice:6-10`, `choice:11+` and the `score:` / `noul:` equivalents. The
English checkpoint ships `choice:11+ = 0.1006`, far below Laya's own `TEMP_MIN` of `0.5`. Dividing
the logits by it multiplies them roughly tenfold before the softmax.

This package clamps that value to `0.5` and sets `CalibrationClamped` on the affected answer:

```csharp
var result = await client.ChooseAsync(ticket, twelveDepartments, "Route this ticket");

if (result.CalibrationClamped)
{
    // The ranking is still meaningful. The confidence number is not.
    logger.LogWarning("Confidence for {Choice} is not a fitted calibration.", result.Choice);
}
```

**Be clear about what the clamp does and does not buy you.** A temperature below `1.0` still
sharpens the distribution — `0.5` doubles the logits. Clamping only prevents the far more extreme
tenfold sharpening. Running the [DecisionCalibration sample](../src/samples/DecisionCalibration/)
against the same ticket shows what survives:

| Options | Bucket | Choice | Reported confidence | `CalibrationClamped` |
|---|---|---|---|---|
| 5 | `choice:3-5` | `billing` | 86.9% | `false` |
| 12 | `choice:11+` | `billing` | **100.0%** | `true` |

Both pick the right department. But the 12-option answer reports **saturated confidence** even
after clamping, because `0.5` still over-sharpens. So:

- **Use the ranking, not the number**, for choices with 11 or more options. `Choice` and the
  relative ordering of `Probabilities` are trustworthy; the magnitude is not.
- **`ChoiceOrNull(threshold)` is close to useless in this bucket** — a saturated probability clears
  every threshold you would plausibly set, so it will never abstain.
- **Prefer fewer options.** Splitting one 12-way choice into two smaller ones keeps you inside a
  bucket whose fitted temperature the checkpoint actually got right.
- If you need calibrated confidence over many options, fit your own temperature on labelled data
  and apply it to the probabilities yourself.

### fp16 batch variance

The published checkpoint stores fp16 weights. Asking a question on its own and asking the same
question inside a larger batch produce slightly different probabilities, because the batched matrix
multiplications accumulate in a different order. Measured with the same sample:

| Label | Alone | Batched with 3 others | Delta |
|---|---|---|---|
| `billing` | 0.93738423 | 0.93739642 | 1.22e-05 |
| `sales` | 0.03319702 | 0.03318152 | 1.55e-05 |
| `technical` | 0.02941875 | 0.02942205 | 3.31e-06 |

Deltas land between roughly `1e-6` and `1e-4` depending on the input and batch size. This is
accumulation noise, **not** a sign that markers are misaligned — the answer and the ordering are
unchanged.

It is far too small to flip a routing decision, but large enough to matter if you:

- **assert exact equality** in tests — compare with a tolerance (`1e-3` is comfortable);
- **cache or deduplicate on the probability value** — key on the label instead;
- **persist probabilities and diff them across runs** — expect churn in the low decimals;
- **hash a result for change detection** — round first, or hash only the choice.

Batching is still the right default: four questions in one pass cost about what one costs. Just
treat the probabilities as approximate, which the calibration caveats above already require.

## Dependency injection

```csharp
builder.Services.AddLocalDecisions(options =>
{
    options.ModelRepository = "elbruno/laya-onnx";
    options.DecisionThreshold = 0.7;
});
```

Then inject `IDecisionClient`. The client is thread-safe and registered as a singleton, because
loading the model is expensive and the loaded session is safe to share. Registration does not
touch the network — the model is loaded lazily on the first call.

## Options

| Option | Default | Notes |
|---|---|---|
| `ModelRepository` | `elbruno/laya-onnx` | The HuggingFace repository to download from. Mirrors the community `inferenceprince/laya-onnx` export unmodified. |
| `ModelPath` | `null` | A local directory holding the model files. When set, nothing is downloaded. |
| `CacheDirectory` | `%LOCALAPPDATA%\ElBruno\LocalLLMs\decisions` | Where downloads are cached. |
| `IntraOpNumThreads` | `null` | Threads used within a single ONNX operator. Worth pinning to a small number under concurrency, since the default fills every core for one call. |
| `DecisionThreshold` | `0.5` | Applied by `ProbabilityResult.IsTrue`. |

## Testing

`IDecisionClient` is a plain interface, so unit tests can implement it directly with whatever
distributions the test needs — no model download and no ONNX session:

```csharp
internal sealed class StubDecisionClient : IDecisionClient { /* ... */ }
```

## Error handling

`DecisionException` is thrown when the model cannot be downloaded or loaded, when the model
directory is incomplete, or when a question does not fit the model's token window. The message
says which of those happened and what to do about it.

```csharp
try
{
    var result = await client.EvaluateAsync(request);
}
catch (DecisionException ex)
{
    // Download failed, model directory incomplete, or too many options to score.
}
```

## Performance

Measured on CPU with the English checkpoint, 4 questions per request:

| | Latency |
|---|---|
| First ever call (download, ~800 MB) | minutes |
| First call after that (session load) | a few seconds |
| Warm, 4 questions, CPU | ~600 ms |

Latency scales with input length far more than with question count, which is why batching is close
to free.

## Sample

[`src/samples/LocalDecisions`](../src/samples/LocalDecisions/) triages three support tickets and
prints the routing decision, urgency score and both probabilities for each.

```bash
dotnet run --project src/samples/LocalDecisions

# or against a model directory you already have, skipping the download
dotnet run --project src/samples/LocalDecisions -- D:\models\laya
```

## Limitations

- **The ONNX export is community-produced.** Laya's authors ship PyTorch weights only. The default
  export was verified against its own model card, but it is not first-party.
- **Confidence is not reliably calibrated.** See the section above; this is the real constraint.
- **English checkpoint is English-only in practice.** It stays confident on non-Latin scripts while
  being wrong. Point `ModelRepository` at a multilingual export if your input is not English.
- **The first run downloads about 800 MB.** Pre-populate `ModelPath` in environments where that is
  not acceptable.
