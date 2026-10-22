# Kev 0.8B: native local decision inference

`ElBruno.LocalLLMs.Decisions` can execute the original
[`jaredpalmer/kev-0.8b`](https://huggingface.co/jaredpalmer/kev-0.8b) decision
checkpoint entirely in-process on the CPU. Ollama's catalog identified the
candidate; **Ollama is not the backend, conversion source, or runtime dependency**.
Laya remains the default for `AddLocalDecisions()`.

Kev uses a trained pointer head, not generated text or a next-token probability
guess. Its original Apache-2.0 LoRA adapter and pointer head are merged/exported
with the Apache-2.0 Qwen3.5 0.8B base. The exact checkpoint temperature
`2.3510958125672174` is embedded in the graph.

## Automatic download and cache

Starting with **v0.24.1**, no conversion or local path is needed:

```csharp
services.AddLocalKevDecisions();
// Or, without DI:
using var client = new KevOnnxDecisionClient();
```

The first decision call downloads the preconverted CPU FP32 export from
[`elbruno/kev-0.8b-onnx`](https://huggingface.co/elbruno/kev-0.8b-onnx),
pinned to commit `deec2b78a30db0c4d00ee36386e87be5382dafd7`.
It needs internet access and about **3.1 GB** of disk space. Construction and
DI registration are lazy and do not touch the network.

Files are cached below
`%LOCALAPPDATA%\ElBruno\LocalLLMs\decisions\kev-<source-and-revision-hash>`
on Windows, or the equivalent .NET local application data directory on other
platforms. A new client reuses a complete cache without HTTP requests.
The source and export revision get distinct cache identities.

Acquisition verifies required files against manifest SHA-256 hashes, including
external weights and tokenizer, and retains licenses/provenance. A completion
receipt is written only after successful validation. Later loads compare file
sizes and modification timestamps to that receipt rather than rehashing 3 GB
on every startup. Missing/changed cache files are revalidated and, if needed,
downloaded again. This detects incomplete/modified files, not all possible
disk corruption that preserves both size and timestamp.

Cancellation and interrupted transfers do not mark the cache complete. Transfers
use the downloader's atomic-write/resume support; clients/processes sharing a
cache coordinate acquisition. Errors remain explicit, with no conversion,
alternate model, or Ollama fallback.

```csharp
services.AddLocalKevDecisions(options =>
{
    options.CacheDirectory = @"D:\model-cache";
    options.IntraOpNumThreads = 4;
    options.DownloadProgress = new Progress<ElBruno.HuggingFace.DownloadProgress>(
        progress => Console.WriteLine($"{progress.CurrentFile}: {progress.PercentComplete:F1}%"));
});
```

`KevDecisionOptions.ModelRepository` and `ModelRevision` can select another
compatible published export; revision must be its full immutable 40-character
commit SHA, not `main` or the original PyTorch checkpoint's revision.
The exported graph still has to satisfy the pinned Kev 0.8B runtime contract.

For explicitly offline/local-only operation, the existing API is unchanged:

```csharp
services.AddLocalKevDecisions(@"D:\models\kev-0.8b");
// Or configure options.ModelPath, or construct:
using var local = new KevOnnxDecisionClient(@"D:\models\kev-0.8b");
```

An explicit `ModelPath` always disables acquisition and cache repair. Missing
files fail with an actionable error instead of silently downloading.
Version 0.24.0 supports only this explicit-path mode.

## Advanced: convert your own export

**Normal consumers skip this section.** Conversion is for publishing a new
compatible export or preparing original weights yourself.

Use an isolated Python environment. This recipe was executed on Windows x64
with Python 3.14.4, CPU PyTorch, and the pinned packages in
`scripts/requirements-kev.txt`. Linux/macOS execution is not claimed by this
validation.

```powershell
python -m venv .venv-kev
.\.venv-kev\Scripts\python.exe -m pip install -r scripts\requirements-kev.txt
.\.venv-kev\Scripts\python.exe scripts\convert_kev.py --output-dir D:\models\kev-0.8b
```

Choose an empty output directory. The converter downloads the pinned original
checkpoint, base weights, and official reference loader at the revisions below.
It loads the checkpoint's trusted original PyTorch head during conversion,
merges the LoRA in FP32, exports the backbone with the released ORT GenAI builder,
and embeds the pointer head and fresh attention caches in `decision.onnx`.
No Python code, PyTorch pickle, or adapter is loaded by the .NET client.

Conversion requires several GB of downloads and temporary storage; the final
FP32 export is about 3.1 GB, dominated by `model.onnx.data`. Keep the export
directory outside the repository. Weights are not bundled in the NuGet package.
This is an FP32 baseline, not a quantized export or a GPU backend.

The converter compares all probabilities on six labeled cases with the official
PyTorch implementation. A maximum absolute error of `1e-4` or greater fails
conversion. Only after successful parity and license/provenance collection does
it write `kev_config.json`, the completion marker required by the client.
The manifest includes tool versions and file hashes. A failed conversion can
leave partial assets without that marker; inspect/remove only that output
directory before trying again.

| Component | Pinned identity |
|---|---|
| Kev 0.8B adapter/head | `bf75a6a8848ea6960ff2ed108d9ed44c2941174f` |
| `Qwen/Qwen3.5-0.8B-Base` | `dc7cdfe2ee4154fa7e30f5b51ca41bfa40174e68` |
| [Official Kev reference](https://github.com/jaredpalmer/kev) | `2074cc44256200bb37540103fa4572901faa10f3` |
| Export format | `kev-0.8b-prefill-v1` |
| Native ONNX Runtime | `1.31.0`, required and validated |

The ORT GenAI 0.17.0 exporter CLI fails while constructing help on Python 3.14.
The converter uses the released builder's Python functions instead, without
patching the installed package. ORT 1.28.0 cannot load the resulting graph
because its `com.microsoft:LinearAttentionGate` operator is unavailable.
The Decisions package therefore references ORT 1.31.0.

## Use from .NET

```powershell
dotnet add package ElBruno.LocalLLMs.Decisions
```

```csharp
using ElBruno.AI.Decisions;
using ElBruno.LocalLLMs.Decisions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalKevDecisions();
using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IDecisionClient>();

var decision = await client.ChooseAsync(
    "I need help with a billing problem.",
    "Which team should handle this support ticket?",
    new Dictionary<string, string?>
    {
        ["billing"] = "Payment, invoice and refund problems",
        ["technical"] = "Software bugs, outages and API errors",
        ["sales"] = "Pricing, upgrades and new purchases",
    });
Console.WriteLine(decision.Choice);
Console.WriteLine(decision.Probabilities["billing"]);
```

For direct ownership, construct `new KevOnnxDecisionClient()` or pass a
`KevDecisionOptions` instance and dispose it when finished. The local-path
constructor's optional second argument still sets CPU intra-op threads,
default four. Loading is lazy, requests are serialized, acquisition and native
inference are cancellable, and disposal waits for the active call.

`AddLocalKevDecisions()` registers the concrete client and shared
`IDecisionClient` against the same singleton. As with Laya, the first registered
shared provider wins. Kev does not implement the Laya-specific
`ILayaDecisionClient`, batching API, or `CalibrationClamped` diagnostic.
Do not point Laya's `ModelRepository` at Kev.

The shared `ChooseAsync`, `ScoreAsync`, and `AssessAsync` preserve option order
and labels. Scores are expected zero-based rubric positions. Assessment uses
the original `no`, `yes` order and returns the probability of `yes`.
`Confidence` follows Kev's normalized top-probability advantage over uniform,
not a calibrated accuracy guarantee.

Each request makes one full causal prefill. Its row contains state, instruction,
option-start/option-end markers, and the final decision marker, without a chat
template or autoregressive generation. Qwen NFC/byte-level BPE tokenization and
added-token matching are preserved. Caller-supplied `<|name|>` strings are
sanitized as in the official implementation so they cannot inject delimiters.
The backend rejects rows over **8192 total tokens**, including every option and
marker, rather than silently truncating them, and caps options at 255.
The shared contract's own request validation also applies.

Keep `decision.onnx`, `model.onnx.data`, `tokenizer.json`, and `kev_config.json`
together. Retain both license files and provenance when distributing converted
weights. For an explicit local path, the client checks required files, export
contract and original revisions, but does not hash or authenticate local
weights. Use trusted local exports. Automatic acquisition additionally verifies
the manifest hashes and pins the published export revision as described above.

## Reproduce automatic acquisition and offline reuse

```powershell
dotnet run --project src\samples\SharedDecisions -c Release -- --kev
$env:RUN_KEV_DOWNLOAD_TESTS = 'true'
$env:KEV_DOWNLOAD_CACHE = 'D:\scratch\kev-empty-cache'
dotnet test src\tests\ElBruno.LocalLLMs.Decisions.Tests -c Release --filter FullyQualifiedName~KevDownloadTests --logger 'console;verbosity=detailed'
```

The opt-in download test requires a fresh cache. It downloads the public pinned
export, checks all six reference cases through Choice/Score/Assessment, disposes
the client and opens a new one with HTTP blocked. The second client must produce
the same probabilities without additional HTTP attempts. The guard validates
the .NET HTTP path, not OS-level isolation of arbitrary native sockets.
This test is skipped by default to avoid implicit large downloads in unit/CI runs.

## Reproduce the native reference comparison

```powershell
$env:RUN_KEV_TESTS = 'true'
$env:KEV_MODEL_PATH = 'D:\models\kev-0.8b'
dotnet test src\tests\ElBruno.LocalLLMs.Decisions.Tests -c Release --logger 'console;verbosity=detailed'
dotnet run --project src\samples\SharedDecisions -c Release -- --kev D:\models\kev-0.8b
```

The live test uses free text through the shared client, checks exact token IDs
and marker positions against the original reference, and compares every
probability for Choice, Score, and Assessment. Additional tokenizer cases cover
normalization, non-Latin text, supplementary Unicode, whitespace, ordinary added
tokens, and attempted delimiter injection.

The observed maximum probability error was approximately `5.614e-6` against the
FP32 PyTorch reference, below the mandatory `1e-4` gate. Six synthetic questions
are not an accuracy benchmark or a calibration study. No support or parity is
claimed for Kev 4B/9B/27B, quantization, multimodal inputs, or other trained heads.
See [model evaluation and remaining candidates](decision-model-validation.md).
