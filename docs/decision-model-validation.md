# Local decision inference validation

Investigation for [issue #58](https://github.com/elbruno/ElBruno.LocalLLMs/issues/58),
performed October 22, 2026. This is a small synthetic smoke evaluation, not an
accuracy benchmark or a calibration study.

## Outcome

The existing Laya adapter runs all three shared `IDecisionClient` operations
in-process. No second Laya adapter is needed. The SharedDecisions sample now
demonstrates Score as well as Choice, Assessment and chat routing.

The added opt-in integration test checks exact semantic labels, complete finite
distributions, sum-to-one error at most `1e-10`, zero-based rubric expectation,
true-proposition mapping, DI singleton identity, and the rich API's
`CalibrationClamped` diagnostic. Rich/neutral repeated inference comparisons use
`1e-3` tolerance. It includes labeled contrastive cases and logs a known failing
negation case separately from the adapter's contract assertions.

The original **Kev 0.8B checkpoint is now a separate native backend**, exposed
through `KevOnnxDecisionClient` and `AddLocalKevDecisions()`. Its LoRA and trained
pointer head were converted to FP32 ONNX, then executed from free text through
the shared .NET client. All probabilities on six cases match the original
PyTorch reference within `1e-4`; maximum observed error was approximately
`5.614e-6`. See the [reproducible conversion and usage guide](kev-guide.md).
Ollama is only a catalog of candidates, never the backend or conversion input.

Tev1 is **not enabled**: its inspected weight license is unresolved. The initial
exporter CLI attempt failed before graph construction; calling the released
builder functions bypasses that CLI problem, as subsequently proved with Kev.
No Tev1 weights, ONNX export, native load, scoring, quantization-drift or
reference-parity result is claimed.

## Laya identity and environment

Acquired `elbruno/laya-onnx`, whose Hugging Face API reported revision
`dea9bba5c04d083652eeb7bc9f6b295f04a04529`. Acquisition requested `main`, not a
pinned revision, so the content hashes below are the authoritative identity of
this run. The export uses external fp16 weights.

| File | SHA-256 |
|---|---|
| `model.onnx` | `75d37762009e57b03b28877d330a2a466acd576eb444b0fdd00ee2a3221f8761` |
| `model.onnx.data` | `356764eb3ab760468479e6fa732217d8ccaa84ab38f2ca9e2ed174ae22aaf1b4` |
| `rl_agent_config.json` | `ae287b56bbcf5f8c4f4541ae9dfd00c914c4c48b940b8398c3058af37ba92bbd` |
| `tokenizer/tokenizer.json` | `6c8aaa9a542084f2457eab775d4eeb51f92a70c0fd9de28d5edb0ddec3c08d30` |

Windows x64, build 26300; AMD EPYC 74F3, 18 exposed cores / 36 logical processors,
approximately 440 GiB RAM. CPU execution provider, four intra-op threads, ONNX
Runtime NuGet 1.28.0 for the original measurements below, shared Decisions 0.6.2,
.NET runtime 10.0.13. Both live model tests subsequently passed with ORT 1.31.0.
The machine
selected SDK 11.0.100-rc.2.26504.105; projects still target `net10.0`.
`global.json` contains the pre-existing invalid SDK version `10.0.0`, so this
run must not be described as an SDK-10 build.

With already acquired files, one measured cold call took 2647.1 ms:
2394.3 ms for initialization (wall time minus `DecisionResult.Duration`),
and 252.9 ms for the first tokenization/evaluation. Initialization includes
tokenizer/config/session loading and is not an isolated ONNX load measurement.
Acquisition time was not measured and is excluded.

## Labeled cases and warm results

Single serial calls, short English inputs, Release build, no percentile or
throughput claim:

| Operation and changed evidence | Expected result | Observed result | Warm wall time |
|---|---|---|---|
| Choice: `I need help with a billing problem.` | `Billing / Refunds` | Same label, probability 0.983121 | 172.1 ms |
| Choice: replace `billing` with `technical` | `TECH.support` | Same label, probability 0.565728 | 170.2 ms |
| Score: `The product arrived undamaged.` | Near level 0 | 0.039223 / 2 | 124.8 ms |
| Score: replace `undamaged` with `destroyed` | Near level 2 | 1.831397 / 2 | 113.7 ms |
| Assess `The product is damaged.`, destroyed product | True at 0.5 | 0.847674 | 109.9 ms |
| Same proposition, undamaged product | False at 0.5 | 0.00004782 | 115.1 ms |

The routing descriptions are payments/invoices/refunds, software bugs/outages/API
errors, and sales/pricing/upgrades. The score rubric is `Undamaged`, `Minor
damage`, `Destroyed`, in that order. Full distributions and timings are printed
by the integration test.

### A real counterexample, not an adapter fix

For the proposition `The customer explicitly requests a refund.`:

| Input | Expected at 0.5 | Probability of true |
|---|---|---|
| `I was charged twice. Please refund the extra payment.` | True | 0.883997 |
| `I was charged twice. Do not refund the extra payment.` | False | **0.713369, incorrect** |

The original threshold assertion failed. The test retains both inputs and prints
`Negation contrast ... FAIL`; it does not pretend this is a successful semantic
flip. A separate damage pair supplies the passing assessment smoke check.
Adapter invariants remain hard assertions. Changing probability mapping,
complementing the answer, or fitting a threshold to these two inputs would hide
the model limitation rather than fix it.

The rich assessment probability agrees with the neutral assessment. The rich
12-option case reports `CalibrationClamped = true`; the shared API does not
expose that diagnostic. Distribution concentration, including `Confidence`, is
not calibrated correctness. Neither temperature clamping nor a normalized
distribution establishes calibration.

## Reproducing without model acquisition

Populate a local directory with the matching graph, external weights, runtime
config and tokenizer. Then in PowerShell:

```powershell
$env:RUN_LAYA_TESTS = 'true'
$env:LAYA_MODEL_PATH = 'D:\models\laya'
dotnet test src\tests\ElBruno.LocalLLMs.Decisions.Tests -c Release --logger 'console;verbosity=detailed'
dotnet run --project src\samples\SharedDecisions -c Release -- D:\models\laya
```

Without the opt-in flag, the live test is reported as skipped and the offline
unit tests still run. Enabling it without a model path fails with an actionable
message rather than downloading weights or silently passing.

The live test installs a `System.Net.Http` activity guard that throws before
HTTP dispatch. An explicit HTTP control request must be rejected; loading and
all inference then complete without any additional HTTP attempts. The collection
does not run in parallel with other collections while this guard is active.
This verifies the .NET HTTP/download path is unused after acquisition; it is
**not OS-level isolation of arbitrary native sockets**. No machine-wide
network or firewall settings were changed.

## Kev conversion and native execution

The pinned Apache-2.0 checkpoint `jaredpalmer/kev-0.8b` is an adapter/head, not a
drop-in Laya graph. The converter uses the official reference loader to merge
its LoRA into the pinned Qwen3.5 0.8B base in FP32. It preserves query/key
projection weights and biases and the exact temperature `2.3510958125672174`.
The backbone has 18 linear-attention and six full-attention layers.

The decision graph embeds fresh empty/zero attention caches for one complete
prefill. It gathers final-decision and option-closing hidden states and runs
the trained pointer head. The .NET client normalizes its complete option
logits, never generated text. Original state/question/option delimiters,
user-text sanitization, NFC and Qwen BPE tokenization are preserved.

The repository converter was run end-to-end and emits a versioned manifest,
original licenses, file hashes, six reference cases and tokenizer fixtures.
The native live test validates exact token IDs and marker positions before
scoring, including additional Unicode, whitespace and special-token cases.
Native ORT 1.28.0 failed to load `com.microsoft:LinearAttentionGate`; 1.31.0
loaded and scored successfully. Only the Decisions package's ORT dependency
was upgraded. The SharedDecisions `--kev <export-directory>` path also ran.

| Case | Original PyTorch option probabilities | Maximum .NET absolute probability error |
|---|---|---|
| Billing | 0.95789039 / 0.01767352 / 0.02443611 | `9.515e-7` |
| Technical | 0.00719668 / 0.98602492 / 0.00677836 | `7.136e-7` |
| Undamaged score | 0.92006761 / 0.04287253 / 0.03705982 | `6.744e-7` |
| Destroyed score | 0.01052888 / 0.01166079 / 0.97781026 | `7.194e-7` |
| Damaged assessment, destroyed | no 0.28401920 / yes 0.71598083 | `5.614e-6` |
| Damaged assessment, undamaged | no 0.68346626 / yes 0.31653368 | `3.367e-6` |

The original conversion probe measured about 10.16 seconds for PyTorch reference
loading and roughly 93-218 ms for warm Python ONNX short-row scoring with four
threads. A subsequent native .NET run measured a first call of 3086.5 ms,
including client initialization and inference, followed by 219.2 ms for
technical routing, 124.1/120.8 ms for the two scores, and 101.3/104.0 ms for
assessments. The first call is not an isolated model-load measurement, and the
separate reference-tokenizer checks happen before it. These are single
short-row observations, not throughput or latency guarantees. The final FP32
weight file is about 3.08 GB.

The live test also verified serialized concurrent callers and cancellation
followed by a successful fresh native run. The 99 decision tests passed with
both models enabled. All five publish-workflow suites passed locally, and the
solution built with its existing CI override `-p:NoBuild=false` (required by the
native packaging projects).

Only Kev **0.8B, text, CPU, FP32** is supported by this export contract.
There is no implicit acquisition, Ollama fallback, GPU validation,
quantization-drift claim, or support claim for the larger Kev sizes.
The backend limits each question to 8192 total tokens and 255 options, rejects
overflow rather than truncating, and also applies shared request validation.
Concentration/confidence is not calibrated accuracy.

## Tev1 feasibility

Inspected checkpoint:
[`togethercomputer/Tev1-0.8B-experimental@6bb2dff`](https://huggingface.co/togethercomputer/Tev1-0.8B-experimental/tree/6bb2dff14b38fea90ddb14d870166ccaf77374e9).
Its model card still says the fine-tuned weight release license is being
finalized. Apache-2.0 for its base and MIT for its training code do **not**
establish redistribution rights for these weights. Only tokenizer/config
metadata was downloaded for this probe.

The checkpoint is `Qwen3_5ForConditionalGeneration`, with 24 text layers:
three linear-attention layers followed by one full-attention layer, repeated.
The [released ORT GenAI 0.17.0 builder](https://github.com/microsoft/onnxruntime-genai/blob/v0.17.0/src/python/py/models/builder.py)
explicitly dispatches this architecture to `Qwen35Model`. This is positive source
evidence, not proof that a particular exported graph loads and scores on CPU.
In particular, that builder's optional `gated_delta_net` route requires CUDA;
it must not be substituted into a CPU recipe.

An isolated Python 3.14.4 venv was created outside the repository. Installed
`onnxruntime-genai==0.17.0`, `onnxruntime==1.31.0`, `torch==2.14.1`,
`transformers==5.20.2`, `onnx-ir==1.1.0`, `onnx==1.23.2`, `numpy==2.5.4`,
`tokenizers==0.23.3` and `huggingface_hub==2.2.0`.

Both `builder.py --help` and a CPU export invocation fail during argument-parser
construction, before reading weights:

```text
builder.py -i <pinned-checkpoint-metadata> -o <cpu-export> -p fp32 -e cpu
argparse.py, _expand_help:
ValueError: unsupported format character ' ' (0x20) at index 10559
ValueError: badly formed help string
exit code: 1
```

Here `builder.py` is the installed
`Lib\site-packages\onnxruntime_genai\models\builder.py` executed with the venv's
Python. The metadata directory contains files acquired using `snapshot_download`
with the full checkpoint revision above and only config/tokenizer/Jinja assets.
It is **not a complete weight checkpoint**. The command documents a failed
exporter bootstrap, not a working conversion recipe. No ONNX graph was produced,
and no released exporter/runtime combination has been validated for this
checkpoint. An earlier `-m` invocation fails identically. The later Kev converter
bypassed the CLI by calling `parse_extra_options()` and `create_model()` directly.
The CLI bootstrap is therefore not a fundamental architecture blocker.
This does not resolve Tev1's licensing, full export or CPU scoring gates.

### Tokenization and answer position

The metadata probe loaded the pinned tokenizer locally with
`trust_remote_code=False`. Bare answer letters `A` through `X` each encode as one
distinct token, IDs 32 through 55. This only verifies that exact spelling and
position contract, not whitespace-prefixed or multi-token alternatives.

The [reference request builder](https://github.com/togethercomputer/tev1/blob/1dde7782382c9f49d627153759b8d1deab426ce0/examples/decide.py)
uses its system instruction plus a user JSON object containing only `state`,
`question`, and ordered `options` (`label`, semantic `key`, `description`).
It disables thinking. Applying the pinned chat template with
`add_generation_prompt=True, enable_thinking=False` ends in:

```text
<|im_end|>
<|im_start|>assistant
<think>

</think>

```

A future scorer must read all allowed-letter logits at the next-token position
after that entire suffix, without sampling/top-k/top-p filtering, and normalize
only those complete logits. The reference example requests only five top
logprobs and selects generated text; that alone cannot supply a complete
24-option probability distribution.

Tokenizer SHA-256:
`5f9e4d4901a92b997e463c1f46055088b6cca5ca61a6522d1b9f64c4bb81cb42`.
Chat-template SHA-256:
`d78de6bee4c952ca3145eb161921560a6ede7b59a34e7e4be815f3c5386b4364`.
No reference-inference or Ollama probability comparison was run.

## Should Ollama's decision models become options?

The [decision catalog](https://ollama.com/search?c=decision) listed nine families
when inspected. Laya and the pinned Kev 0.8B export are supported; the remaining
families and Kev sizes are **candidates**, not interchangeable ONNX backends.
Licenses and platform notes below are the publishers' current statements,
not an independent license audit or local benchmark.

| Family | Assessment for this repository |
|---|---|
| [Laya](https://ollama.com/library/laya), 421M | Already supported in-process. Do not duplicate it or replace its ONNX files with an Ollama package. |
| [Tev1](https://ollama.com/library/tev1), 0.8B / 4B | Interesting small LM-head baseline, but inspected weight licensing and export/load/scoring gates remain unresolved. |
| [Nimble](https://ollama.com/library/nimble), 9B | Worth a separate text-decision evaluation; publisher lists Apache-2.0. More demanding than Laya, with a different Qwen3.5 scoring/prompt contract. |
| [Nimble v3](https://ollama.com/library/nimble-v3), 9B | Research-only candidate for now: CC BY-NC 4.0, unlike original Nimble. Not a default commercial option. |
| [Clef Flash](https://ollama.com/library/clef-flash), 9B | Worth investigating for multimodal decisions, Apache-2.0 stated. Needs its own head/export and image contract; the shared text API cannot expose image input. |
| [Clef](https://ollama.com/library/clef), 27B | Same multimodal opportunity at a much larger memory cost. A cloud tag does not satisfy in-process/offline inference. |
| [pplx-decider](https://ollama.com/library/pplx-decider), 27B | Larger multimodal candidate, Apache-2.0 stated. Defer until a smaller backend is validated; no accuracy comparison was performed here. |
| [Kev](https://ollama.com/library/kev), 0.8B / 4B / 9B / 27B | **0.8B implemented and validated natively on Windows CPU**, converted from the original Apache-2.0 checkpoint. Larger sizes remain untested. Ollama's MLX packaging is not an intrinsic original-checkpoint restriction. |
| [Strands Decider](https://ollama.com/library/strands-decider), 2B | Next compact Apache-2.0 candidate. Its original PyTorch reference includes CPU loading; MLX-only Ollama tags do not prevent independent ONNX work. LayerNorm, per-kind temperatures and seed-soup head make it different from Kev; not yet converted or executed here. |

Recommendation: keep Laya as the default and offer the pinned Kev 0.8B export as
an explicit additional option. Prioritize Strands next, with original Nimble as
a larger text baseline. No Ollama HTTP provider or fallback is added.
Every new in-process backend must pass licensing, exact head/prompt/tokenizer,
CPU export/load, complete-logit scoring, contrastive and reference-parity gates.
None can be enabled merely by changing `DecisionOptions.ModelRepository`.

### Original-checkpoint research beyond Kev

- [Strands 2B](https://huggingface.co/StrandsAgents/strands-decider-2B-qwen3.5-v1-2610/tree/216d1ccb46a9e9b036eb9187b248db98480f77d4):
  revision `216d1ccb46a9e9b036eb9187b248db98480f77d4`, reference source
  `strands-labs/strands-decider@d942e5515db750f80a51032629ec312455a87976`.
  A three-seed soup uses an averaged rank-48 LoRA adapter and stacked pointer
  projections. Its LayerNorm and per-kind temperatures must be exported too;
  Kev's simple pointer graph is not a compatible replacement.
- [Original Nimble 9B](https://huggingface.co/bespokelabs/Bespoke-Nimble-9B):
  Apache-2.0, candidate-token logits with a different prompt and up to 255 choices.
  The inspected updated serving config uses temperature **1.0**, not the older
  2.179. Token codebook and reference hashes must be preserved. Its reference
  runner's CUDA BF16 requirement is not proof that a CPU ONNX export is impossible.
- [Clef Flash](https://huggingface.co/Cloudflare/clef-flash/tree/f0b7d3cd65296abfe459e6c228b618f5d8b26d2f)
  and [Clef](https://huggingface.co/Cloudflare/clef/tree/a30162b99d68a481d8bd75d38df069a0cf83df40):
  Apache-2.0 checkpoints carry `joint_head.safetensors`, a head config and custom
  `joint_schema_model.py`. The evidence-routing/attention head needs its own
  conversion; sharing Qwen3.5 is not sufficient.
- [pplx-decider-v1-27b](https://huggingface.co/perplexity-ai/pplx-decider-v1-27b/tree/d8c3bd18e31dc1804433d38528b3e770019bf00c):
  revision `d8c3bd18e31dc1804433d38528b3e770019bf00c`, Apache-2.0, custom scoring
  and multimodal behavior. No local weights/export/scoring run was attempted.

These are source/config/license findings, not working conversion results.

## Release decision

The new Kev client/registration and required native runtime upgrade change the
public package, so **a new minor release, 0.24.0, is warranted**. Laya remains
the default and its rich/shared APIs retain their behavior.
The original issue's Tev1 acceptance permits a reproducible documented blocker;
this work does not claim to resolve its weight license or implement Tev1.
The observed Laya negation error remains documented, not hidden by an adapter
change or an unsupported accuracy claim.
