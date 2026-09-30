# Blog Companion — Local Decisions Assets

Companion to [`blog-local-decisions.md`](blog-local-decisions.md).

## Suggested Titles (3)

1. **I Stopped Asking an LLM "Which Team Owns This Ticket?" — Now I Get an Answer in 600ms, Locally**
2. **System One for .NET: Typed, Local Decisions with Laya and ONNX**
3. **Four Questions, One Forward Pass: Local Decision Models in C#**

## Suggested Tags

- dotnet
- csharp
- ai
- local-llm
- onnx
- onnxruntime
- huggingface
- decision-model
- classification
- laya
- calibration

## Image Prompts Used

All prompts explicitly end with "no text, no letters" — AI image generators garble embedded text.
Generated with the `t2i` CLI using the `foundry-gpt-image-25-flare` provider at 1024×1024.

### 1) Hero image

**Prompt**
> "Split composition illustration: left side a glowing fast reflex arc suggesting instant intuitive judgement, right side a slow deliberate chain of gears suggesting step by step reasoning. Center a laptop with code on screen. Purple and orange accents, dark navy background, modern flat vector tech illustration, high contrast, clean, no text, no letters"

**Output file**
- `images/blog-local-decisions.png`

### 2) One forward pass

**Prompt**
> "A single glowing arrow passing straight through a neural network block in one clean sweep, emerging as three horizontal probability bars of different lengths. Contrasted against a faint looping spiral behind it suggesting repeated token by token generation. Purple and orange accents, dark navy background, modern flat vector tech illustration, minimal, no text, no letters, no numbers"

**Output file**
- `docs/blog/assets/local-decisions/one-forward-pass.png`

### 3) Calibration clamp

**Prompt**
> "A thermometer style temperature dial turned far too low, causing three probability bars beside it to collapse so one bar saturates to full height while the others flatten to nothing. A warning shield icon clamps the dial back to a safe zone. Orange warning accents, purple bars, dark navy background, modern flat vector infographic style, minimal, no text, no letters, no numbers"

**Output file**
- `docs/blog/assets/local-decisions/calibration-clamp.png`

## Alt Text

- **blog-local-decisions.png**: "Local decisions — System One fast intuition versus System Two deliberate reasoning, with a laptop running C# in the middle."
- **one-forward-pass.png**: "A single arrow passing through a neural network in one sweep, emerging as probability bars, contrasted with a looping generation spiral."
- **calibration-clamp.png**: "A temperature dial clamped by a warning shield, with probability bars collapsing into saturation."

## Fact-check notes

Every number in the post is from a live run on this repo at `v0.22.0`. Do not edit these without re-running:

| Claim | Source |
|---|---|
| 98.4% / 73.7% / 64.9% routing confidence | `dotnet run --project src/samples/LocalDecisions` |
| 586 ms / 595 ms / 526 ms for 4 batched questions | same run |
| 244 / 260 / 248 input tokens | same run |
| 86.9% (5 options) vs 100.0% (12 options, clamped) | `docs/decisions-guide.md` → *Clamped calibration buckets* |
| fp16 deltas `1.22e-05` / `1.55e-05` / `3.31e-06` | `docs/decisions-guide.md` → *fp16 batch variance* |
| `choice:11+ = 0.1006`, `TEMP_MIN = 0.5` | English Laya checkpoint runtime config |
| ~800 MB first-run download | `src/samples/LocalDecisions/Program.cs` header |
| 79 tests | `ElBruno.LocalLLMs.Decisions.Tests` |
