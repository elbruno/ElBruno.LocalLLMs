


## Round 5: ElBruno.S1Mini Bootstrap `FIRST_PROMPT.md` (2026-08-20)

**2026-08-20T10:21-04:00:** Bruno asked for a disposable, phased bootstrap prompt file at `C:\src\ElBruno.S1Mini\FIRST_PROMPT.md` — addressed to a coding agent (Copilot CLI / Squad), covering repo bring-up from local-only scaffold through first NuGet release. Delivered a 9-phase plan (0 Preflight → 1 Icon → 2 GitHub repo → 3 Model verification → 4 C# review → 5 Web app sample → 6 NuGet trusted-publishing → 7 First release v0.1.0 → 8 Post-launch). Repo untouched otherwise; nothing committed.

**Phase structure & rationale:**
- Kept Bruno's suggested 0–8 numbering. Phase 3 explicitly framed as *verify, do not re-upload* — `elbruno/s1-mini-onnx` already exists on HF with int4/fp16/model card/LICENSE, and re-running `convert_s1_mini.py` without `--skip-upload` would clobber a working artifact every existing `S1MiniClient.CreateAsync()` call downloads. Called out as a stop-and-ask.
- Phase 6 (NuGet trusted publishing) split into three sub-steps: `release` env creation (agent can do), `NUGET_USER` secret (STOP-AND-ASK — Bruno picks CLI vs browser), nuget.org trusted-publisher browser policy (STOP-AND-ASK — verbatim click-through instructions with the exact Repository Owner / Workflow filename / Environment values from the real `publish.yml`).
- Phase 7 verification gate is `dotnet add package` from a clean scratch project running the reference transcript against the real model — not just "workflow succeeded."
- Rollback/troubleshooting section covers NU5046, OIDC failure, What's-New 5-bullet validator, ORT-GenAI native crashes, HF download failures, `--force` push refusal, `.slnx` hand-edit note.

**Verified against real repo files (not guessed):**
- `publish.yml` uses `NuGet/login@v1` with `user: ${{ secrets.NUGET_USER }}`, `permissions: id-token: write`, `environment: release`. All four values (`elbruno`, `ElBruno.S1Mini`, `publish.yml`, `release`) plumbed into the Phase 6.3 browser instructions verbatim.
- `Directory.Build.props` and `ElBruno.S1Mini.csproj` guard `<PackageIcon>` and the `<None Include=...>` with `Condition="Exists(...)"` — confirmed the pack stays green without the icon; Phase 1 verification just extracts the nupkg and checks the file is embedded.
- `convert_s1_mini.py` flags checked: `--precision {int4,fp16,both}`, `--skip-upload`, `--skip-conversion`, `--output-dir`, `--cache-dir`.
- `eval_s1_mini.py` signature checked: `--model-dir` is **required** and points to a variant subfolder (int4/ or fp16/), not the repo root — the plan reflects this.
- `Set-ReleaseVersion.ps1` / `Validate-ReleaseVersion.ps1` / `Validate-PackageAssemblyVersions.ps1` invocation shapes copied from the current `scripts/README.md`.
- Solution is `.slnx` (XML), current one commit `ea8be0a`, no remote — matches Bruno's brief.

**Non-negotiable technical invariants preserved verbatim in the prompt:**
1. Temperature-0 ORT-GenAI divide-by-zero guard (`Internal/OnnxGenAIRuntime.cs` + `OnnxGenAIRuntimeTemperatureTests.cs`) — never remove.
2. No batch-decode of zero-length token arrays.
3. Empty output is correct for pure-filler input — `string.Empty`, not throw.
4. Qwen3 ChatML byte-exact prompt ending `<|im_start|>assistant\n<think>\n\n</think>\n\n`.
5. User message shape `[Styling: x] [Structure: y] [Context: z]\n{raw}`.
6. Empirically verified control-value caveats: `Structure.Lists` doesn't reliably bullet, `Context.Message`/`Notes` behave identically to `General`.
7. FP16 broken on CPU with ORT-GenAI 0.15.1 — INT4 only.
8. Reference transcript `so um i need to like send the the report by uh friday no wait make that thursday` → ~`I need to send the report by Thursday.`

**Web sample decision (Phase 5):** picked **Blazor Server** over minimal-API + static HTML for visual/DX consistency with the rest of Bruno's ecosystem, but noted minimal-API is acceptable if the agent prefers. Explicitly flagged — but did NOT bake in — the open question of whether S1Mini should get an `ElBruno.S1Mini.BlazorComponents` RCL sibling package the way `ElBruno.Whisper.BlazorComponents` / `ElBruno.Speech.BlazorComponents` do. Deferred to Phase 8 as a Morpheus decision brief candidate.

**What I discovered vs Bruno's brief:**
- Bruno's phase list said "eval script against reference transcript" — but `eval_s1_mini.py --model-dir` requires a *local* variant directory, so Phase 3 has to either (a) reproduce conversion locally with `--skip-upload` first, or (b) `hf snapshot download` the int4/ folder. Chose (a) as the smoke-test path and made this explicit.
- Bruno's brief said "note other ElBruno repos use a consistent look" for the icon. Verified `C:\src\ElBruno.LocalLLMs\images\nuget_logo.png` exists (as reference) — pointed the agent at it explicitly plus siblings `ElBruno.Whisper` / `ElBruno.QwenTTS`.
- Bruno's brief said `publish.yml` push is `--skip-duplicate` — confirmed, already correct in the workflow, no change needed.

**One additional gap I flagged that Bruno's brief did not enumerate:**
- **GitHub `release` environment protection rules.** The brief calls out creating the environment and the secret, but not the "Required reviewers" + "Restrict to `main` branch" protection rules that are best-practice for a trusted-publisher OIDC setup. Added as a recommendation in Phase 6.1 (browser step, optional but noted). Not blocking.

**Nothing else in Bruno's brief was contradicted by the real repo.** The 56/0 test count, the file listing, the one-commit-no-remote git state, and the API surface all match.

**Files created:** `C:\src\ElBruno.S1Mini\FIRST_PROMPT.md` (only). No other files touched in either repo. No commits.

**Confidence: High** on the phase structure, verification gates, and the four hard invariants that were baked into the prompt (temperature-0 guard, empty-decode crash, empty-output-is-correct, byte-exact prompt). **Medium** on the web-sample choice (Blazor Server vs minimal API is genuinely Bruno's call — noted as such). **High** on the stop-and-ask flags being placed exactly where a human decision is genuinely required.

---


## Round 4: ASR Product-Line Umbrella — REJECT `ElBruno.Transcribe`, keep `ElBruno.Whisper` (2026-08-19)

**2026-08-19T19:41-04:00:** Bruno escalated Round 3 into a strategic question: given planned Parakeet + Nemotron support, should `ElBruno.Whisper` be deprecated and replaced by `ElBruno.Transcribe`? Delivered `.squad/decisions/inbox/morpheus-transcribe-umbrella-strategy.md`.

**Verdict: S4+ — Status Quo + sibling repos on demand.** Do NOT deprecate `ElBruno.Whisper`. Do NOT create `ElBruno.Transcribe`. Do NOT build `ElBruno.Transcribe.Abstractions`. Ship `ElBruno.Whisper.Normalization` this week (Round 3 placement unchanged). When Parakeet actually runs in .NET, publish `ElBruno.Parakeet` as its own repo — matching Bruno's one-model-per-repo convention (`QwenTTS`, `VibeVoiceTTS`, `Podcast.TTS`, `Whisper`, `Realtime`, `PersonaPlex`).

**Decisive finding — the MEAI question.** `WhisperSpeechToTextClient.cs:12` already declares `: ISpeechToTextClient` from `Microsoft.Extensions.AI` 10.7.0. Provider-specific extensions (word/segment timestamps, detected language, model id, execution provider) ride on `SpeechToTextResponse.AdditionalProperties` via metadata keys `elbruno.whisper.*`. Parakeet and Nemotron have identical semantics — MEAI's shape covers them. **A custom `ElBruno.Transcribe.Abstractions` on top of `ISpeechToTextClient` is redundant. The umbrella already exists; Microsoft ships it.** This kills S1 and S3's primary architectural rationale.

**Why deprecating Whisper is wrong even at pre-1.0:**
- Package name is accurate; users searching NuGet for "whisper" find it. Rename → discoverability loss, not gain.
- Pre-1.0 makes it *technically* cheap but solves no user-facing problem; 1,989 downloads + 105 Blazor sibling + Bruno's blog/YouTube discovery paths → non-zero migration and SEO cost for zero benefit.
- Bruno's own ecosystem is **one-model-per-repo** across TTS (QwenTTS, VibeVoiceTTS, Podcast.TTS) and ASR (Whisper). No umbrella-of-models package exists. `ElBruno.Transcribe` would be the outlier that breaks his convention.
- Repo already publishes multi-package (Whisper + Whisper.BlazorComponents) so multi-package-per-repo is fine — the issue is multi-*model* consolidation, which buys nothing users care about.

**Speculative-generality trap.** Parakeet/Nemotron in .NET is not near-term commodity — NeMo→ONNX has real export friction (custom ops, RNN-T decoding, tokenizer coupling). Timeline uncertain. Designing an abstraction for one implementation while imagining the second is textbook premature. Rule of three applies: build the second provider first, in its own repo, referencing MEAI directly; let shared patterns reveal themselves; extract `ElBruno.SpeechToText.Extensions` only if real duplication emerges.

**s1-mini placement** — unchanged from Round 3. Goes to `ElBruno.Whisper.Normalization` sibling package in the Whisper repo. `TranscriptNormalizer` drops its `CreateAsync(LocalLLMsOptions?, ...)` factory; keeps `IChatClient` primary ctor; consumers compose in two lines. `KnownModels.S1Mini`, `LocalChatClient`, ORT-GenAI temp-0 fix, 16 regression tests, conversion scripts all stay in LocalLLMs.

**Sequencing communicated:** this week = ship Normalization sibling; do nothing else. Later = when Parakeet works in .NET, ship `ElBruno.Parakeet` as own repo. Umbrella reconsidered only if 3 providers exist AND painful copy-paste duplication appears.

**Steelmanned counter (S1 rebuttal):** product-line marketing narrative + last-cheap-moment framing + legitimate helper-abstractions value. Rejected because the narrative is a README section not a repo, "later" never comes if MEAI stays sufficient (and it will), and helpers can ship as their own small package without deprecating anything.

**Confidence: High** on don't-deprecate + don't-create-Transcribe + MEAI-is-sufficient. Would flip only on: (a) working Parakeet prototype with response semantics MEAI can't carry, (b) Bruno making an explicit brand/marketing call, or (c) concrete dated roadmap for 3+ providers. No source or commits touched — analysis only.

---

## Round 3: s1-mini Ecosystem Placement — MOVE to `ElBruno.Whisper` (2026-08-19)

**2026-08-19T17:36-04:00:** Bruno rejected both STAY (round 1) and MOVE-to-Speech (round 2). His words: *"so we should create a new ElBruno.Transcribe and start to create all these libraries there? or add this to ElBruno.Voice? not to Speech"* — Speech explicitly off the table. Delivered `.squad/decisions/inbox/morpheus-s1-mini-ecosystem-placement.md`.

**Verdict: MOVE to `ElBruno.Whisper` as new sibling package `ElBruno.Whisper.Normalization`.** New repo (`ElBruno.Transcribe`) rejected as premature for a single 5-file feature; would only justify itself as an umbrella for 3+ transcript-adjacent post-processing libraries (diarization, redaction, summarization, translation). `ElBruno.Voice` doesn't exist — flagged as open question.

**Key evidence from `C:\src\ElBruno.Whisper\`:**

- `src/ElBruno.Whisper/ElBruno.Whisper.csproj` already refs `Microsoft.Extensions.AI.Abstractions 10.7.0` — the ONLY dep `TranscriptNormalizer` needs. Zero new coupling. The ORT-GenAI vs plain-ONNX Runtime objection (Silero analogy weakness) dissolves because normalizer takes `IChatClient` — caller brings the runtime.
- Conventions **match LocalLLMs exactly**: `.slnx` (not `.sln`), `src/tests/`, `src/samples/`, root `Directory.Build.props`, OIDC publishing (`docs/publishing.md`). Migration is essentially file move + namespace rename. Materially cheaper than the Speech migration would have been.
- Sibling-package precedent: `ElBruno.Whisper.BlazorComponents` (verified in `README.md` Packages table). `ElBruno.Whisper.Normalization` is a natural third sibling.
- Grep of Whisper docs for `normali[sz]|punctuat|post.process|clean.?up|LLM|IChatClient|future|roadmap`: every hit refers to *audio* normalization (16 kHz mono / PCM). Zero mentions of transcript text cleanup. Real capability gap, not overlap.
- Constraint alignment: Whisper is English-optimized (`.en` variants) and s1-mini v1 is English-only. Docs stories match without asterisks.
- Adjacency: A Whisper user calls `TranscribeAsync(...)`, gets messy text, needs cleanup — that's *precisely* the moment `TranscriptNormalizer` fires. Tighter than Speech's pipeline-orchestrator framing.

**Recommended package boundary:** `ElBruno.Whisper.Normalization` (sibling), **not** fold into `ElBruno.Whisper` core. Core must stay pure STT (`ISpeechToTextClient` semantics); adding chat concepts to it pollutes the "just transcribe audio" mental model.

**What stays in LocalLLMs (invariant across all options):** `KnownModels.S1Mini`/`S1MiniFp16`, `LocalChatClient`, ORT-GenAI temperature-0 native-crash fix in `OnnxGenAIModel.cs`/`OnnxVisionModel.cs`, its 16 regression tests, `scripts/convert_s1_mini.py`/`eval_s1_mini.py`.

**On `ElBruno.Transcribe`:** told Bruno directly — no, not for one library. 7 speech/audio repos already exist. New repo overhead (CI, publishing, README, NuGet listing, `.squad/`, versioning) is only justified with a family of 3+ transcript-adjacent siblings launched together. If diarization/redaction/summarization are on the roadmap, reconsider — but ship s1-mini in Whisper first.

**On `ElBruno.Voice`:** verified doesn't exist on GitHub or `c:\src\`. Best guesses ranked: (1) mental umbrella for speech family, (2) `ElBruno.Realtime` mis-labeled, (3) planned future consolidation. Flagged as an open question for Bruno rather than guessing.

**Confidence: High** on Whisper vs the other options — evidence stack is unambiguous. Would flip to `ElBruno.Transcribe` only if Bruno confirms 2–3 more transcript post-processing libraries planned. No source edits or commits made in any repo — analysis only.

---

## REVERSAL: s1-mini Repo-Boundary Re-Decision — MOVE (2026-08-19)

**2026-08-19T17:23-04:00:** Bruno pushed back twice on my STAY brief, quoting the s1-mini model card ("not a chat model … does one job … for speech-to-text output"). Coordinator asked me to re-run the analysis with **domain cohesion as PRIMARY**, and explicitly authorized reversal. I reversed.

**Revised verdict: MOVE.** Brief written to `.squad/decisions/inbox/morpheus-s1-mini-boundary-revised.md`.

**What broke my prior STAY position:**

1. **`ElBruno.Speech.Vad.Silero` is decisive precedent.** Verified at `C:\src\ElBruno.Speech\src\ElBruno.Speech.Vad.Silero\`: a published NuGet package (`1.1.0.nupkg` on disk) containing `public sealed class SileroVadClient : IVadClient` + `ModelDownloader.cs` + `SileroVadClientFactory.cs` + ONNX runtime deps. My earlier claim "Speech's architecture is provider-agnostic and does not host chat-model implementations" was flat wrong. Speech happily hosts **domain-specific ONNX-model concrete implementations as sibling packages.** The s1-mini/`TranscriptNormalizer` parallel to Silero VAD is very tight.
2. **`TextSegmenter.cs` is a real analog.** `C:\src\ElBruno.Speech\src\ElBruno.Speech.Pipeline\TextSegmenter.cs` is `internal static`, `string → IEnumerable<string>`, pure text processing living inside Speech.Pipeline (LLM→TTS chunking). Weaker than a public normalizer, but confirms non-audio text processing is a legitimate Speech.Pipeline resident when it serves the pipeline's domain.
3. **The entire feature is uncommitted.** `git status` on `src/ElBruno.LocalLLMs/Normalization/`, `src/tests/…/Normalization/`, `src/samples/TranscriptNormalizer/` → all `??`. My prior "~1 day + NuGet release cycle" cost estimate was over-stated: no release cycle needed, this is a git rm on the removal side.
4. **The API is already provider-neutral.** `TranscriptNormalizer(IChatClient)` primary ctor takes any `IChatClient`. Only the `CreateAsync(LocalLLMsOptions?, ...)` static factory (one method, `TranscriptNormalizer.cs:59-74`) is LocalLLMs-coupled. Removing it is trivial; LocalLLMs users compose in two lines.

**What survived from STAY:**
- PRD sentence "LocalLLMs provides local IChatClient implementations" (`docs/PRD.md:60-63`) — but I had read it as a boundary rule; it's a provider-catalog note. Under MOVE, LocalLLMs continues to provide the `IChatClient` for s1-mini; only the wrapper class relocates.
- No dependency edge Speech→LocalLLMs. MOVE preserves this (Speech.Normalization needs only `Microsoft.Extensions.AI.Abstractions`).
- Absent post-STT hook in `DefaultSpeechPipeline.RunSttStageAsync` (`DefaultSpeechPipeline.cs:207-248`) — real, but describes cost of a follow-up formalization, not wrongness of fit.

**Honest weakness in MOVE:** PRD is silent on transcript normalization. § 12.4 (STT) does not mention it; § 12.6's "text normalizer must be replaceable" (`PRD.md:778`) refers to LLM-output→TTS segmentation, not ASR post-processing. Silence is not exclusion, especially for a capability the repo owner is discovering, but I flag it plainly in the brief.

**SPLIT documented as secondary** — `ITranscriptPostProcessor` in Speech.Abstractions + optional Pipeline stage, impl stays in LocalLLMs. Valid if Bruno wants the pipeline seam formalized *and* the ergonomic `CreateAsync(LocalLLMsOptions)` factory kept. My ranking prefers MOVE because Vad.Silero argues for *impls in Speech*, not for interfaces-only.

**Confidence: High** on reversal direction. **Medium** on MOVE vs SPLIT within the "reverse STAY" camp — Bruno's product preference decides. What stays in LocalLLMs under all options: `KnownModels.S1Mini`, `LocalChatClient`, the ORT-GenAI temperature-0 native-crash fix, and its 16 regression tests. No source or commits touched in either repo — analysis only.

---

## Follow-up: Discoverability Cross-Link Added (2026-08-19)

**2026-08-19T17:20:02-04:00:** Closed the one weakness flagged in the STAY brief (`morpheus-s1-mini-repo-boundary.md`) — discoverability. Added a short cross-link in `C:\src\ElBruno.Speech\README.md`:
- A callout right after the pipeline ASCII diagram (STT → LLM boundary), pointing to `TranscriptNormalizer` in `ElBruno.LocalLLMs` with a one-line before/after example, framed explicitly as optional/separate — not a Speech dependency, no pipeline hook implied.
- A one-clause addition to the existing "Related Repositories" bullet for `ElBruno.LocalLLMs`.
- Did not touch Speech's "What's New" table, csproj/props, or any other file. No commits made in either repo.

## Latest: s1-mini Repo-Boundary Decision (2026-08-19)

**2026-08-19T17:05:35-04:00:** Bruno asked whether the `superwhisper/s1-mini` `TranscriptNormalizer` (uncommitted in `ElBruno.LocalLLMs`) belongs in `ElBruno.Speech` instead. Delivered decision brief `.squad/decisions/inbox/morpheus-s1-mini-repo-boundary.md`.

**Verdict: STAY in `ElBruno.LocalLLMs`.**

Key evidence gathered from `C:\src\ElBruno.Speech`:
- `docs/PRD.md:57-63` and `README.md:151-155` explicitly list LocalLLMs as an **external `IChatClient` provider** — Speech's architecture is provider-agnostic and does not host chat-model implementations.
- `Directory.Packages.props` — zero reference to `ElBruno.LocalLLMs`; grep confirms no `<PackageReference>` or `<ProjectReference>` anywhere in `src/`.
- `DefaultSpeechPipeline.cs` — STT stage writes transcripts directly to `_llm.GetResponseAsync(...)`. **No post-STT text-transform hook exists.** Adding one is a Speech design change, not a code-move.
- `TranscriptNormalizer` is `string → string` via `IChatClient` — never touches audio/VAD/TTS. It is a chat-client wrapper, which is LocalLLMs' pattern.

Future path preserved: if Bruno adds `ITranscriptPostProcessor` to `ElBruno.Speech.Abstractions`, the s1-mini implementation stays here and implements the interface. Additive, not a migration. STAY does not close that door.

Convention friction noted (in case Bruno overrides): Speech uses `.sln` (not `.slnx`), central package management, and root-level `tests/`. Estimated MOVE cost: ~1 day + NuGet release cycle.

---

## Previous: Phase 3A — magentic-ui .NET Port — Implementation Complete (2026-07-23)

**2026-08-19 (follow-up note):** Tank's end-to-end verification (Decision 37) confirms Trinity's C# `TranscriptNormalizer` implementation is production-ready — the library path matches Dozer's validated Python baseline exactly (byte-for-byte prompt parity, all 6 reference outputs match). Your documentation and API design decisions are validated by real-world execution against the published model.

**2026-07-23T16:38:** Phase 3A complete. Switch delivered scaffold (0 errors), Trinity delivered 15-file `MagenticUIServer.Agents` library (0 errors, 2 turns), Tank delivered 40 tests (all passing). Decision 35 merged to `.squad/decisions.md`. Session logged to `.squad/log/2026-07-23T16-38-30-magentic-ui-phase3a.md`.

**Phase 3A delivery:**
- `MagenticUIServer` — ASP.NET Core 8.0 host with SignalR hub, SPA, CORS
- `MagenticUIServer.Agents` — 4 models, 4 tools, 4 agents, 1 orchestrator (MEAI OmniAgent loop)
- `MagenticUIServer.Agents.Tests` — 40 tests, all passing
- React 19 minimal ClientApp with `@microsoft/signalr` and placeholder components

**Pending Phase 3B:** UserProxy full HIL wiring; WSL2 coder; BrowserSurferAgent; SQLite persistence.  
**Pending Phase 3C:** Full magentic-ui React fork; QEMU sandbox; Auth.

---

## Previous: Phase 3 — magentic-ui .NET Port Architecture (2026-07-23, amended A1)

**2026-07-23:** Completed architecture decision record for Phase 3: .NET port of microsoft/magentic-ui. Delivered `.squad/decisions/inbox/morpheus-phase3-architecture.md` covering 10 numbered decisions across 3 new projects.

**Amendment A1 (2026-07-23) — Dozer (ML Engineer) corrections:**  
Three breaking corrections applied before finalisation:
1. **SK MagenticOrchestrator dropped.** `Microsoft.SemanticKernel.Agents.Magentic` v1.78.0-preview requires `IChatCompletionService` (SK-native), not `IChatClient` (MEAI). No confirmed bridge in v1.78. Decision: use proven MEAI OmniAgent loop from `MagenticBrainAgent` — `AIFunctionFactory.Create`, `FunctionCallContent`, `FunctionResultContent`. No SK packages in Phase 3A.  
2. **Hub protocol corrected to full Python taxonomy.** Frontend sends 8 message types (start, stop, ping, input_response, approval_response, continuation_response, pause, resume). Backend emits 15 frame types via `metadata.type` discriminator (text, tool_call, tool_result, input_request, approval_request, pause_notification, resume_notification, system, browser_screenshot, browser_action, file_event, orchestrator_plan, token_stream, final_answer, error). Human-in-the-loop maps to `TaskCompletionSource<string>`.  
3. **MarkItDotNet API confirmed.** `ElBruno.MarkItDotNet` v0.9.1 stable. `services.AddMarkItDotNet()`, `await converter.ConvertAsync(path)`, `await MarkdownService.ConvertUrlAsync(url)`. Pinned to v0.9.1.

**Key Decisions (post-amendment):**

1. **Stay in current solution** — `src/samples/MagenticUIServer/` inside `ElBruno.LocalLLMs.slnx`. 3 new csproj.
2. **No SK — pure MEAI orchestration.** `MagenticUIOrchestrator` is a custom coordinator on `IChatClient`. `AIFunctionFactory.Create()` for tools. Participants as `AgentParticipant` records.
3. **React 19 fork + @microsoft/signalr, not Blazor.** `AgentHub` with 8 client methods + `frame` event emitting all 15 frame types.
4. **Phase 3A topology:** MagenticBrain/Qwen3 orchestrator + FileSurfer + WebFetcher + UserProxy + CoderStub. QEMU deferred to Phase 3C; WSL2 in Phase 3B.
5. **New risks added:** UserProxy TCS deadlock on disconnect (mitigated via CancellationToken), orchestrator participant selection unreliability (mitigated via `SelectParticipant` sentinel tool).

**Status:** ADR written and amended. Gates Switch (scaffold), Trinity (implementation), Tank (tests).

---


## Previous Work Summary

Delivered across 20+ sessions since 2026-03-17:
- Magentic-ui Phase 3A (2026-07-23): 3-project ASP.NET Core orchestration, 40 tests passing
- VLM support (Fara1.5-9B, bitnet, Qwen3, Phi-4, GPT-OSS-20B)
- Conversion pipelines for ONNX, quantization strategies (INT4/FP16)
- Test coverage, CI/CD workflows, documentation standards
- DI patterns, chat template formats, model registry architecture

See decision archive for full records.
