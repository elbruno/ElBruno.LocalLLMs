# Squad Decisions

## Open Decisions

### Decision 38: S1-mini Repository Boundary — OPEN / AWAITING BRUNO'S DECISION

**Date:** 2026-08-19T17:29:00-04:00
**Status:** **OPEN — Awaiting Bruno's ratification**
**Agents:** Morpheus (Lead/Architect), Fact Checker (Devil's Advocate), Coordinator relay
**Related:** Decision 36 (s1-mini delivery complete), Decision 37 (test seam + e2e verification)
**Question:** Where does the `TranscriptNormalizer` API live — `ElBruno.LocalLLMs` (STAY), `ElBruno.Speech` (MOVE), or split with interface in Speech.Abstractions (SPLIT)?

---

## Coordinator Summary

**The team's initial STAY recommendation was attacked twice by Bruno and reversed by parallel re-analysis.** Morpheus initially recommended STAY (s1-mini normalizer stays in LocalLLMs as an `IChatClient` wrapper). Bruno pushed back twice, citing the model card ("A 0.6B-parameter text normalizer **for speech-to-text output**... does one job") and questioning whether it should live in `ElBruno.Speech` instead. Morpheus was dispatched to re-run the analysis with domain-cohesion weighted primary; Fact Checker was dispatched in parallel as Devil's Advocate attacking STAY.

**Outcome: Both agents reversed the call**, but to different answers:
- **Morpheus:** Now recommends **MOVE** (implementation relocates to Speech, following the `ElBruno.Speech.Vad.Silero` precedent of concrete ONNX-model packages as Speech siblings).
- **Fact Checker:** Recommends **SPLIT** (interface `ITranscriptNormalizer` in `Space.Abstractions`; implementation stays in LocalLLMs). Claims SPLIT is architecturally consistent with Speech's pattern for every other model domain (STT, TTS, VAD, Chat — all interface-in-Space, implementation-external).

**Invariant:** Regardless of outcome, `KnownModels.S1Mini`, `LocalChatClient`, the ORT-GenAI temperature-0 fix in `OnnxGenAIModel.cs`/`OnnxVisionModel.cs`, its 16 regression tests, and conversion/eval scripts stay in LocalLLMs. No NuGet release needed to remove anything (feature is uncommitted).

---

## Key Evidence Cited

**Evidence for MOVE (Morpheus):**
1. `ElBruno.Speech.Vad.Silero` is a concrete ONNX-model package published as a Speech sibling — **not an external provider** as Morpheus's original brief claimed. Direct contradiction of his own reason 1.
2. `TextSegmenter.cs` in `ElBruno.Speech.Pipeline` — `internal static string→IEnumerable<string>` text processing lives in Speech, establishing that non-audio text processing is a legitimate Speech resident.
3. The entire `TranscriptNormalizer` implementation is uncommitted — zero NuGet release cycle friction on the removal side.
4. The API is already provider-neutral (`IChatClient` primary constructor); only `CreateAsync(LocalLLMsOptions)` is LocalLLMs-coupled.

**Evidence for SPLIT (Fact Checker):**
1. Speech's own architecture is the **interface-in-Space, implementation-external** pattern for every capability: STT (Speech.Abstractions:`ISpeechToTextClient` ← ElBruno.Whisper impl), TTS (Speech.Abstractions:`ITextToSpeechClient` ← ElBruno.QwenTTS/VibeVoice impl), VAD (Speech.Abstractions:`IVoiceActivityDetector` ← Space.Vad.Silero impl), Chat (MEAI:`IChatClient` ← LocalLLMs impl). Adding `ITranscriptNormalizer` to Space.Abstractions and having LocalLLMs implement it is **the consistent pattern**, not a novelty.
2. Bruno's PRD authorship (July 3, 2026) predates s1-mini; citing it as a constraint against a feature that didn't exist is circular.
3. Discoverability is the user-visible outcome: users searching for transcript cleanup first look under Space, not LocalLLMs. SPLIT resolves this structurally (IntelliSense, NuGet namespace, docs) rather than via a README callout.
4. MOVE imports ORT-GenAI, CPM, and `.sln` friction into Space; SPLIT avoids all of it.

**Bruno's Model Card Quote:**
> "A 0.6B-parameter text normalizer **for speech-to-text output**. S1-mini is not a chat model and will not follow general instructions; it does one job."

This cuts both ways: "for speech-to-text output" → Speech domain (supports MOVE/SPLIT). "Not a chat model" → implementation stays with ORT-GenAI (supports SPLIT over MOVE). Both agents agreed Bruno's instinct was *correct* (the feature belongs in Space's domain), and disagreed only on *how* (concrete impl in Space vs. interface in Space + impl in LocalLLMs).

**Process Lesson — Coordinator Error:**
The original framing was **binary (STAY-vs-MOVE)**, not **ternary (STAY-vs-MOVE-vs-SPLIT)**. Fact Checker noted that the missing third option dissolved the conflict: both Morpheus and Fact Checker converge on "reverse STAY," and SPLIT dominates on every axis Morpheus cares about (zero dependency edge Speech→LocalLLMs, ORT-GenAI coupling remains in LocalLLMs, zero NuGet friction) *while also* honoring Bruno's discoverability-first instinct.

---

## Unresolved: MOVE vs SPLIT

**Morpheus's recommendation:** MOVE (cleaner for consumers; parallels Silero; zero code drift risk).
**Fact Checker's recommendation:** SPLIT (maintains architectural consistency with Speech's other capabilities; zero runtime coupling in Space; structural discoverability).

**Both agents flagged this as Bruno's product call.** Bruno was unavailable for structured prompt; no implementation performed. Feature remains uncommitted in LocalLLMs.

---

## Migration Costs

| Option | API Cost | Runtime Cost | NuGet Cost | Implementation Time |
|---|---|---|---|---|
| **STAY** (original) | Zero | Zero | Zero | Zero |
| **MOVE** | Medium (namespace, drop LocalLLMsOptions factory, add Space.csproj) | High (ORT-GenAI imports into Space, CPM setup, `.sln`→Space convention friction) | Space 1.2.0 release needed | ~0.5 day + 1 release cycle |
| **SPLIT** | Low (add ~30-LOC `ITranscriptNormalizer` interface to Space.Abstractions; LocalLLMs impl `: ITranscriptNormalizer`) | Zero (no new deps in Space; LocalLLMs takes Space.Abstractions ref — same direction as MEAI) | Space.Abstractions SemVer minor; LocalLLMs ships with interface adoption | ~0.25 day; coordinates with next regular release |

---

## Next Actions — Awaiting Bruno

1. **Which option?** MOVE (impl in Space) or SPLIT (interface in Space, impl in LocalLLMs)? Both reverse STAY and honor the "does one job for speech" instinct.
2. **If SPLIT:** Do you want the pipeline seam formalized now (e.g., `Func<string, CT, Task<string>>` post-STT hook in `DefaultSpeechPipeline`), or is this v1 shipped standalone and the hook comes later?
3. **If MOVE:** Accept the ORT-GenAI + CPM + `.sln` friction in Space in exchange for a cleaner on-disk home?

No code changes have been committed. All s1-mini work in LocalLLMs and the cross-link in Speech README remain uncommitted pending decision.

---

## Process Lessons

### Lesson 1: Re-analyze with the owner's criterion when the owner contradicts a team recommendation

**Context:** Morpheus initially recommended STAY. Bruno disagreed twice, citing the model card and domain fit. Rather than defend the prior call, the coordinator re-dispatched Morpheus with a changed weighting: "domain-cohesion first, not architecture-preservation first."

**Outcome:** Morpheus reversed his own recommendation to MOVE. Fact Checker, dispatched independently as Devil's Advocate, reversed to SPLIT.

**Durable lesson:** When a repo owner (not a junior contributor) contradicts a team recommendation, the team's instinct was probably wrong, or the team's weighting was wrong. Re-run the analysis under the owner's stated or implied criterion (domain-primacy, in this case) and report the result, even if it contradicts prior work. This is not a sign of weakness; it is a sign of principled re-weighting.

### Lesson 2: A binary framing can itself be the error

**Context:** The coordinator framed the decision as STAY-vs-MOVE. Morpheus and Fact Checker both reversed STAY, but to different answers (Morpheus → MOVE, Fact Checker → SPLIT). Neither agent asked "is the framing itself wrong?"

**Outcome:** Only when Fact Checker's full brief was read did the ternary option (SPLIT) emerge as dominant on every axis Morpheus cared about. The whole debate dissolved once a third option was introduced.

**Durable lesson:** Before defending prior conclusions, check whether the decision framing itself is the error. When two strong agents reverse a prior call but disagree on the direction, ask whether they're optimizing for different criteria — or whether the criteria itself is incomplete. A framing like "STAY-vs-MOVE" can hide a third option that dominates both. Always ask: are there more than two options?

### Lesson 3: Precedent is discoverable through code inspection, not summaries

**Context:** Morpheus's original brief stated "Speech doesn't host chat-model implementations" as an abstract principle. Fact Checker, reading the actual files, found `ElBruno.Speech.Vad.Silero/SileroVadClient.cs` — a concrete ONNX-model implementation living inside Space.

**Outcome:** One grep + one view replaced an entire philosophical debate. The precedent was decisive.

**Durable lesson:** Never assume an abstract principle is true without checking the codebase for exceptions. "Speech is provider-agnostic" is true as a philosophy; but "Speech never hosts model implementations" is false as a claim about reality. Read the code.

---

## Active Decisions

### Decision 36: S1-mini ASR Transcript Normalizer — Delivery Complete

**Date:** 2026-08-19
**Status:** Complete — INT4 published, C# API built, tests passing  
**Agents:** Dozer (ML Conversion), Trinity (C# API), Tank (Tests), Morpheus (Docs)

## Coordinator Summary

Complete cross-team delivery of superwhisper/s1-mini ASR transcript normalizer support. Single-task English text normalization from spoken transcripts — **not** a chat model.

## Key Coordinator Decisions

1. **INT4 is the only recommended variant.** FP16 fails at inference time on CPU with onnxruntime-genai 0.15.1 (shape-mismatch in GQA repeat_kv Reshape). Both variants published to elbruno/s1-mini-onnx for forward compatibility, but INT4 only is functional today.

2. **No new ChatTemplateFormat needed.** ChatTemplateFormat.Qwen3 already emits enable_thinking=False equivalent (<think>\n\n</think>), reused by both KnownModels.S1Mini and S1MiniFp16.

3. **Temperature = 0 crash fixed at execution layer.** OnnxGenAIModel.ApplyParameters and OnnxVisionModel.ApplyParameters now guard SetSearchOption("temperature", ...) with if (Temperature > 0), protecting all models library-wide from native divide-by-zero. TranscriptNormalizer.NormalizeAsync still sends Temperature = 0f (greedy contract), which is now safe.

4. **Control-line enum values verified empirically:**
   - Styling: formal/semi-formal/casual all distinct.
   - Context: email is distinct; message/notes identical to general.
   - Structure: lists accepted, no literal Markdown bullets.

5. **API boundary:** s1-mini is task-specific, not a chat model. Docs enforce fixed system prompt + control-line format + transcript input only.

## Verification

- INT4 model: 6-prompt smoke-eval passed.
- Empty-output-on-filler: correctly returns "" for pure-filler input.
- C# API: 1563 tests passing (+48 vs baseline 1515).
- Docs: all 5 What's New entries current; no open P0s.

## Detailed Decision Records

See linked agent decision files for full evidence:
- `.squad/decisions/inbox/dozer-s1-mini-conversion.md` — ONNX conversion, smoke-eval, INT4 recommendation.
- `.squad/decisions/inbox/trinity-s1-mini-support.md` — C# API design, DI registration, execution-layer bug fix.
- `.squad/decisions/inbox/tank-s1-mini-tests.md` — test coverage (1563 passing).
- `.squad/decisions/inbox/morpheus-s1-mini-docs.md` — documentation completeness.

---


# Dozer — s1-mini ONNX Conversion

**Date:** 2026-08-19T16:02:09.824-04:00
**Author:** Dozer (ML Engineer)
**Requested by:** Bruno Capuano (@elbruno)

## Objective

Add ONNX conversion support for `superwhisper/s1-mini` and publish converted
artifacts to `elbruno/s1-mini-onnx`.

## What was done

- Created `scripts/convert_s1_mini.py`, modeled directly on
  `scripts/convert_magentic_brain.py` (same structure/section-comment style).
  Supports `--precision {int4,fp16,both}` (default `both`), writing `int4/` and
  `fp16/` subfolders when `both` is selected, matching the `ModelSubPath`
  convention already used by `KnownModels.Phi4` and `KnownModels.GptOss20B`.
- Created `scripts/eval_s1_mini.py`, a documented throwaway smoke-eval script
  that loads a converted variant with `onnxruntime_genai` and runs 6 ASR
  transcript-normalization prompts through the model's required system prompt
  + control-line format, greedy decoding.
- Ran conversion for both `int4` and `fp16` against `superwhisper/s1-mini`.
- Ran the smoke-eval against both variants.
- Uploaded `int4/`, `fp16/`, `README.md`, and the upstream `LICENSE` to
  `elbruno/s1-mini-onnx` on HuggingFace (already authenticated as `elbruno`).

## Preflight results

| Check | Result |
|---|---|
| Python | 3.14.4 |
| onnxruntime-genai | 0.15.1 (meets >= 0.15.1 requirement) |
| HF auth | authenticated as `elbruno` |
| Disk free | ~1.15 TB (far more than the ~8 GB peak needed) |
| RAM | 440 GB |
| GPU | NVIDIA A10-24Q detected (conversion ran CPU-only, `-e cpu`, per script default) |

## Conversion results

| Variant | Status | model.onnx | model.onnx.data |
|---|---|---|---|
| int4 | ✅ succeeded, all required files present | 0.3 MB | 0.37 GB |
| fp16 | ✅ succeeded, all required files present | 0.7 MB | 1.12 GB |

## Two bugs found and fixed during eval (documented in the model card)

1. **`temperature=0.0` crashes.** Calling
   `GeneratorParams.set_search_options(do_sample=False, temperature=0.0, ...)`
   crashes onnxruntime-genai's native runtime with an integer divide-by-zero,
   even though `do_sample=False` should make temperature irrelevant. Fix:
   omit `temperature` entirely for greedy decoding.
2. **`tokenizer.decode([])` crashes.** When the model correctly emits EOS
   immediately (e.g. pure-filler input that should normalize to an empty
   string), the generated sequence is length 0. Calling `tokenizer.decode()`
   on an empty token list crashes the native decoder with an integer
   divide-by-zero. Fix: check `len(new_tokens) > 0` before calling `decode()`
   and treat a zero-length result as `""`.
3. **Chat template needs an explicit empty `<think>` block.** s1-mini's chat
   template only suppresses the model's `<think>...</think>` reasoning block
   when `enable_thinking is defined and enable_thinking is false`, which is
   rendered as a literal `<think>\n\n</think>\n\n` string right after
   `<|im_start|>assistant\n`. If you build the prompt manually rather than
   through the HF chat template, you must include that block explicitly, or
   the model will emit an open `<think>` tag and no useful content.

## Smoke-eval results — INT4 (recommended default)

| Case | Input (last line) | Output |
|---|---|---|
| Model-card reference | `so um i need to like send the the report by uh friday no wait make that thursday` | `So I need to send the report by Thursday.` |
| Email + phone | `hey so uh my email is bruno at example dot com and i'll call you at like three thirty tomorrow` | `Hey, so my email is bruno@example.com, and I'll call you at like 3:30 tomorrow.` |
| Pure filler (expect empty) | `um uh you know like` | `` (empty — correct) |
| Longer dictation w/ self-correction | `okay so uh the the meeting is at nine am no sorry it got moved to ten thirty and uh we need like twenty five copies of the the slide deck and uh don't forget to book room number two oh and uh bring the the laptop charger this time` | `Okay, so the meeting got moved to 10:30, and we need 25 copies of the slide deck. Don't forget to book room number 20, and bring the laptop charger this time.` |
| `[Context: email]` | `hi team uh just wanted to say the the deployment went fine last night and uh no issues so far` | `Hi Team,\n\nJust wanted to say the deployment went fine last night, and no issues so far.` |
| `[Structure: lists]` | `so uh first we need to uh order the parts then uh schedule the install and uh finally uh test everything before uh shipping it out` | `So first we need to order the parts, then schedule the install, and finally test everything before shipping it out.` |

Notes on quality: matches the model-card reference example exactly in spirit
(minor casing difference: "So I" vs "I"). Correctly returns an empty string
for pure filler. Correctly converts spoken email/phone formats. One minor
error: "room number two" became "room number 20" in the self-correction test
— a plausible ASR-normalizer failure mode (misheard "two" as part of "20"),
not something specific to INT4 quantization (not cross-checked against FP16
since FP16 doesn't run — see below).

## Smoke-eval results — FP16

**FP16 does not run on CPU with onnxruntime-genai 0.15.1.** Every prompt
fails identically with:

```
[ONNXRuntimeError] : 1 : FAIL : Shape mismatch attempting to re-use buffer.
{1,1,2048} != {1,98,2048}. ... Cast node
'InsertedPrecisionFreeCast_/model/layers.1/attn/v_proj/repeat_kv/Reshape_4/output_0'
```

This is a shape-mismatch inside ONNX Runtime's buffer-reuse graph optimizer,
in the GQA `repeat_kv` `Reshape` node produced by the FP16 CPU builder path
for this architecture (16 Q heads / 8 KV heads, GQA). Reproduced consistently
across multiple runs and prompts. Root cause looks like an onnxruntime-genai
builder/graph-optimization bug for FP16 + GQA on the CPU execution provider,
not a problem with s1-mini itself or with this conversion process (INT4
conversion of the exact same source model works perfectly).

## Recommendation

**Default `KnownModels` variant: INT4.** Evidence:
- INT4 runs correctly and produces high-quality, expected output across all
  6 test prompts (including the empty-output filler case and the
  `[Context: email]` / `[Structure: lists]` control-line variants).
- FP16 does not run at all on CPU with the currently-installed
  onnxruntime-genai (0.15.1) — it is not a "worse quality, smaller size"
  tradeoff, it is simply broken right now. INT4 is the only usable variant.
- INT4 is also far smaller (0.37 GB vs 1.12 GB), matching the default
  assumption in the task brief.
- The `fp16/` artifact is still published to `elbruno/s1-mini-onnx` for
  future use (e.g. once the ORT bug is fixed, or for GPU execution providers
  that may not hit this optimizer path) but is flagged as non-functional on
  CPU in the model card's "Known Issues" section.

## What the C# side (Trinity) needs to know

- `HuggingFaceRepoId = "elbruno/s1-mini-onnx"`
- Default variant should point at the `int4/` subfolder:
  - `RequiredFiles = ["int4/*"]`
  - `ModelSubPath = "int4"`
- If a second `KnownModels` entry is added for FP16 (mirroring the
  `GptOss20B` / `GptOss20BCuda` pattern), use:
  - `RequiredFiles = ["fp16/*"]`
  - `ModelSubPath = "fp16"`
  - but note in its doc comment that FP16 currently fails at inference time
    on CPU with onnxruntime-genai 0.15.1 (shape-mismatch in GQA repeat_kv
    Reshape) — do not make it the default, and consider not shipping it as a
    `KnownModels` entry at all until the upstream bug is confirmed fixed.
- Both variants use `ChatTemplateFormat` similar to Qwen3 models
  (`<|im_start|>role\n...<|im_end|>\n` framing), but this is **not a chat
  model** — callers should not expose arbitrary multi-turn chat. The required
  interaction is: fixed system prompt (see model card) + one user message
  containing a `[Styling: ...] [Structure: ...] [Context: ...]` control line
  followed by the raw transcript. If `LocalChatClient`/`ChatTemplateFormat`
  always renders `enable_thinking=True` semantics, s1-mini needs the explicit
  empty `<think>\n\n</think>\n\n` block injected right after the assistant
  turn header, or output quality will regress to leaking raw `<think>` tags.
- Guard any code path that calls the tokenizer's decode on a possibly-empty
  generated sequence (native crash otherwise) — relevant if any wrapper code
  in this repo calls into onnxruntime-genai's tokenizer decode directly for
  zero-token completions.

## Unverified control-value probe (requested follow-up, 2026-08-19)

Trinity's typed `TranscriptNormalizer` C# API has enum members for control-line
values that were only extrapolated, not verified against the model: Styling
`formal`/`casual`, Context `message`/`notes`. Probed all four empirically
against the INT4 model (already loaded for eval — no conversion work
restarted).

**Styling — both `formal` and `casual` are real, distinct, and sensible.
Keep them.**

Input: `"hey uh i cant make it to the meeting today my car broke down so uh yeah ill be there tomorrow instead"`

| Value | Output |
|---|---|
| `semi-formal` (baseline) | `"Hey, I can't make it to the meeting today. My car broke down, so I'll be there tomorrow instead."` |
| `formal` | `"Hey, I cannot make it to the meeting today. My car broke down, so I will be there tomorrow instead."` — expands contractions, clearly more formal register. |
| `casual` | `"hey uh i cant make it to the meeting today. my car broke down so uh yeah ill be there tomorrow instead"` — keeps filler words and casing/contractions largely as-is, minimal cleanup, clearly casual register. |

**Context — `email` is real and distinct; `message` and `notes` are
effectively no-ops (identical to `general`).**

Input: `"hi so uh just wanted to let you know the meeting got moved to three pm uh thanks"`

| Value | Output |
|---|---|
| `general` (baseline) | `"Hi, so just wanted to let you know the meeting got moved to 3pm. Thanks."` |
| `email` | `"Hi,\n\nSo just wanted to let you know the meeting got moved to 3pm.\n\nThanks,"` — adds greeting/body/signoff structure. Clearly distinct. |
| `message` | Identical to `general`. |
| `notes` | Identical to `general`. |

Re-checked `notes`/`message` against a second, note-taking-style transcript
(grocery/todo list) combined with `Structure: lists` to rule out a false
negative from the first test being too short — same result: `notes` and
`general` produced near-identical output (one incidental word-order
difference, not systematic), and `message` matched `general` byte-for-byte.

**Recommendation for Trinity:**
- Keep `Styling.Formal` and `Styling.Casual` — remove "unverified/best-guess"
  wording from their `<remarks>`; they are confirmed to produce distinct,
  sensible output.
- For `Context.Message` and `Context.Notes`: either drop them from the enum,
  or keep them but change the `<remarks>` to state plainly that empirical
  testing shows the model accepts these tokens without error but treats them
  identically to `Context.General` — do not describe them as producing
  distinct formatting.

**Aside (not part of this ask, worth a follow-up look):** `Structure: lists`
did not produce actual bulleted/numbered output in any test run (prose in,
prose out, just reworded) — if Trinity's API implies literal list formatting
for this value, that expectation doesn't match observed model behavior.


- `converted_models/` and `cache_dir/` are already in `.gitignore` — no
  action needed, multi-GB conversion artifacts were not at risk of being
  committed.
- Did not touch `docs/` or any `src/` C# code per task constraints.


# Trinity — S1-mini ASR transcript normalizer support

Date: 2026-08-19

## Summary

Added first-class library support for `superwhisper/s1-mini`, a 0.6B
`Qwen3ForCausalLM` fine-tune of `Qwen/Qwen3-0.6B` that is a single-task ASR
transcript normalizer (not a chat model).

## Key decisions

1. **No new `ChatTemplateFormat`.** `ChatTemplateFormat.Qwen3` → `Qwen3Formatter`
   already emits ChatML with the `<|im_start|>assistant\n<think>\n\n</think>\n\n`
   generation prompt, equivalent to the model card's `enable_thinking=False`
   requirement. Reused as-is for both `KnownModels.S1Mini` and `S1MiniFp16`.

2. **Two `ModelDefinition`s, one repo.** `S1Mini` (`int4/*`, `ModelSubPath =
   "int4"`) and `S1MiniFp16` (`fp16/*`, `ModelSubPath = "fp16"`) both point at
   `elbruno/s1-mini-onnx`, mirroring the existing `Phi4`/`GptOss20B` variant
   pattern (single repo, multiple precision subfolders). Both are `ModelTier.Tiny`,
   `SupportsToolCalling = false`. The ONNX artifacts do not exist yet — Dozer is
   converting in parallel — so these definitions are untested against a real
   model; only the download path shape and prompt formatting were validated by
   compiling against the existing `ModelDownloader`/`LocalChatClient` code paths.

3. **New `TranscriptNormalizer` API surface** under
   `ElBruno.LocalLLMs.Normalization`:
   - `TranscriptStyling` (Formal/SemiFormal/Casual), `TranscriptStructure`
     (Prose/Lists), `TranscriptContext` (General/Email/Message/Notes) enums,
     each with an `internal` `ToWireValue()` extension mapping to the model
     card's control-line strings. Per the task's own uncertainty note: only
     `semi-formal`, `prose`, `general`, `lists`, and `email` are verified against
     the model card; `formal`, `casual`, `message`, `notes` are my best-guess
     lowercase kebab-case extrapolations and are flagged as such in `<remarks>`.
   - `TranscriptNormalizerOptions`: the three enums + `MaxTokens` (default 1024)
     + optional `SystemPrompt` override.
   - `TranscriptNormalizer`: wraps an `IChatClient` via a public constructor
     (testable without a real model) plus an `internal` constructor with an
     `ownsChatClient` flag used only by the static `CreateAsync` factory and by
     `AddTranscriptNormalizer` DI registration, so the client we create
     ourselves gets disposed but a caller-supplied client does not.
     `NormalizeAsync` forces `Temperature = 0` (greedy decoding, matching
     `do_sample=False` on the model card) and returns `string.Empty` immediately
     for empty/whitespace input without calling the model. Also added a
     best-effort `NormalizeChunkedAsync` convenience overload (sentence-boundary
     chunking) since the model card recommends ~1,000-token input; this is
     documented as best-effort only, not authoritative chunking.
   - Internal seams for Tank's tests: `TranscriptNormalizer.BuildPrompt(string,
     TranscriptNormalizerOptions)` (prompt shape, no model needed) and
     `TranscriptNormalizer.SplitIntoChunks(string, int)` (chunking logic).
     Both accessible via the existing `InternalsVisibleTo`
     `ElBruno.LocalLLMs.Tests` entry in the library csproj — no csproj change
     was needed there.

4. **DI registration**: `AddTranscriptNormalizer(Action<LocalLLMsOptions>?
   configure = null)` on `LocalLLMsServiceExtensions`, registered as its own
   `TranscriptNormalizer` service type rather than `IChatClient` — deliberately
   not registered as `IChatClient` since it is not a general chat client and
   registering it as one could collide with `AddLocalLLMs()`/
   `AddLocalVisionLLM()` in the same container and would be misleading to
   consumers expecting chat semantics.

5. **Sample project** `src/samples/TranscriptNormalizer/` added and registered
   in `ElBruno.LocalLLMs.slnx` under `/src/samples/`. Demonstrates: default
   normalization of the model card's reference filler-heavy transcript ("so um i
   need to like send the the report by uh friday no wait make that thursday"),
   `Context: email`, `Structure: lists`, and empty-output-on-pure-filler.

## Build status

`dotnet build ElBruno.LocalLLMs.slnx -p:TargetFrameworks=net8.0` fails only on
3 pre-existing, unrelated errors from `ElBruno.LocalLLMs.BitNet.Native.{linux-x64,
osx-arm64,win-x64}` — those projects hard-code `<NoBuild>true</NoBuild>`
unconditionally (native pack-only projects) and always error under a plain
`dotnet build` on this machine regardless of any other change; confirmed by
building `BitNetChat.csproj` in isolation before touching anything, which fails
identically. All projects I touched or added build clean in isolation:
`ElBruno.LocalLLMs.csproj`, the new `TranscriptNormalizer.csproj` sample,
`ElBruno.LocalLLMs.Tests.csproj`, and `DependencyInjection.csproj` (exercises
`LocalLLMsServiceExtensions`).

## Notes for Tank / docs

- Do not write ONNX-artifact-dependent tests; `elbruno/s1-mini-onnx` does not
  exist on HuggingFace yet.
- Test `TranscriptNormalizer.BuildPrompt` and `SplitIntoChunks` directly
  (internal, IVT-visible) plus `NormalizeAsync`/`NormalizeChunkedAsync` against
  a fake `IChatClient` to verify: empty/whitespace short-circuit, `Temperature
  = 0` / `MaxOutputTokens` wiring, control-line format, and disposal ownership
  (constructor-supplied client is NOT disposed by `TranscriptNormalizer.Dispose()`;
  `CreateAsync`-created client IS disposed).
- docs/README follow-up should document the wire-value uncertainty for
  `formal`/`casual`/`message`/`notes` called out above.

---

## Follow-up: post-conversion fixes from Dozer's empirical findings (2026-08-19)

Dozer completed the ONNX conversion, published `elbruno/s1-mini-onnx` (int4 +
fp16), and ran a real smoke-eval. Full evidence in
`.squad/decisions/inbox/dozer-s1-mini-conversion.md`. This invalidated three
assumptions; here is what changed and why.

### 1. Blocking bug: `Temperature = 0` crashes the native runtime — FIXED at the execution layer, not in `TranscriptNormalizer`

Root-caused the crash to `Execution/OnnxGenAIModel.cs` and
`Execution/OnnxVisionModel.cs`'s `ApplyParameters`, which called
`genParams.SetSearchOption("temperature", parameters.Temperature)`
**unconditionally** — including when `Temperature == 0`. ORT-GenAI's native
runtime crashes with an integer divide-by-zero on a literal `0` temperature
search option, regardless of `do_sample`.

`GenerationParameters.Temperature` already documents `0 = greedy`, and
`do_sample` is already derived as `parameters.Temperature > 0`. So the
existing execution layer already had the greedy concept Dozer's report asked
me to look for — it just never withheld the crashing native call for that
case. Fix (in both `OnnxGenAIModel.cs` and `OnnxVisionModel.cs`): only call
`SetSearchOption("temperature", ...)` when `Temperature > 0`; when
`Temperature <= 0`, the search option is omitted entirely (do_sample=false is
sufficient on its own to select greedy decoding). This is a systemic fix — it
protects **every** model in the library from this native crash whenever a
caller (or a model's own defaults) resolves to `Temperature <= 0`, not just
s1-mini.

`TranscriptNormalizer.NormalizeAsync` still sets `ChatOptions.Temperature =
0f` — that is correct and safe now, since it maps through
`GenerationParameters(Temperature: 0)` → the fixed `ApplyParameters`, which no
longer forwards the crashing value to the native runtime. I deliberately did
NOT change `TranscriptNormalizer.cs` to omit `Temperature` from `ChatOptions`,
because doing so would fall back to `LocalLLMsOptions.Temperature` (default
0.7, non-greedy) for any caller who wires up `TranscriptNormalizer` via DI
(`AddTranscriptNormalizer`) without separately hard-coding
`LocalLLMsOptions.Temperature = 0`. Forcing `ChatOptions.Temperature = 0f`
per-call is the only way to guarantee greedy decoding regardless of how the
underlying `LocalChatClient` was configured, and it is now safe to do so.
Confirmed no test asserts the raw float value passed to the native
`SetSearchOption` call, so this does not regress any existing test.

### 2. Empty-generation crash guard — confirmed already safe, no code change needed

Checked `OnnxGenAIModel.Generate` / `GenerateStreamingAsync` and
`OnnxVisionModel`'s equivalents: every decode call in this repo's execution
layer decodes exactly **one** already-generated token at a time via the
tokenizer's incremental stream decoder (`tokenizerStream.Decode(singleTokenId)`
/ `IVisionTokenizerStream.Decode(int tokenId)`). Nothing in this codebase ever
calls a batch/array decode API with a zero-length token list — that failure
mode is specific to Dozer's throwaway Python eval script
(`tokenizer.decode([])` on a full generated-sequence array), which is a
different code path than this library's incremental per-token decode loop.
When the model emits EOS immediately (e.g. pure-filler input for s1-mini),
`generator.IsDone()` short-circuits the `while` loop before or after
consuming exactly one token, and the C# `Generate`/`GenerateStreamingAsync`
methods correctly return an empty string / yield nothing — no crash-prone
path exists here. Confirmed, no guard was added because none was needed.

### 3. Enum control values — updated `<remarks>` per empirical verification

- `TranscriptStyling.Formal` / `Casual`: removed "unverified/best-guess"
  hedging from both the member docs and the `ToWireValue()` `<remarks>` —
  Dozer confirmed both produce distinct, sensible output vs. `SemiFormal`.
- `TranscriptContext.Message` / `Notes`: **kept both enum members** (chose
  honesty-via-documentation over removal, since dropping them would be a
  breaking API change for no functional gain over just documenting the
  reality). Doc comments and the `ToWireValue()` `<remarks>` now state
  plainly, per Dozer's two independent verification runs, that both are
  accepted by the model without error but produce output **identical** to
  `TranscriptContext.General` — explicitly not described as distinct
  formatting.
- `TranscriptStructure.Lists`: softened the doc comment — no longer promises
  literal Markdown bullet/numbered output. States the model card documents
  this value, but local empirical testing did not reproduce bulleted output
  (prose in, prose out, just reworded). Enum member kept as a documented
  model-card value.

### 4. `S1MiniFp16` — kept, with a prominent non-functional warning

Decision: keep `KnownModels.S1MiniFp16` as a `KnownModels` entry (matching
Dozer's note that the `fp16/` artifact is genuinely published to
`elbruno/s1-mini-onnx` for future use), but its XML doc comment now leads with
an explicit ⚠️ warning that it currently fails at inference time on CPU with
onnxruntime-genai 0.15.1 (shape-mismatch in the GQA `repeat_kv` Reshape node —
an ORT builder/optimizer bug, not an s1-mini or conversion problem), states
plainly this is **not** a "higher fidelity, larger download" tradeoff today,
and directs callers to `S1Mini` (INT4) for all current use. `DisplayName` was
also updated to flag the issue inline ("currently broken on CPU, see doc
comment") so it doesn't read as a normal supported variant in tooling that
surfaces just the display name. INT4 remains the sole default/recommended
variant either way — no change to that.

### Build status

`dotnet build ElBruno.LocalLLMs.csproj -p:TargetFrameworks=net8.0` — 0
warnings, 0 errors (also fixed an unrelated pre-existing `CS1574`
unresolved-`cref` warning on `KnownModels.S1Mini`'s doc comment while I was in
there). Full solution build
(`ElBruno.LocalLLMs.slnx -p:TargetFrameworks=net8.0`) has exactly the same 3
pre-existing, unrelated `BitNet.Native.*` `NoBuild=true` failures as before —
no new errors. Also independently rebuilt
`ElBruno.LocalLLMs.Tests.csproj` (Tank's in-progress test project, not
edited) and `ElBruno.LocalLLMs.BitNet.csproj` to confirm nothing downstream
regressed — both clean.



# Tank — S1-mini transcript normalizer test coverage

Date: 2026-08-19

## Summary

Added unit tests for Trinity's `TranscriptNormalizer` / `KnownModels.S1Mini`
support (`.squad/decisions/inbox/trinity-s1-mini-support.md`). All tests run
fully offline against a new fake `IChatClient` — no ONNX model, no network,
per the constraint that `elbruno/s1-mini-onnx` does not exist on HuggingFace
yet.

## Files added / changed

- `src/tests/ElBruno.LocalLLMs.Tests/TestDoubles/FakeChatClient.cs` (new) —
  minimal `IChatClient` test double: records call count, last messages,
  last `ChatOptions`, last `CancellationToken`; supports a queued or default
  response text. No suitable existing double was reusable — the only
  existing test double (`ScriptedTextGenerationModel`) fakes
  `ITextGenerationModel`, one layer below `IChatClient`.
- `src/tests/ElBruno.LocalLLMs.Tests/Normalization/TranscriptNormalizerBuildPromptTests.cs`
  (new, 10 tests) — control-line construction via the internal `BuildPrompt` seam.
- `src/tests/ElBruno.LocalLLMs.Tests/Normalization/TranscriptNormalizerTests.cs`
  (new, 20 tests) — `NormalizeAsync` behavior against `FakeChatClient`.
- `src/tests/ElBruno.LocalLLMs.Tests/Normalization/TranscriptNormalizerChunkingTests.cs`
  (new, 9 tests) — `SplitIntoChunks` / `NormalizeChunkedAsync`.
- `src/tests/ElBruno.LocalLLMs.Tests/KnownModelsTests.cs` (extended) —
  `S1Mini`/`S1MiniFp16` registry property tests, `FindById`, `All` membership.
- `src/tests/ElBruno.LocalLLMs.Tests/LocalLLMsServiceExtensionsTests.cs`
  (extended) — `AddTranscriptNormalizer` DI registration tests.

## Coverage detail

1. **Registry integrity**: `s1-mini`/`s1-mini-fp16` present in `KnownModels.All`;
   `FindById` works incl. case-insensitively; IDs unique/kebab-case (covered by
   the existing generic `KnownModelsRegistryTests` which iterate `All`, so no
   changes needed there — the two new entries are automatically exercised);
   HF repo ID is `owner/repo` shaped; `ModelSubPath` (`int4`/`fp16`) matches the
   `RequiredFiles` glob prefix (`int4/*`/`fp16/*`); `ChatTemplate == Qwen3`;
   `SupportsToolCalling == false`; `HasNativeOnnx == true`;
   `IsVisionCapable == false` (derived from `ModelType == GenAI`, not `VisionGenAI`).

2. **`BuildPrompt`**: default options produce exactly
   `[Styling: semi-formal] [Structure: prose] [Context: general]\n{transcript}`;
   every `Styling`/`Structure`/`Context` enum value maps to its documented (or
   Trinity's best-guess, for the unverified ones) lowercase wire string;
   `Lists`+`Email` combo; transcript passed through byte-for-byte (leading/
   trailing whitespace and internal casing preserved — confirmed the model
   receives the raw transcript, not a cleaned one); null `options` throws
   `ArgumentNullException`.

3. **`NormalizeAsync`**: empty, whitespace-only (space/tab/newline/CR), and
   null input all return `string.Empty` with the chat client **never**
   invoked (`CallCount == 0`); `DefaultSystemPrompt` sent verbatim by default,
   custom `SystemPrompt` override honored; `ChatOptions.Temperature == 0f`
   always; `MaxOutputTokens` reflects `MaxTokens` (default 1024, and a custom
   256); exactly 2 messages sent, `System` then `User`, user message body
   matches `BuildPrompt`'s output; response text is `.Trim()`-med; a model
   response that is itself empty/whitespace (the legitimate pure-filler case)
   yields `string.Empty` while still recording exactly 1 call (proves the
   short-circuit is input-based only, not response-based);
   `CancellationToken` passed to `NormalizeAsync` is the exact token seen by
   the chat client.

4. **Chunking**: `SplitIntoChunks` — short input → 1 chunk; long repeated-
   sentence input splits into >1 chunks each within
   `maxCharsPerChunk + one sentence` (the implementation can slightly exceed
   the hard limit by up to one trailing sentence — documented as expected,
   not a bug, since it chunks at sentence boundaries only); all original
   sentences survive reassembly; a single ~10,000-char run with no `.`/`!`/`?`
   boundaries produces one (over-limit) chunk without hanging or throwing —
   this is inherent to sentence-boundary-only chunking and matches Trinity's
   own "best-effort, not authoritative" documentation, so not flagged as a bug.
   `NormalizeChunkedAsync` — empty/whitespace short-circuits without calling
   the model; short input → exactly 1 model call; long input → exactly one
   model call per computed chunk, joined with `" "`; chunks whose cleaned
   result is empty are omitted from the joined output (not turned into extra
   whitespace).

5. **Disposal**: public constructor (`ownsChatClient: false` implicitly) does
   **not** dispose the injected `FakeChatClient`; the internal
   `(IChatClient, ownsChatClient: true)` constructor (the seam
   `CreateAsync`/`AddTranscriptNormalizer` use) **does** dispose it. Matches
   Trinity's documented design exactly — no discrepancy found. Also verified
   double-`Dispose()` is a no-op and post-dispose `NormalizeAsync` throws
   `ObjectDisposedException`.

6. **DI**: `AddTranscriptNormalizer()` registers `TranscriptNormalizer` and
   `IModelDownloader` as singletons, does **not** register `IChatClient`
   (deliberate, per Trinity's decision log), configure-action mutates the
   registered `LocalLLMsOptions` instance (default model is `S1Mini`), returns
   the same `IServiceCollection` for fluent chaining, and null `services`
   throws. Asserted directly on `ServiceDescriptor`s (`ImplementationInstance`
   for the singleton options instance, `ImplementationFactory` presence for
   the factory-registered `TranscriptNormalizer`) rather than building an
   `IServiceProvider`, since the test project only references
   `Microsoft.Extensions.DependencyInjection.Abstractions` — no
   `BuildServiceProvider()` extension is available. This mirrors how the
   pre-existing `AddLocalLLMs` tests are already written.

## Test run

`dotnet test src/tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj
--framework net8.0` → **1558 passed, 0 failed, 0 skipped** (up from 1515
before this change; net +43 new tests, no existing test broke).

## Bugs / discrepancies found

**None in the final landed code.** But one important correction to the
heads-up's own prediction: Trinity did **not** fix the ORT-GenAI
divide-by-zero by unsetting/nulling `ChatOptions.Temperature`. Instead:
- `TranscriptNormalizer.NormalizeAsync` still sends `ChatOptions.Temperature =
  0f` — unchanged — documented as the library's stable "greedy" sentinel.
- The actual crash guard lives one layer down, in
  `Execution/OnnxGenAIModel.cs` (`ApplyParameters`) and
  `Execution/OnnxVisionModel.cs` (`ApplyParameters`): the native
  `"temperature"` search option is now only set when `parameters.Temperature
  > 0`; for `Temperature <= 0`, the option is omitted entirely and
  `do_sample=false` alone selects greedy decoding, avoiding ORT-GenAI's
  divide-by-zero on a literal 0.
- Net effect: my original assertion (`ChatOptions.Temperature == 0f`) was
  actually still correct as the genuine current behavior — I kept it, but
  added a second regression test
  (`NormalizeAsync_TemperatureContractIsExactlyZero_NotAPositiveEpsilon`)
  pinning that it must stay exactly `0f` and never become a small positive
  epsilon (which would flip `do_sample` to `true` downstream and silently
  defeat greedy decoding). I did **not** add a test asserting
  "Temperature is never 0" at the `TranscriptNormalizer` level as the
  heads-up initially suggested, because that assertion is factually false
  against the correct, final implementation — the crash fix is at the
  Execution layer, not the Normalization layer. Flagging this back to the
  coordinator/Trinity since it means the Execution-layer `ApplyParameters`
  omit-on-<=0 behavior doesn't yet have direct unit test coverage in this
  PR (it would need a fake/mock at the `GeneratorParams`/native-SDK
  boundary, which is out of scope for the Normalization test brief and
  likely belongs to whoever owns `Execution/` test coverage).
- `KnownModels.S1MiniFp16` was **not** dropped — Trinity kept it, documenting
  the known FP16/GQA CPU shape-mismatch bug in its XML doc and steering
  callers to prefer `S1Mini` (INT4). My registry tests for it still hold.
- `TranscriptContext.Message`/`.Notes` were **not** dropped — Trinity kept
  both, documenting that they're empirically identical to `.General` in
  output but kept for forward-compatibility. My per-value wire-mapping tests
  for both still hold unchanged.

Disposal ownership, prompt shape, and short-circuit behavior all still match
the documented design exactly — no changes needed there.

## Final test run (after Trinity's fix landed)

`dotnet test src/tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj
--framework net8.0` → **1563 passed, 0 failed, 0 skipped** (net +48 vs.
baseline 1515).

## Execution-layer regression coverage (added after coordinator correction)

Added `Execution/OnnxVisionModelTemperatureTests.cs` (4 new tests) covering
the systemic divide-by-zero fix in `OnnxVisionModel.ApplyParameters`
(reachable via the existing `IVisionGenerationRuntime`/`IVisionSearchOptions`
test seam already used by `OnnxVisionModelTests.cs`, with a locally-owned
`RecordingVisionSearchOptions` extended to capture `SetSearchOption("temperature",
float)` and `SetSearchOption("do_sample", bool)` calls):
- `Temperature: 0f` → native `"temperature"` option is never set; `do_sample == false`.
- `Temperature: -1f` / `-0.5f` → same (any non-positive value is safe).
- `Temperature: 0.7f` → native `"temperature"` option IS set to `0.7f`; `do_sample == true`.

**Coverage gap, explicitly acknowledged (not closed):** `OnnxGenAIModel.ApplyParameters`
(the text-generation counterpart actually used by `TranscriptNormalizer`'s
`LocalChatClient`) has the identical fix but constructs a native ORT-GenAI
`GeneratorParams` directly from a real `Model` — there is no runtime-abstraction
seam for it like `IVisionGenerationRuntime`, so it cannot be unit-tested without
live model weights. Documented in the new test file's class doc comment as an
integration-test gap. Since both `ApplyParameters` methods share identical logic
and doc comments (confirmed by reading both files side by side), the vision-layer
test suite serves as a reasonable proxy for the shared logic, but does not
literally execute the text-generation code path.



# Morpheus — S1-mini Documentation

**Date:** 2026-08-19T16:02:09.824-04:00
**Author:** Morpheus (Lead / Architect)
**Requested by:** Bruno Capuano (@elbruno)

## Objective

Document the newly-added `superwhisper/s1-mini` ASR transcript-normalizer support
(Dozer's ONNX conversion + Trinity's `TranscriptNormalizer` C# API) across all
relevant docs and the root README, per the task brief.

## Files created

- `docs/transcript-normalization.md` — new, primary usage doc. Covers what
  s1-mini is/isn't, required system prompt + control-line format, C#
  `TranscriptNormalizer` quick-start (constructor, `CreateAsync`, DI via
  `AddTranscriptNormalizer`), a verified control-line behavior table, real
  before/after examples from Dozer's eval log, empty-output-on-filler
  behavior, ~1,000-token chunking guidance, and a note explaining why
  `Temperature = 0` is safe by design in the C# execution layer (corrected
  after initial review — see Discrepancies below).

## Files changed

- `docs/supported-models.md` — bumped model count 35→36, added an S1-mini row
  to the main table, added a new "🎙️ ASR Transcript Normalization" subsection
  (mirroring the "Fine-Tuned Models" subsection style) explicitly marking it as
  a task model, not chat, with tier/params/repo/ONNX status and a link to the
  new usage doc.
- `docs/onnx-conversion.md` — added a "S1-mini Conversion (ASR Transcript
  Normalizer)" section modeled on the Gemma 4 section: what the script does,
  how to run it (flags table for `--precision`, `--skip-upload`, etc.),
  disk/RAM expectations for a 0.6B model, published output layout, and a
  "Known Issues" subsection (anchor `#s1-mini-known-issues`) covering the FP16
  CPU shape-mismatch failure and the two native crash traps
  (`temperature=0.0`, `tokenizer.decode([])` on empty sequence). Notes that
  the `temperature=0.0` trap applies to raw Python scripts calling
  `onnxruntime-genai` directly, and that C# library callers are already
  protected by an execution-layer guard (corrected after initial review —
  see Discrepancies below).
- `scripts/README.md` — added a "S1-mini Conversion & Eval" section for
  `convert_s1_mini.py`/`eval_s1_mini.py` with usage examples and a link to the
  known-issues section, plus a row in the existing "Supported Models" table.
- `README.md` (root) — added a "What's New" entry for S1-mini at the top
  (see below for which entry was dropped), a Features bullet, a row in the
  Supported Models table, an entry in the Samples table for
  `TranscriptNormalizer`, and a Documentation link to the new usage doc.

## What's New entry removed to keep exactly 5

Dropped the oldest entry: **"🧩 `ElBruno.LocalLLMs.BlazorComponents`"** (the
Razor Class Library / Blazor components announcement). It was the 5th and
oldest entry before this change. The remaining 5, top to bottom, are now:
S1-mini, GPT-OSS 20B, v0.21.0, v0.20.12, v0.20.9.

## Where the FP16 known-issue was recorded, and why

Recorded it as a **"Known Issues" subsection inside `docs/onnx-conversion.md`'s
new S1-mini section** (anchor `#s1-mini-known-issues`), not in
`docs/blocked-models.md`. Reasoning: `blocked-models.md` is scoped to models
that **cannot be converted/used at all** (architecture blockers, license
gates, RAM walls, MoE routing support) — every entry there describes a model
with zero working path today. S1-mini is not blocked: the INT4 variant
converts and runs correctly, and is the shipped default. Only one *precision
variant* of one artifact (FP16, CPU execution provider only) is broken, which
is a narrower, artifact-specific runtime bug rather than a "this model is
unusable" situation. That fits the conversion guide's existing pattern of a
per-model "Troubleshooting" table (see the Gemma 4 section in the same file)
better than the blocked-models catalog. Cross-referenced from
`supported-models.md` and `transcript-normalization.md` so it's discoverable
either way.

## Discrepancies found between the task brief and actual source code

**Correction (2026-08-19, post-review):** Trinity's fixes for #1, #3, and #4
below landed while I was mid-task, so my initial read was stale. I re-verified
against current source and corrected the docs accordingly — see details under
each item.

1. **`Temperature = 0` is now safe — CORRECTED, no longer an open issue.**
   My original read found `TranscriptNormalizer.NormalizeAsync` sets
   `ChatOptions.Temperature = 0f`, and traced it through to
   `OnnxGenAIModel.cs`, which at the time appeared to forward it
   unconditionally to `SetSearchOption("temperature", ...)` — the exact crash
   Dozer found. **Re-verified current source:** `OnnxGenAIModel.cs` (lines
   257-266) now guards this with `if (parameters.Temperature > 0)` before
   calling `SetSearchOption("temperature", ...)`, with an explicit comment
   explaining the native crash trap and why `Temperature <= 0` must omit the
   call entirely (`do_sample=false` alone selects greedy decoding). The same
   fix applies to `OnnxVisionModel.cs`. **Docs corrected:** removed the
   "open P0" / "reproduces Dozer's crash" framing from both
   `docs/transcript-normalization.md` (renamed the section to "Temperature 0
   = greedy decoding (safe by design)") and `docs/onnx-conversion.md`'s Known
   Issues table. The `temperature=0.0` crash trap itself is still documented
   — it remains a real risk for **Python scripts calling `onnxruntime-genai`
   directly** (e.g. `scripts/eval_s1_mini.py`) — but both docs now explicitly
   state that C# `TranscriptNormalizer`/`LocalChatClient` callers are
   protected by the execution-layer guard.
2. **`KnownModels.S1MiniFp16` was kept — confirmed correct, no change
   needed.** It is still present in `KnownModels.cs`, with a ⚠️ warning in
   its doc comment and the non-functional status reflected in its
   `DisplayName`. Docs already documented it as published-but-non-functional
   with INT4 as the sole recommended variant; this matches Trinity's
   decision and required no correction.
3. **`TranscriptContext.Message`/`.Notes` and `TranscriptStyling`
   remarks — CORRECTED, they were NOT stale.** My original read (correctly
   at the time) found the XML remarks still said "not individually
   verified." **Re-verified current source:** `TranscriptContext.cs` now
   documents `Message` and `Notes` as "Empirically verified to behave
   identically to `General`," and `TranscriptStyling.Formal`/`.Casual` had
   their unverified hedging removed, both matching the verified findings.
   **Docs corrected:** removed the claim in
   `docs/transcript-normalization.md` that the code comments were stale;
   replaced it with a note that the code comments now agree with the
   documented table.
4. **`TranscriptStructure.Lists` — re-checked, still accurate, no
   correction needed.** Re-read `TranscriptStructure.cs` as it stands now:
   Trinity did soften the doc comment — it now explicitly states empirical
   testing did not reproduce bulleted/numbered Markdown output for `lists`
   and warns not to assume it reliably yields literal list formatting. This
   already matches the caveat in `docs/transcript-normalization.md`'s
   control-line table verbatim in spirit, so no doc change was needed here.

None of the above required any C# edits — all are documentation-only
corrections, made after re-reading the current state of
`src/ElBruno.LocalLLMs/Execution/OnnxGenAIModel.cs`,
`src/ElBruno.LocalLLMs/Normalization/TranscriptContext.cs`,
`src/ElBruno.LocalLLMs/Normalization/TranscriptStyling.cs`, and
`src/ElBruno.LocalLLMs/Normalization/TranscriptStructure.cs`.

---

## Decision 37: S1-mini Follow-up — End-to-End Verification + OnnxGenAIModel Test Seam

**Date:** 2026-08-19
**Status:** Complete
**Parent:** Decision 36 (s1-mini support)
**Agents:** Tank (Verification), Trinity (Test Seam)

### Overview

Closed two open verification gaps from Decision 36:
1. **C# path end-to-end validation** against the real published s1-mini INT4 model (Tank).
2. **Automated test coverage for text-generation temperature guard** (Trinity).

### Tank: s1-mini End-to-End Verification

**Objective:** Close the gap between Dozer's Python-driven ONNX quality validation and the C# path. Run the real `TranscriptNormalizer` against the published `elbruno/s1-mini-onnx` INT4 model and verify output matches Dozer's validated Python baseline.

**Results:**

1. **Prompt-parity check (static trace):** Verified byte-for-byte identity between:
   - `Qwen3Formatter.FormatMessages` (C# path) → system message, control-line wire values, user message, assistant suffix with `<think>\n\n</think>` block
   - `scripts/eval_s1_mini.py` hand-built prompt (Python path)
   - Model's own `chat_template.jinja` specification
   - Identical on all 6 test cases: model-card reference, email+phone, pure filler, long dictation, `[Context: email]`, `[Structure: lists]`

2. **Live end-to-end run:** Used Dozer's local `converted_models/s1-mini-onnx/int4` copy (no redundant download). Built throwaway harness, ran 6 prompts through `TranscriptNormalizer.NormalizeAsync`. Deleted harness after run (not committed).

3. **All 6 reference outputs reproduced exactly** — character-for-character match vs. Dozer's validated Python baseline.

4. **Hazard verification — all confirmed safe:**
   - `<think>` tag leakage: **Not observed** in any output
   - Empty-output safety: **Confirmed** — pure-filler case returned `string.Empty` cleanly with no exception (first real validation of incremental-decoder safety claim)
   - `Temperature = 0` crash: **Confirmed safe** across 8 live calls; first real validation of `OnnxGenAIModel.ApplyParameters` native-runtime guard
   - Greedy decoding determinism: **Confirmed** — model-card prompt run twice produced identical output

5. **Verdict:** C# `TranscriptNormalizer` path is production-ready. Prompt parity is byte-identical to Python baseline. Feature verified end-to-end against real published model.

### Trinity: OnnxGenAIModel Test Seam

**Objective:** Close automated-coverage gap on text-generation path. The `Temperature <= 0` divide-by-zero guard in `OnnxGenAIModel.ApplyParameters` (used by every chat model and `TranscriptNormalizer`) was previously only indirectly tested via vision call site, never directly via unit test on the text path.

**Changes (internal-only, zero behavior impact):**

1. Introduced `internal interface IGenerationSearchOptions` wrapping `SetSearchOption(string, int|float|bool)` — mirrors existing `OnnxVisionModel.IVisionSearchOptions` pattern exactly.

2. Introduced `file sealed class OnnxGenerationSearchOptions(GeneratorParams genParams)` — default implementation delegating straight through to real `GeneratorParams`, used on all production code paths (zero behavior change).

3. Changed `ApplyParameters` from `private static(GeneratorParams, ...)` to `internal static(IGenerationSearchOptions, ...)` — same logic, same order, same conditions.

4. Both real call sites (`Generate`, `GenerateStreamingAsync`) now wrap `GeneratorParams` in `OnnxGenerationSearchOptions` before calling `ApplyParameters`.

5. Added `src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxGenAIModelTemperatureTests.cs` (12 new tests):
   - `Temperature <= 0` → `"temperature"` never set; `do_sample = false`
   - `Temperature > 0` → `"temperature"` set; `do_sample = true`
   - `max_length` mapping: `MaxOutputTokens` → `min(MaxLength, inputTokenCount + MaxOutputTokens)`
   - `top_p`, `top_k`, `repetition_penalty` conditional logic all pinned

**Test Results:** 1575 passed / 0 failed (baseline 1563 + 12 new, no regressions).

**Verdict:** Text-path `Temperature <= 0` guard now pinned by automated tests on both vision and text execution paths. Refactor risk eliminated.

### Key Preserved Decisions

1. **C# path is empirically proven equivalent to validated Python baseline** for s1-mini — prompt parity is byte-for-byte; all 6 reference outputs match.
2. **`ChatTemplateFormat.Qwen3` is confirmed correct** for s1-mini — emits required `<think>\n\n</think>` block; no model-specific formatter needed.
3. **`Temperature = 0` native divide-by-zero guard is pinned by automated tests on both paths** — vision (4 tests) and text (12 tests). Text path previously validated only by manual run; now CI-covered.
4. **Seam convention for native-runtime testing:** Mirror existing `IVisionSearchOptions` abstraction pattern; keep `internal`; keep default impl a pure pass-through.
5. **Test suite baseline:** 1563 → **1575 passed / 0 failed**.

---

# Tank — s1-mini end-to-end verification (C# path × real published model)

Date: 2026-08-19
Author: Tank (Tester)
Parent: Decision 37

## Objective

Close the gap between Dozer's Python-driven ONNX quality validation and my
earlier `IChatClient`-double unit tests: run the real `TranscriptNormalizer`
C# code path against the real `elbruno/s1-mini-onnx` INT4 model, and compare
directly against Dozer's validated Python outputs.

## 1. Prompt-parity check — RESULT: IDENTICAL

Compared the exact prompt string produced by the C# path against
`scripts/eval_s1_mini.py`'s hand-built prompt, by tracing the code (no diff
found, so no throwaway formatter-dump test was needed — the static trace was
conclusive):

- `KnownModels.S1Mini.ChatTemplate = ChatTemplateFormat.Qwen3`
  (`src/ElBruno.LocalLLMs/Models/KnownModels.cs:590`) → `LocalChatClient`
  renders messages through `Qwen3Formatter.FormatMessages`.
- System message (no tools) → default branch:
  `<|im_start|>system\n{content}<|im_end|>\n` — identical to Python's
  `f"<|im_start|>system\n{SYSTEM_PROMPT}<|im_end|>\n"`.
  `TranscriptNormalizer.DefaultSystemPrompt` text is byte-identical to
  Python's `SYSTEM_PROMPT` constant.
- User message (no `FunctionResultContent`) → `FormatUserMessage` returns
  `message.Text` verbatim → wrapped as
  `<|im_start|>user\n{content}<|im_end|>\n` — identical to Python's
  `f"<|im_start|>user\n{user_message}<|im_end|>\n"`.
  `TranscriptNormalizer.BuildPrompt` control line
  (`[Styling: ...] [Structure: ...] [Context: ...]\n{transcript}`) uses the
  same wire values as Python's hardcoded control lines (`semi-formal`,
  `prose`, `general`, `email`, `lists` all match
  `TranscriptStyling/Structure/ContextExtensions.ToWireValue()`).
- Generation prompt suffix: `Qwen3Formatter.FormatMessages` always appends
  `<|im_start|>assistant\n<think>\n\n</think>\n\n` — identical to Python's
  hardcoded suffix, and matches the model's own `chat_template.jinja`
  (`{%- if enable_thinking is defined and enable_thinking is false %}
  {{- '<think>\n\n</think>\n\n' }}{%- endif %}` under
  `add_generation_prompt`).

**Verdict: the two prompt strings are byte-for-byte identical for every test
case in scope.** No whitespace, ordering, or `<think>`-block discrepancy
found. This was the single highest-risk unknown named in the task brief, and
it is now closed with high confidence — confirmed further by the live run
below reproducing Dozer's outputs exactly.

## 2. Live end-to-end run

- Used Dozer's existing local copy at
  `converted_models/s1-mini-onnx/int4` (present, ~394 MB `model.onnx.data` +
  config/tokenizer files) via `LocalLLMsOptions.ModelPath` +
  `EnsureModelDownloaded = false` — no redundant download needed.
- Built a throwaway console harness (`_tank_e2e_harness/`, ProjectReference
  to `ElBruno.LocalLLMs.csproj` + explicit
  `Microsoft.ML.OnnxRuntimeGenAI` 0.15.1 package reference — required
  because the library marks its own reference `PrivateAssets="native"`, so
  any executable consuming it must add its own explicit reference to get
  the native runtime DLLs; the existing `TranscriptNormalizer` sample does
  this too) that calls `TranscriptNormalizer.CreateAsync` /
  `NormalizeAsync` with the exact same six prompts from
  `scripts/eval_s1_mini.py`. **Deleted after the run** — not committed, no
  production source touched.
- Model loaded in ~2.3s; each normalization call took 0.3–0.9s on CPU.

### Three-column comparison

| Case | Input | Dozer's Python (INT4) output | Tank's C# (INT4) output |
|---|---|---|---|
| Model-card reference | `so um i need to like send the the report by uh friday no wait make that thursday` | `So I need to send the report by Thursday.` | `So I need to send the report by Thursday.` ✅ |
| Email + phone | `hey so uh my email is bruno at example dot com and i'll call you at like three thirty tomorrow` | `Hey, so my email is bruno@example.com, and I'll call you at like 3:30 tomorrow.` | `Hey, so my email is bruno@example.com, and I'll call you at like 3:30 tomorrow.` ✅ |
| Pure filler | `um uh you know like` | `` (empty) | `` (empty — `string.Empty`, no throw) ✅ |
| Long dictation | `okay so uh the the meeting is at nine am no sorry it got moved to ten thirty and uh we need like twenty five copies of the the slide deck and uh don't forget to book room number two oh and uh bring the the laptop charger this time` | `Okay, so the meeting got moved to 10:30, and we need 25 copies of the slide deck. Don't forget to book room number 20, and bring the laptop charger this time.` | `Okay, so the meeting got moved to 10:30, and we need 25 copies of the slide deck. Don't forget to book room number 20, and bring the laptop charger this time.` ✅ |
| `[Context: email]` | `hi team uh just wanted to say the the deployment went fine last night and uh no issues so far` | `Hi Team,\n\nJust wanted to say the deployment went fine last night, and no issues so far.` | `Hi Team,\n\nJust wanted to say the deployment went fine last night, and no issues so far.` ✅ |
| `[Structure: lists]` | `so uh first we need to uh order the parts then uh schedule the install and uh finally uh test everything before uh shipping it out` | `So first we need to order the parts, then schedule the install, and finally test everything before shipping it out.` | `So first we need to order the parts, then schedule the install, and finally test everything before shipping it out.` ✅ |

**Every single case reproduces Dozer's validated output character-for-character.**
No divergence found in any of the six cases.

## 3. Hazard verification

| Hazard | Result |
|---|---|
| `<think>` tag leakage | **Not observed.** Checked every output for `<think>`/`</think>` substrings (case-insensitive) — none present in any of the 6 cases. Non-thinking mode is correctly signalled and honored end-to-end. |
| Empty-output case returns `string.Empty` without throwing | **Confirmed.** The "pure filler" case returned `string.Empty` cleanly through `TranscriptNormalizer.NormalizeAsync` → `LocalChatClient` → `OnnxGenAIModel`'s incremental stream decoder, with no exception. This is the first real (non-mocked) empirical proof of Trinity's safety claim about the incremental decoder never hitting the crash-prone empty-decode path. |
| `Temperature = 0` does not crash | **Confirmed.** All 8 live calls (6 cases + 2 determinism-check calls) used `TranscriptNormalizer`'s hardcoded `Temperature = 0f`, which routes through `OnnxGenAIModel.ApplyParameters` — no native divide-by-zero, no exception, in any call. This is the first real (non-mocked) validation of `ApplyParameters` skipping `SetSearchOption("temperature")` for `Temperature <= 0`; that code path previously had no unit test and no live confirmation. |
| Determinism (greedy decoding) | **Confirmed.** Ran the model-card reference prompt twice back-to-back: both runs produced the identical string `"So I need to send the report by Thursday."` |

## Bugs found

**None.** No divergence, no crash, no tag leakage, no non-determinism.

## Verdict

**The C# `TranscriptNormalizer` path reproduces Dozer's Python-validated
INT4 output exactly, for all six of his test cases, with all four
hazards from the task brief confirmed safe.** The prompt-parity risk named
in the task brief — that `Qwen3Formatter`'s rendering might silently diverge
from Dozer's hand-built Python prompt — is fully closed: the two paths are
byte-identical, and the live run is direct empirical proof of that (matching
output would have been very unlikely if the prompts differed in any
significant way, e.g. missing `<think>` block or wrong system prompt text).

This is the first real, non-mocked, end-to-end run of the s1-mini feature
against the actual published model through the actual public C# API
surface. The feature is production-ready from a quality/correctness
perspective as tested here (CPU execution provider, single-threaded,
sequential calls; concurrency/streaming/GPU paths were out of scope for
this task and untested here).

## Cleanup

- Throwaway harness `_tank_e2e_harness/` was deleted after the run (not
  committed). No production source was modified.
- No changes made to `docs/`, `README.md`, or `scripts/*.py`.

---

# Trinity — Test seam for OnnxGenAIModel's temperature/search-option logic

**Date:** 2026-08-19
**Author:** Trinity (Core Dev)
**Status:** Done
**Parent:** Decision 37

## Context

Decision 36 recorded Trinity's fix for the ORT-GenAI native divide-by-zero
crash (`SetSearchOption("temperature", 0)` crashes even with
`do_sample=false`), applied identically to both `OnnxVisionModel.ApplyParameters`
and `OnnxGenAIModel.ApplyParameters`. Tank could unit-test the vision side
because `OnnxVisionModel` already has an `IVisionGenerationRuntime` /
`IVisionSearchOptions` seam. The text side (`OnnxGenAIModel`) had no such
seam — `ApplyParameters` built a native ORT-GenAI `GeneratorParams` directly
from a real `Model`, so the guard was only validated by one manual
end-to-end run against the real s1-mini model, not by CI. The text path is
the one that matters most in practice: every chat model and
`TranscriptNormalizer`'s underlying `LocalChatClient` go through it.

## Decision

Introduced a minimal, internal-only test seam in `OnnxGenAIModel.cs`,
directly mirroring the existing vision-side pattern rather than inventing a
new one:

- `internal interface IGenerationSearchOptions` — analogous to
  `IVisionSearchOptions` — with `SetSearchOption(string, int|float|bool)`.
- `file sealed class OnnxGenerationSearchOptions(GeneratorParams genParams)`
  — the default implementation, delegating every call straight through to
  the real `GeneratorParams`. This is the only implementation used on any
  real (non-test) code path, so runtime behavior is byte-for-byte unchanged.
- `ApplyParameters` changed from `private static(GeneratorParams, ...)` to
  `internal static(IGenerationSearchOptions, ...)`. Same logic, same order,
  same conditions — pure signature/seam change. Made `internal` (rather than
  kept `private`, unlike vision's `ApplyParameters`) because there is no
  broader internal entry point analogous to vision's `GenerateWithImagesCore`
  that a test could drive without a real `Model`; the existing
  `InternalsVisibleTo` grant to `ElBruno.LocalLLMs.Tests` covers this safely.
- Both real call sites (`Generate`, `GenerateStreamingAsync`) now construct
  `new OnnxGenerationSearchOptions(genParams)` and pass it to
  `ApplyParameters` instead of passing `genParams` directly.

Nothing here is exposed on the public API surface — everything is `internal`
or `file`-scoped, matching the vision-side scoping.

## Tests added

`src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxGenAIModelTemperatureTests.cs`
(12 new tests), with a recording fake `RecordingGenerationSearchOptions`
implementing `IGenerationSearchOptions`, mirroring the structure/naming of
Tank's `OnnxVisionModelTemperatureTests.cs`. Covers:

- `Temperature = 0f` → `"temperature"` never set; `do_sample = false`.
- `Temperature = -1f` / `-0.5f` → same (non-positive guard).
- `Temperature = 0.7f` → `"temperature"` set to `0.7f`; `do_sample = true`.
- `max_length`: `MaxOutputTokens` → `min(MaxLength, inputTokenCount +
  MaxOutputTokens)` mapping, the clamp-to-configured-context-length case, and
  the no-`MaxOutputTokens` (use `MaxLength` as-is) case.
- `top_p` always set to the configured value.
- `top_k` only set when non-null.
- `repetition_penalty` only set when `!= 1.0f`.

Deliberately did not modify `OnnxVisionModelTemperatureTests.cs` per the
task constraint to keep it passing unchanged; its "coverage gap" doc comment
describing this exact gap is now stale (the gap is closed), but updating it
was out of scope for this change — flagging as a small follow-up doc cleanup
for whoever touches that file next.

## Validation

- `dotnet build ElBruno.LocalLLMs.slnx -p:TargetFrameworks=net8.0`: 0 new
  warnings/errors from the touched files (fixed one transient `CS1574`
  cref-resolution warning from an XML doc comment during development). Same
  3 pre-existing unrelated `BitNet.Native.*`/net10.0-sample failures as
  always (`NoBuild=true` misconfiguration, unrelated to this change).
- `dotnet test src/tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj
  --framework net8.0`: **1575 passed / 0 failed** (baseline 1563 + 12 new
  tests, no regressions).

## Non-goals / explicitly not done

- No behavior change to generation logic — this is a pure testability
  refactor. Any "improvement" temptation while in `ApplyParameters` was
  deliberately resisted per the task's constraints.
- No public API surface change.
- Did not touch `TranscriptNormalizer`, `OnnxVisionModel`, `docs/`,
  `README.md`, `scripts/`, or `converted_models/`.
