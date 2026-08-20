# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## ⚠️ ALERT: S1-mini Feature Repo Placement Under Active Reconsideration

**2026-08-19 (repository boundary review):** The `TranscriptNormalizer` API placement is under re-evaluation. The original recommendation was to keep it in LocalLLMs (STAY). After Bruno's pushback citing the model card ("text normalizer for speech-to-text output"), parallel re-analysis by Morpheus and Fact Checker have reversed that recommendation. The decision now rests between MOVE (relocate impl to Space.Normalization) or SPLIT (interface in Space.Abstractions, impl stays in LocalLLMs). **All s1-mini implementation code in LocalLLMs (tests, samples, KnownModels, ORT-GenAI fix) remains committed as-is and will not change unless Bruno selects MOVE.** Awaiting Bruno's decision. See Decision 38 in `.squad/decisions.md` for full analysis.

## Latest: S1-mini follow-up — End-to-end verification + cross-team seam closure (2026-08-19)

**2026-08-19 (follow-up):** Completed Decision 37: Closed Tank's s1-mini end-to-end verification + Trinity's test seam work. Tank's live run against real `elbruno/s1-mini-onnx` INT4 reproduced all 6 of Dozer's Python-validated outputs exactly (byte-for-byte), prompts traced as byte-identical, all 4 hazards confirmed safe. Trinity introduced `IGenerationSearchOptions` test seam for `OnnxGenAIModel` (mirroring `IVisionSearchOptions` pattern), added 12 tests pinning `Temperature <= 0` guard. Test suite: 1575 passed / 0 failed. Both tasks merged into `.squad/decisions.md` (Decision 37). Orchestration logs: `2026-08-19T16-51-27-tank.md`, `2026-08-19T16-51-27-trinity.md`. Session log: `2026-08-19T16-51-27-s1-mini-e2e-and-seam.md`.

**2026-08-19 (original s1-mini test work):** Closed the last open risk on s1-mini: nobody had ever run the
real C# `TranscriptNormalizer` path against the real published
`elbruno/s1-mini-onnx` (INT4) model — Dozer validated quality from Python
with a hand-built prompt, and I had only validated the C# path against fake
`IChatClient` doubles. Ran both halves together.

- **Prompt-parity check (static trace, no diff needed):** Traced
  `KnownModels.S1Mini.ChatTemplate = Qwen3` → `Qwen3Formatter.FormatMessages`
  and compared byte-for-byte against `scripts/eval_s1_mini.py`'s hand-built
  prompt string. **Identical** — same system prompt text, same control-line
  wire values, same trailing `<|im_start|>assistant\n<think>\n\n</think>\n\n`
  block (also confirmed against the model's own `chat_template.jinja`).
- **Live run:** Built a throwaway console harness
  (`_tank_e2e_harness/`, deleted after use — not committed) referencing
  `ElBruno.LocalLLMs.csproj` + an explicit `Microsoft.ML.OnnxRuntimeGenAI`
  package reference (required because the library marks its own reference
  `PrivateAssets="native"` — same pattern the `TranscriptNormalizer` sample
  already uses). Pointed `LocalLLMsOptions.ModelPath` at Dozer's existing
  local copy (`converted_models/s1-mini-onnx/int4`) with
  `EnsureModelDownloaded = false` — no redundant download.
- **Result: all 6 of Dozer's test cases reproduced his Python output
  character-for-character** (model-card reference, email+phone, pure filler
  → empty, long dictation, `[Context: email]`, `[Structure: lists]`).
- **All 4 hazards confirmed safe:** no `<think>` tag leakage in any output;
  the pure-filler case returns `string.Empty` cleanly with no throw (first
  real proof of the incremental-decoder safety claim); `Temperature = 0`
  never crashed across 8 live calls (first real proof of
  `OnnxGenAIModel.ApplyParameters` skipping the native `temperature` search
  option); the model-card prompt run twice produced identical output
  (determinism confirmed for greedy decoding).
- **No bugs found.** Full report:
  `.squad/decisions/inbox/tank-s1-mini-e2e.md`.

## Latest: S1-mini transcript normalizer test coverage (2026-08-19)

**2026-08-19:** Added unit tests for Trinity's new `superwhisper/s1-mini` ASR
transcript normalizer support. **4 new/updated files**, all offline against a
new `FakeChatClient` test double (no ONNX model, no network — `elbruno/s1-mini-onnx`
does not exist on HF yet per Trinity's decision log):

1. **TestDoubles/FakeChatClient.cs** — minimal `IChatClient` fake recording
   call count, last messages/options/cancellation token, with queued/default
   response text. Reused nowhere previously (existing `ScriptedTextGenerationModel`
   operates at the `ITextGenerationModel` seam, one layer below `IChatClient`).
2. **Normalization/TranscriptNormalizerBuildPromptTests.cs** (10 tests) —
   control-line construction: default `[Styling: semi-formal] [Structure: prose]
   [Context: general]`, each enum's wire-value mapping, Lists+Email combo,
   verbatim (untrimmed/uncased) transcript passthrough, null-options guard.
3. **Normalization/TranscriptNormalizerTests.cs** (20 tests) — empty/whitespace/
   null short-circuit (chat client never invoked), default vs. custom
   `DefaultSystemPrompt`, `Temperature == 0`, default/custom `MaxOutputTokens`,
   exactly-2-messages-in-order, response trimming, pure-filler → empty string,
   cancellation token propagation, disposal ownership semantics for both the
   public ctor (`ownsChatClient: false`) and the internal ctor
   (`ownsChatClient: true`), double-dispose safety, `ObjectDisposedException`
   after dispose, null-chat-client guard.
4. **Normalization/TranscriptNormalizerChunkingTests.cs** (9 tests) —
   `SplitIntoChunks`: single-chunk short input, chunk-size ceiling on long
   input, lossless reassembly, unbreakable-run-longer-than-limit doesn't hang;
   `NormalizeChunkedAsync`: empty short-circuit, one-call-per-chunk, join
   behavior, empty-chunk-result omission.
5. **KnownModelsTests.cs** — added `S1Mini`/`S1MiniFp16` property tests
   (id/repo/subpath/tier/ChatTemplate=Qwen3/SupportsToolCalling=false/
   HasNativeOnnx=true/IsVisionCapable=false), shared-repo/distinct-subpath
   check, `FindById` incl. case-insensitive, `All`/static-field membership.
6. **LocalLLMsServiceExtensionsTests.cs** — `AddTranscriptNormalizer`
   registration tests (TranscriptNormalizer + IModelDownloader singletons,
   deliberately NOT registered as IChatClient, default model is S1Mini,
   fluent return, null-services guard). Followed the existing pattern of
   asserting on `ServiceDescriptor`s directly rather than building a
   provider, since the test project only references
   `Microsoft.Extensions.DependencyInjection.Abstractions` (no
   `BuildServiceProvider` extension available) — same constraint the
   existing `AddLocalLLMs` tests already work around.

**Result:** `dotnet test ... --framework net8.0` → **1563 passed, 0 failed**
(up from 1515 pre-existing). Ran final validation only after confirming
Trinity's ORT-GenAI divide-by-zero fix had landed (waited ~7 min, polling
production files). Coordinator's mid-task correction confirmed: Trinity fixed
the crash at the Execution layer (`OnnxGenAIModel`/`OnnxVisionModel.ApplyParameters`
now omit the native `"temperature"` search option when `Temperature <= 0`), NOT
by unsetting `ChatOptions.Temperature` in `TranscriptNormalizer` — that still
sends `Temperature = 0f` intentionally, as documented, and my original
assertion for it was correct all along. Added one extra regression test on
the normalizer ("exactly 0f, never a positive epsilon") plus 4 new
Execution-layer tests in `Execution/OnnxVisionModelTemperatureTests.cs`
pinning the systemic fix (temperature omitted + do_sample=false for
Temperature<=0; temperature set + do_sample=true for Temperature>0) via the
existing `IVisionGenerationRuntime` test seam. `OnnxGenAIModel.ApplyParameters`
(text-generation path) has no equivalent seam and is flagged as an
acknowledged integration-test gap, not force-tested. `S1MiniFp16` and
`TranscriptContext.Message`/`.Notes` were both kept (not dropped) — existing
tests for them stand unchanged. No bugs found in the final landed code.

## Latest: DX Implementation Test Coverage (2026-03-29)

**2026-03-29:** Delivered comprehensive unit tests for DX wave (Waves 1–4). **94 new tests** across 7 files:

1. **GpuDiagnosticsTests.cs** (24 tests) — Provider detection, CUDA/DirectML availability, graceful degradation
2. **LocalChatClientBuilderTests.cs** (18 tests) — Fluent API configuration, option chaining, builder defaults
3. **WarmupHealthCheckTests.cs** (20 tests) — Warmup success/failure, health check state validation, synthetic prompts
4. **ExceptionEnrichmentTests.cs** (16 tests) — ExecutionProviderException properties, suggestion formatting, exception hierarchy
5. **InitializationProgressTests.cs** (8 tests) — Event emission sequencing, progress reporting, cancellation
6. **LoggerIntegrationTests.cs** (5 tests) — ILogger DI, log level filtering, structured logging
7. **OptionsValidationTests.cs** (3 tests) — Async factory validation, constructor bypass, invalid option detection

**Test Results:** 484/484 passing (390 existing + 94 new). Zero regressions. Coverage >95% for Waves 2–4.

**Validation:** Tests confirm Trinity's Wave 1–4 implementations:
- Exception hierarchy with actionable suggestions
- ILogger optional integration with NullLogger defaults
- GPU diagnostics without model load
- Warmup & health check APIs
- Fluent builder pattern
- Progress event stream
- Options validation in async factory only

**Commits:** All tests merged in PR #8 (squash-merged to main).


## Previous Work Summary

Delivered across 20+ sessions since 2026-03-17:
- Magentic-ui Phase 3A (2026-07-23): 3-project ASP.NET Core orchestration, 40 tests passing
- VLM support (Fara1.5-9B, bitnet, Qwen3, Phi-4, GPT-OSS-20B)
- Conversion pipelines for ONNX, quantization strategies (INT4/FP16)
- Test coverage, CI/CD workflows, documentation standards
- DI patterns, chat template formats, model registry architecture

See decision archive for full records.
