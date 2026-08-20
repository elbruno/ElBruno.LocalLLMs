# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Latest: Removed redundant s1-mini feature from LocalLLMs (2026-08-20)

**2026-08-20T10:02-04:00:** Per Bruno's authorization ("remove all the local
redundant information"), cleanly removed the s1-mini transcript-normalization
feature from `C:\src\ElBruno.LocalLLMs` now that it lives in the standalone
`C:\src\ElBruno.S1Mini` repo (scaffolded 2026-08-19, local commit `ea8be0a`).
Generic library improvements were preserved verbatim.

**Files deleted (verbatim, verified present first):**
- `src/ElBruno.LocalLLMs/Normalization/` (5 files: `TranscriptNormalizer.cs`,
  `TranscriptNormalizerOptions.cs`, `TranscriptStyling.cs`,
  `TranscriptStructure.cs`, `TranscriptContext.cs`)
- `src/tests/ElBruno.LocalLLMs.Tests/Normalization/` (3 files:
  `TranscriptNormalizerTests.cs`, `TranscriptNormalizerBuildPromptTests.cs`,
  `TranscriptNormalizerChunkingTests.cs`)
- `src/tests/ElBruno.LocalLLMs.Tests/TestDoubles/FakeChatClient.cs` — grep
  confirmed only the two now-deleted Normalization test files referenced it.
- `src/samples/TranscriptNormalizer/` (whole sample dir: `Program.cs`,
  `TranscriptNormalizer.csproj`, plus `bin/`/`obj/`)
- `scripts/convert_s1_mini.py`, `scripts/eval_s1_mini.py`
- `docs/transcript-normalization.md`

**Files edited:**
- `src/ElBruno.LocalLLMs/LocalLLMsServiceExtensions.cs` — removed the
  `AddTranscriptNormalizer(...)` extension method and its
  `using ElBruno.LocalLLMs.Normalization;`. Left `AddLocalLLMs` and
  `AddLocalVisionLLM` untouched.
- `src/ElBruno.LocalLLMs/Models/KnownModels.cs` — removed
  `KnownModels.S1Mini` + `KnownModels.S1MiniFp16` definitions, their two
  entries in `All`, and the `"🎙️ Speech post-processing"` section-header
  comment block that only wrapped them.
- `src/tests/ElBruno.LocalLLMs.Tests/KnownModelsTests.cs` — removed the
  entire "S1-mini (elbruno/s1-mini-onnx) — ASR transcript normalizer" test
  section (3 `[Fact]` + 3 `[Theory]` w/ 2 InlineData each = 9 tests) and the
  two `Assert.Contains(KnownModels.S1Mini/S1MiniFp16, ...)` lines inside
  `StaticFields_AreSameInstancesAsInAll`. Rest of the file unchanged.
- `src/tests/ElBruno.LocalLLMs.Tests/LocalLLMsServiceExtensionsTests.cs` —
  removed the entire `AddTranscriptNormalizer` region (9 `[Fact]`s) and the
  `using ElBruno.LocalLLMs.Normalization;`.
- `src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxGenAIModelTemperatureTests.cs`
  — cleaned one XML-doc reference to `<c>TranscriptNormalizer</c>` in the
  class doc-comment. Test bodies untouched — this file **stays**, all 12
  tests still present and passing.
- `src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxVisionModelTemperatureTests.cs`
  — cleaned two XML-doc references (`"not just s1-mini"` phrase +
  `<c>TranscriptNormalizer</c>`) in the class doc-comment. Test bodies
  untouched — this file **stays**, all 4 tests still present and passing.
- `ElBruno.LocalLLMs.slnx` — removed the
  `<Project Path="src/samples/TranscriptNormalizer/TranscriptNormalizer.csproj" />`
  entry.
- `README.md` — removed the s1-mini "What's New" bullet; **restored the
  BlazorComponents "What's New" bullet** (recovered verbatim from git commit
  `83488b1 docs: keep release highlights at five entries`, the commit that
  dropped it to make room for s1-mini); What's New count back to exactly 5.
  Also removed: the ASR-transcript-normalization Features bullet, the
  `🎙️ Task | S1-mini` row from the Models table, the TranscriptNormalizer
  entry from the Samples table, and the Transcript Normalization entry from
  the Documentation list. No other s1-mini strings remain.
- `docs/supported-models.md` — removed the s1-mini row from the Next-Gen
  table and the entire `### 🎙️ ASR Transcript Normalization` section.
- `docs/onnx-conversion.md` — removed the entire
  `## S1-mini Conversion (ASR Transcript Normalizer)` section
  (including its Known Issues sub-block referenced from the See Also list)
  and the See Also link to `transcript-normalization.md`.
- `scripts/README.md` — removed the `## S1-mini Conversion & Eval` section
  and the s1-mini row from its Supported Models table.

**CHANGELOG handling:** `docs/CHANGELOG.md` had **no** s1-mini/superwhisper/
TranscriptNormalizer entry (grepped exhaustively). Nothing to remove and
nothing to migrate. The unreleased section already tracks the GPT-OSS work.
Did not add a "moved to standalone repo" note because the task said to add
one only if an entry sat under a **released** version; there was no entry
at all.

**Kept deliberately (per constraint — generic library fixes):**
- `Execution/OnnxGenAIModel.cs` `Temperature <= 0` native divide-by-zero
  guard + `internal interface IGenerationSearchOptions` +
  `OnnxGenerationSearchOptions` seam + `internal static ApplyParameters` —
  untouched.
- `Execution/OnnxVisionModel.cs` matching `Temperature <= 0` guard +
  `IVisionSearchOptions` seam — untouched.
- `src/tests/.../Execution/OnnxGenAIModelTemperatureTests.cs` (12 tests) and
  `.../OnnxVisionModelTemperatureTests.cs` (4 tests) — kept in full,
  only two XML doc comments cleaned. **Explicit filter run:
  `--filter "FullyQualifiedName~TemperatureTests"` → 16 passed / 0 failed.**

**Sweep result:** `grep -i -r` for `s1-mini | S1Mini | s1_mini |
TranscriptNormalizer | TranscriptStyling | TranscriptStructure |
TranscriptContext | superwhisper` across the whole repo returned matches
**only** in `.squad/` (agents' history, decisions.md — protected, do not
modify) and zero matches under `src/`, `docs/`, `scripts/`, `README.md`,
`ElBruno.LocalLLMs.slnx`. Clean.

**Verification:**
- `dotnet build src\tests\ElBruno.LocalLLMs.Tests\ElBruno.LocalLLMs.Tests.csproj -p:TargetFrameworks=net8.0`
  → **0 warnings / 0 errors**.
- `dotnet test src\tests\ElBruno.LocalLLMs.Tests\ElBruno.LocalLLMs.Tests.csproj --framework net8.0`
  → **Passed: 1497, Failed: 0, Skipped: 0, Total: 1497, Duration 601 ms**.
- Delta reconciliation: `1575 (baseline) - 1497 (new) = 78 tests removed`.
  Breakdown: 60 removed from `Normalization/` (3 files: 12 KB + 4 KB + 6 KB
  of test code), 9 removed from `LocalLLMsServiceExtensionsTests`
  (`AddTranscriptNormalizer` region), 9 removed from `KnownModelsTests`
  (3 `[Fact]` + 3 `[Theory]` × 2 InlineData). 60 + 9 + 9 = 78. ✅
- `--filter "FullyQualifiedName~TemperatureTests"` → **16 passed / 0
  failed** (12 from `OnnxGenAIModelTemperatureTests` + 4 from
  `OnnxVisionModelTemperatureTests`). ✅
- `dotnet build ElBruno.LocalLLMs.slnx -p:TargetFrameworks=net8.0` reports
  6 errors, all pre-existing and unrelated to this change:
  1. `BitNet.Native.win-x64` — `NoBuild=true` (known, per task).
  2. `BitNet.Native.linux-x64` — same.
  3. `BitNet.Native.osx-arm64` — same.
  4. `samples/ZeroCloudRag` — `NETSDK1005` missing net10.0 restore assets
     (from unrelated GPT-OSS/Harmony uncommitted work; not on the do-touch
     list).
  5. `samples/BitNetPerformance` — same `NETSDK1005`.
  6. `samples/BitNetChat` — same `NETSDK1005`.
  None of these projects were modified by this task.

**Constraint checks:**
- No commits made — all changes in the working tree, `git status` shows
  only `M` on the files listed above (plus `D` on deleted files).
- Nothing under `C:\src\ElBruno.S1Mini\` touched.
- Nothing under `C:\src\ElBruno.Speech\` touched.
- Nothing under any repo's `.squad/` touched by this session's edits
  (aside from this history append).
- No changes to unrelated uncommitted GPT-OSS/Harmony files, `docs/plans/*`,
  or `docs/tests/*`.

**Ambiguity flag (nothing kept "just in case" — all removals matched the
spec verbatim):** none. Every candidate removal on the list was found and
removed; no borderline "is this s1-mini or generic?" call had to be made
beyond the temperature guards, which the spec explicitly said to keep.

## ⚠️ ALERT: S1-mini Feature Repo Placement Under Active Reconsideration

**2026-08-19 (repository boundary review):** The `TranscriptNormalizer` API placement is under re-evaluation. The original recommendation was to keep it in LocalLLMs (STAY). After Bruno's pushback citing the model card ("text normalizer for speech-to-text output"), parallel re-analysis by Morpheus and Fact Checker have reversed that recommendation. The decision now rests between MOVE (relocate impl to Space.Normalization) or SPLIT (interface in Space.Abstractions, impl stays in LocalLLMs). **All s1-mini implementation code in LocalLLMs (tests, samples, ORT-GenAI fix) remains as-is and will not change unless Bruno selects MOVE.** Awaiting Bruno's decision. See Decision 38 in `.squad/decisions.md` for full analysis.

## Latest: Scaffolded standalone repo ElBruno.S1Mini at C:\src\ElBruno.S1Mini (2026-08-19)

**2026-08-19:** Per Bruno's decision to make s1-mini its own repo (one-model-per-repo convention), scaffolded `C:\src\ElBruno.S1Mini\` locally — no `gh repo create`, no push, no NuGet publish. Followed `C:\src\ElBruno.Whisper\` as the template. `git init` + initial commit `ea8be0a` in the new repo only; nothing committed in ElBruno.LocalLLMs or ElBruno.Speech.

**Contents produced (24 files):**
- Root: `Directory.Build.props`, `global.json` (`rollForward: latestMajor`), `.gitignore`, `LICENSE` (MIT, 2026), `ElBruno.S1Mini.slnx`, `README.md`, `docs/`, `images/`, `scripts/`, `.github/workflows/`.
- Library `src/ElBruno.S1Mini/` (multi-target `net8.0;net10.0`): `S1MiniOptions`, `S1MiniClient` (self-contained `IChatClient`), `S1MiniServiceExtensions.AddTranscriptNormalizer`, `Internal/Qwen3PromptBuilder`, `Internal/OnnxGenAIRuntime` (+ `IGenerationSearchOptions` seam), `Internal/ModelResolver` (HF glob resolution + cache check + download via `ElBruno.HuggingFace.Downloader`), `Normalization/TranscriptStyling|Structure|Context`, `Normalization/TranscriptNormalizerOptions`, `Normalization/TranscriptNormalizer`.
- Tests `src/tests/ElBruno.S1Mini.Tests/` (net8.0): ported `TranscriptNormalizerTests` (18), `TranscriptNormalizerBuildPromptTests` (11), `TranscriptNormalizerChunkingTests` (9), plus new `Internal/OnnxGenAIRuntimeTemperatureTests` (12) exercising the divide-by-zero guard via the `IGenerationSearchOptions` recording fake, plus `TestDoubles/FakeChatClient`, `GlobalUsings.cs`. Uses `xunit` 2.9.2 + `Microsoft.NET.Test.Sdk` 17.11.1 + `coverlet.collector` 6.0.2.
- Sample `src/samples/HelloS1Mini/` (net8.0): console demo covering default normalization, `Context.Email`, `Structure.Lists`, and pure-filler → empty output. Reads `S1MINI_MODEL_PATH`.
- Docs: `README.md` (badges, packages table, install, quick start, control-line docs with all empirically-verified caveats, DI, FP16 warning, license, "What's New" section with 5 entries, all author links per convention), `docs/getting-started.md`, `docs/transcript-normalization.md` (full behavior table).
- Scripts: `scripts/convert_s1_mini.py` + `scripts/eval_s1_mini.py` (copied verbatim from LocalLLMs, unchanged — they build the ONNX artifact for `elbruno/s1-mini-onnx`) + `scripts/README.md`.
- Workflows: `.github/workflows/build.yml` (CI: net8.0 restore/build + full test), `.github/workflows/publish.yml` (OIDC via `NuGet/login@v1`, no API-key secret, `NUGET_USER` from `release` environment, tag/`workflow_dispatch`/csproj version extraction with regex validation, `--skip-duplicate`).

**Hard-won correctness details preserved (all present in the code, not just docs):**
1. Temperature ≤ 0 guard: `OnnxGenAIRuntime.ApplyParameters` never calls `SetSearchOption("temperature", ...)` for non-positive temperature — `do_sample=false` alone. 3 InlineData rows in the temperature tests exercise `0f`, `-1f`, `-0.5f`.
2. Empty output is correct — `TranscriptNormalizer.NormalizeAsync` short-circuits empty/whitespace input to `string.Empty` without a model call and returns empty for pure-filler zero-token completions.
3. Never batch-decodes an empty token array — `OnnxGenAIRuntime.Generate` uses `Tokenizer.CreateStream()` and decodes one token at a time.
4. Qwen3 prompt shape byte-exact — `Qwen3PromptBuilder` emits `<|im_start|>{role}\n{content}<|im_end|>\n` per message and terminates with `<|im_start|>assistant\n<think>\n\n</think>\n\n` (verbatim from `chat_template.jinja` in non-thinking mode).
5. Control line format: `[Styling: x] [Structure: y] [Context: z]\n{transcript}`.
6. `MaxTokens` default 1024. Greedy decoding via `Temperature = 0f`.
7. Documented control-value caveats verbatim: `Styling` formal/semi-formal/casual distinct, `Structure.Lists` may not produce literal bullets, `Context.Message`/`Notes` behave identically to `General`.
8. FP16 called out as **broken on CPU** with `onnxruntime-genai` 0.15.1 in README + docs. Only INT4 is supported by the library out of the box (`S1MiniOptions.ModelSubPath = "int4"`, `RequiredFiles = ["int4/*"]`). Did not add an FP16 option — Bruno can toggle `ModelSubPath` if they must, but it's not surfaced as a documented working choice.
9. Model license notice — README documents the s1-mini Apache-2.0 + naming clause and marks this as an explicitly unofficial, unaffiliated, non-endorsed derivative. The C# code is MIT.
10. Vendor 94.8% token accuracy attributed as vendor claim, not our measurement. English only, v1.

**Verification (`dotnet` — .NET 10.0.400 SDK only on this machine; `rollForward: latestMajor` picks that up):**
- `dotnet build src\ElBruno.S1Mini\ElBruno.S1Mini.csproj -p:TargetFrameworks=net8.0` → **0 warnings / 0 errors**.
- `dotnet build src\ElBruno.S1Mini\ElBruno.S1Mini.csproj` (both `net8.0;net10.0`) → **0 warnings / 0 errors**.
- `dotnet build src\tests\ElBruno.S1Mini.Tests\ElBruno.S1Mini.Tests.csproj` → **0 warnings / 0 errors**.
- `dotnet build src\samples\HelloS1Mini\HelloS1Mini.csproj` → **0 warnings / 0 errors**.
- `dotnet build ElBruno.S1Mini.slnx` (full solution, both TFMs) → **0 warnings / 0 errors**.
- `dotnet test src\tests\ElBruno.S1Mini.Tests\ElBruno.S1Mini.Tests.csproj --framework net8.0` → **Passed! Failed: 0, Passed: 56, Skipped: 0, Total: 56, Duration: 146 ms.**

**Git:** `git init -b main`; single commit `ea8be0a` `Initial scaffold: ElBruno.S1Mini standalone repo`. No remote, no push.

**Improvised / flagged for Bruno:**
- `images/nuget_logo.png` **not created** — I don't fabricate binaries. The library csproj `<None Include="..\..\images\nuget_logo.png" Pack="true" PackagePath="" Condition="Exists('..\..\images\nuget_logo.png')" />` is wrapped in a `Condition="Exists(...)"` so builds/packs still succeed until Bruno drops the logo in. **Action needed:** copy `nuget_logo.png` from another repo into `C:\src\ElBruno.S1Mini\images\` before the first NuGet publish.
- No `.NET 8 SDK` on this machine — verified with `dotnet --list-sdks` (10.0.111 / 10.0.302 / 10.0.303 / 10.0.400). `global.json` uses `rollForward: latestMajor`, so builds succeed under .NET 10 targeting net8.0 via the shipped ref packs. CI workflow explicitly requests `8.0.x` + `10.0.x` for the setup-dotnet step.
- `S1MiniClient.BuildParameters` maps `ChatOptions.FrequencyPenalty` → `RepetitionPenalty = 1.0 + FrequencyPenalty` as a best-effort semantic bridge; noted in code comment.
- `ModelResolver` re-implements the HF-API glob-resolution logic instead of adding a runtime dependency on `ElBruno.LocalLLMs`'s internal `ModelDownloader`. Self-contained by design.

**What in `C:\src\ElBruno.LocalLLMs` is now redundant** (candidates for later removal — I did NOT touch these, per the hard constraint):
- `src/ElBruno.LocalLLMs/Normalization/` (5 files: `TranscriptNormalizer.cs`, `TranscriptNormalizerOptions.cs`, `TranscriptStyling.cs`, `TranscriptStructure.cs`, `TranscriptContext.cs`).
- `src/ElBruno.LocalLLMs/LocalLLMsServiceExtensions.cs` → the `AddTranscriptNormalizer(...)` extension method + its 60-line implementation.
- `src/tests/ElBruno.LocalLLMs.Tests/Normalization/` (3 test files: `TranscriptNormalizerTests.cs`, `TranscriptNormalizerBuildPromptTests.cs`, `TranscriptNormalizerChunkingTests.cs`) plus `TestDoubles/FakeChatClient.cs` if not used by any other tests.
- `src/samples/TranscriptNormalizer/` (whole sample project — must also be de-registered from `ElBruno.LocalLLMs.slnx`).
- `docs/transcript-normalization.md`.
- `scripts/convert_s1_mini.py`, `scripts/eval_s1_mini.py` (now live in the new repo).
- `KnownModels.S1Mini` and `KnownModels.S1MiniFp16` in `src/ElBruno.LocalLLMs/Models/KnownModels.cs` — the standalone repo no longer needs LocalLLMs to know about s1-mini. Removing them will also require removing them from the `KnownModels.All` list and from any tier-lookup or count-based tests.

The temperature-0 guard in `Execution/OnnxGenAIModel.cs` and `Execution/OnnxVisionModel.cs`, and the `IGenerationSearchOptions` / `IVisionSearchOptions` seams, are **general** fixes benefiting every model in LocalLLMs — those should stay.

---

## Latest: S1-mini follow-up — Test seam + cross-verification closure (2026-08-19)

**2026-08-19 (follow-up):** Completed Decision 37: Tank's end-to-end verification + Trinity's test-seam work. Closed automated-coverage gap on text-generation temperature guard. Introduced `internal interface IGenerationSearchOptions` test seam in `OnnxGenAIModel.cs` (mirroring existing `IVisionSearchOptions` pattern exactly), with pure pass-through `OnnxGenerationSearchOptions` default impl — zero behavior change on production paths. Added `src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxGenAIModelTemperatureTests.cs` (12 new tests) covering `Temperature <= 0` guard, max_length mapping, top_p/top_k/repetition_penalty conditional logic. Test suite: 1575 passed / 0 failed (baseline 1563 + 12 new). Both Trinity's seam work and Tank's e2e verification merged into `.squad/decisions.md` (Decision 37). Orchestration logs: `2026-08-19T16-51-27-trinity.md`, `2026-08-19T16-51-27-tank.md`. Session log: `2026-08-19T16-51-27-s1-mini-e2e-and-seam.md`.

## Latest: S1-mini ASR transcript normalizer support (2026-08-19)
of s1-mini INT4/FP16. (1) Fixed the actual root cause of the `Temperature=0`
native crash in `Execution/OnnxGenAIModel.cs` and `OnnxVisionModel.cs`'s
`ApplyParameters` — the native `SetSearchOption("temperature", ...)` call is
now skipped whenever `Temperature <= 0` (do_sample=false alone selects greedy),
fixing this crash for every model in the library, not just s1-mini;
`TranscriptNormalizer` itself was left unchanged (still sends
`ChatOptions.Temperature = 0f`, which is now safe). (2) Confirmed the
empty-generation decode path was already safe — the execution layer only ever
decodes one token at a time, never a batch/empty array, so no guard was
needed. (3) Updated `TranscriptStyling`/`TranscriptContext`/`TranscriptStructure`
XML docs to reflect empirical findings: Formal/Casual confirmed distinct
(hedging removed); Context.Message/Notes kept but documented as behaviorally
identical to General; Structure.Lists softened to not promise literal bullet
output. (4) Kept `KnownModels.S1MiniFp16` but added a prominent doc-comment
warning that it is non-functional on CPU with onnxruntime-genai 0.15.1 (ORT
GQA/repeat_kv shape-mismatch bug) and pointed callers to INT4. Full details in
`.squad/decisions/inbox/trinity-s1-mini-support.md`. Build clean (0
warnings/errors on the library and every touched/downstream project); solution
build has the same 3 pre-existing unrelated `BitNet.Native.*` failures as
before.

## Latest: S1-mini ASR transcript normalizer support (2026-08-19)

**2026-08-19:** Added first-class support for `superwhisper/s1-mini` (0.6B Qwen3
fine-tune, single-task ASR transcript normalizer — not a chat model). Added
`KnownModels.S1Mini`/`S1MiniFp16` (repo `elbruno/s1-mini-onnx`, `int4`/`fp16`
subfolders, reuses `ChatTemplateFormat.Qwen3` — no new template format needed).
New `ElBruno.LocalLLMs.Normalization` namespace: `TranscriptStyling`,
`TranscriptStructure`, `TranscriptContext` enums, `TranscriptNormalizerOptions`,
and `TranscriptNormalizer` (wraps `IChatClient`, `CreateAsync` factory, greedy
decoding, empty-input short circuit, internal `BuildPrompt`/`SplitIntoChunks`
seams for Tank's tests). Added `AddTranscriptNormalizer()` DI extension and a
`src/samples/TranscriptNormalizer/` sample (registered in the .slnx). Full
details in `.squad/decisions/inbox/trinity-s1-mini-support.md`. Build clean on
all touched/added projects; solution-level build has 3 pre-existing unrelated
failures in `ElBruno.LocalLLMs.BitNet.Native.*` (hard-coded `NoBuild=true`).


## Latest: Test seam for OnnxGenAIModel temperature guard (2026-08-19)

**2026-08-19:** Closed the text-path coverage gap Tank flagged: the
`Temperature <= 0` divide-by-zero guard in `OnnxGenAIModel.ApplyParameters`
(the path every chat model and `TranscriptNormalizer` flows through) was
previously only pinned indirectly via the vision call site, since
`GeneratorParams` was constructed directly from a real `Model` with no seam.
Introduced `internal interface IGenerationSearchOptions` (wrapping
`SetSearchOption(name, int|float|bool)`) mirroring `OnnxVisionModel`'s existing
`IVisionSearchOptions` pattern exactly, plus a `file sealed class
OnnxGenerationSearchOptions` default implementation that delegates straight to
the real `GeneratorParams` — zero behavior change on the real runtime path.
Changed `ApplyParameters` from `private static(GeneratorParams, ...)` to
`internal static(IGenerationSearchOptions, ...)`; both real call sites
(`Generate`, `GenerateStreamingAsync`) now wrap their `GeneratorParams` in
`OnnxGenerationSearchOptions` before calling it. Added
`src/tests/ElBruno.LocalLLMs.Tests/Execution/OnnxGenAIModelTemperatureTests.cs`
(12 new tests) with a recording fake covering: Temperature 0/-1/-0.5 →
`"temperature"` never set + `do_sample=false`; Temperature 0.7 →
`"temperature"=0.7` + `do_sample=true`; `max_length`
(`MaxOutputTokens`→`min(MaxLength, inputTokenCount+MaxOutputTokens)` mapping,
with/without clamping and without `MaxOutputTokens`); `top_p` always set;
`top_k` only when non-null; `repetition_penalty` only when `!= 1.0f`. Did not
touch `OnnxVisionModelTemperatureTests.cs` (kept passing unchanged, per
constraint) even though its "coverage gap" doc comment is now stale — flagging
that as a follow-up doc cleanup, not done here. Build clean (0 warnings on the
touched library/test projects); solution build has the same 3 pre-existing
unrelated `BitNet.Native.*`/net10.0-sample failures as before. Full test suite:
1575 passed / 0 failed (baseline 1563 + 12 new). Full details in
`.squad/decisions/inbox/trinity-onnxgenai-test-seam.md`.

## Previous Work Summary

Delivered across 20+ sessions since 2026-03-17:
- Magentic-ui Phase 3A (2026-07-23): 3-project ASP.NET Core orchestration, 40 tests passing
- VLM support (Fara1.5-9B, bitnet, Qwen3, Phi-4, GPT-OSS-20B)
- Conversion pipelines for ONNX, quantization strategies (INT4/FP16)
- Test coverage, CI/CD workflows, documentation standards
- DI patterns, chat template formats, model registry architecture

See decision archive for full records.


---

## 2026-08-20 10:12 EDT — Ported NuGet release automation to ElBruno.S1Mini

**Task:** Port LocalLLMs' NuGet release workflow to the standalone `C:\src\ElBruno.S1Mini\` repo so releases publish identically.

**Files created in S1Mini:**
- `.editorconfig` — copied verbatim from LocalLLMs.
- `scripts/Validate-PackageAssemblyVersions.ps1` — copied; only the slnx-name sentinel changed to `ElBruno.S1Mini.slnx`.
- `scripts/Set-ReleaseVersion.ps1` — rewritten. Single packable project. Bumps csproj `<Version>` + README What's New (5-bullet cap). Dropped LocalLLMs' `Directory.Build.props <PublishedSiblingPackageVersion>` and `docs/CHANGELOG.md` steps — neither exists in S1Mini.
- `scripts/Validate-ReleaseVersion.ps1` — rewritten to match the new Set script (csproj `<Version>` + 5-bullet README + optional packed-assembly cross-check).
- `scripts/run-tests.ps1` / `scripts/run-tests.sh` — simplified. Build + unit tests only. Dropped LocalLLMs' integration-test project references, `docs/tests/*-run-results.md` scanner, and `Start-Transcript` logging — no integration tests in S1Mini.
- `scripts/README.md` — rewritten to document all new scripts alongside existing Python ones.

**Files rewritten in S1Mini:**
- `.github/workflows/publish.yml` — mirrors LocalLLMs' `publish.yml` exactly, adapted for the single `ElBruno.S1Mini` package. Uses `dotnet msbuild ... -getProperty:Version` fallback, README What's New validator (awk, 5 bullets), and `Validate-PackageAssemblyVersions.ps1` post-pack. OIDC via `NuGet/login@v1` + `secrets.NUGET_USER` — no API keys.
- `.github/workflows/build.yml` — rewritten to match LocalLLMs' `ci.yml` (slnx-wide restore/build/test, TRX logger, artifact upload).
- `README.md` — converted `## What's New` from numbered list to 5 bullet entries so the ported validator passes as-is. First bullet now starts with `- 🎉 **`v0.1.0`** — …` so the "first bullet mentions v$Version" check works after the first bump.
- `Directory.Build.props` + `src/ElBruno.S1Mini/ElBruno.S1Mini.csproj` — made `<PackageIcon>` conditional on `Exists(...)` so `dotnet pack` succeeds without `images/nuget_logo.png`. The scaffold's `<None Include=... Condition="Exists(...)">` guard alone was insufficient — NuGet still failed with NU5046 because the icon property was set unconditionally.

**What's New format decision:** chose Bruno's option (a) — kept the validator as-is and adjusted the README to 5 bullets. All 5 entries are real, drawn from the actual initial-release feature set already documented in the numbered list (initial release, S1MiniClient, Qwen3 prompt, ORT temperature-0 guard, empirically-verified control-line docs). No fabrication. Alternative (adapt the validator to `≤5`) would have needed no README edit but would drift the two repos' publish policies, defeating the "publish the same way" goal.

**Deliberately skipped:**
- Model-conversion PowerShell (`convert_gemma4.ps1`, `delete-models.ps1`, `manage-models.ps1`) — LocalLLMs-specific.
- Squad-product workflows (`squad-ci.yml`, `squad-docs.yml`, `squad-insider-release.yml`, `squad-label-enforce.yml`, `squad-main-guard.yml`, `squad-preview.yml`, `squad-promote.yml`, `squad-release.yml`) — those govern Squad's own product-release lifecycle, not a consumer repo.
- BitNet workflows (`build-bitnet-native.yml`, `publish-bitnet-native.yml`) — LocalLLMs-specific native package.
- Gemma4/finetune workflows (`monitor-gemma4-blocker.yml`, `validate-finetune.yml`) — LocalLLMs-specific.
- `squad.config.ts` — Squad-product-specific.

**Verification:**
- `dotnet build ElBruno.S1Mini.slnx` — 0 warnings, 0 errors, 3.1 s.
- `dotnet test src/tests/ElBruno.S1Mini.Tests --framework net8.0` — **56 passed / 0 failed** (matches scaffold baseline).
- YAML lint via `python -c "import yaml; yaml.safe_load(...)"` on both `publish.yml` and `build.yml` — parsed clean.
- `dotnet msbuild src/ElBruno.S1Mini/ElBruno.S1Mini.csproj -nologo -getProperty:Version` — returned `0.1.0`.
- `dotnet pack` — produced `ElBruno.S1Mini.0.1.0.nupkg` (58 KB, both `net8.0` and `net10.0` lib dirs).
- `Validate-PackageAssemblyVersions.ps1 -PackageDirectory ./artifacts` — OK for both net8.0 and net10.0 lib DLLs at 0.1.0.0.
- `Validate-ReleaseVersion.ps1 -Version 0.1.0 -PackageDirectory ./artifacts` — all checks green.
- Regex-based What's New scan — 5 bullets confirmed, first mentions `v0.1.0`.
- Cleaned up `artifacts/` after validation.

**Still missing before Bruno can cut a real NuGet release:**
1. `images/nuget_logo.png` — Bruno is generating via t2i. Icon guard now works so builds don't block on it.
2. GitHub repository `elbruno/ElBruno.S1Mini` (may not exist yet; branch/remote push).
3. GitHub environment `release` with `NUGET_USER` secret set to Bruno's NuGet.org username.
4. NuGet.org trusted-publisher configured for the `elbruno/ElBruno.S1Mini` repo + `publish.yml` workflow + `release` environment.
5. Cutting a GitHub release tagged `v0.1.0` (or manual `workflow_dispatch` on `publish.yml`).

No commits made in either repo — changes left in S1Mini's working tree per Bruno's instruction.
