# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Architecture Status & RAG Plan

**2026-03-17:** Morpheus completed full solution architecture. Blueprint in `docs/architecture.md`. 9 decisions merged to `.squad/decisions.md`. Build infra (csproj, Directory.Build.props) and CI/CD scripting depend on architecture choices (net8.0, net10.0, ONNX GenAI). Switch can now proceed with solution scaffolding.

**2026-03-27:** RAG tool routing plan approved (`docs/plan-rag-tool-routing.md`). New deliverables: benchmark project (Phase 1, Tank owner) and sample project (Phase 2, Trinity owner). Switch should update `.slnx` to include these projects and configure CI/CD for benchmark runs if needed.

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **CI simplified to net8.0-only on ubuntu-latest** — Removed multi-OS matrix and net10.0 SDK install per copilot-instructions.md convention. CI runners only need `8.0.x` with `-p:TargetFrameworks=net8.0` for restore/build and `--framework net8.0` for test. Test path updated from per-project to solution-level.
- **gitignore: glob `cache_dir/` replaces 80+ individual lock file entries** — Model download caches should never be committed. One glob line replaces 140+ lines of individual file paths.
- **.NET 10.0.200 SDK available**— supports slnx format natively. No need for .sln migration.
- **Microsoft.Extensions.AI.Abstractions 10.4.0 breaking changes:** IChatClient interface renamed methods: `CompleteAsync` → `GetResponseAsync`, `CompleteStreamingAsync` → `GetStreamingResponseAsync`. Return types changed: `ChatCompletion` → `ChatResponse`, `StreamingChatCompletionUpdate` → `ChatResponseUpdate`. `ChatClientMetadata` constructor parameter renamed `modelId` → `defaultModelId`. `ChatResponseUpdate.Text` is now read-only (use constructor).
- **OnnxRuntimeGenAI 0.8.3 API change:** `Generator.GetNextTokens()` removed. Use `generator.GetSequence(0)[^1]` to get the latest token after `GenerateNextToken()`.
- **ElBruno.HuggingFace.Downloader latest is 0.6.0** (not 0.5.0 as architecture doc stated).
- **xUnit 2.9.0 / NSubstitute 5.3.0** are latest stable (not 2.8.2 / 5.1.1). TreatWarningsAsErrors means NuGet version resolution warnings become hard errors — pin exact versions.
- **Record types with `string[]` properties** don't get value equality — arrays use reference equality. Tests must compare properties individually or share array instances.
- **Multi-target `net8.0;net10.0` works** with the current package set. All dependencies resolve for both TFMs.
- **GitHub Actions workflows scaffolded from Node.js templates** — All Squad workflows (squad-release, squad-ci, squad-promote, etc.) were Node.js templates applied to C# project. Required full rewrite: `setup-node` → `setup-dotnet`, `node --test` → `dotnet test`, version extraction from `package.json` → `.csproj`. Version extraction pattern: `grep -oP '(?<=<Version>).*(?=</Version>)' src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj`.
- **Worktree-local strategy allows `.squad/` on main** — Unlike standard Squad setup (where `.squad/` stays on dev), this project commits `.squad/` and `.ai-team/` to main by design. `squad-main-guard.yml` updated to allow these paths.
- **NuGet publishing requires secret check** — `publish.yml` checks if `NUGET_API_KEY` secret is set before attempting push. If not set, workflow still succeeds and uploads artifact (enables testing publish workflow without NuGet credentials).
- **CHANGELOG validation expects Keep a Changelog format** — All release workflows validate CHANGELOG.md has `## [X.Y.Z]` entry matching version in `.csproj`. Must update both files before release.
- **NuGet OIDC Trusted Publishing replaces API keys** — `publish.yml` now uses `NuGet/login@v1` for OIDC token exchange instead of `NUGET_API_KEY`. Requires: (1) NuGet.org Trusted Publishing policy matching repo/workflow/environment, (2) GitHub `release` environment, (3) `NUGET_USER` secret (profile name). Trigger changed from `push: tags` to `release: published` + `workflow_dispatch`.
- **Release chain: main → squad-release → publish** — Push to main triggers `squad-release.yml` which creates a GitHub Release via `gh release create`. The `release: published` event then triggers `publish.yml`. No manual tag creation needed.
- **GITHUB_TOKEN suppresses most triggered events** — GitHub Actions prevents recursive loops by not creating new workflow runs for events triggered by `GITHUB_TOKEN`, with two exceptions: `workflow_dispatch` and `repository_dispatch`. This means `release: published` won't fire when `gh release create` uses `GITHUB_TOKEN`. Fix: explicitly trigger publish via `gh workflow run publish.yml` (workflow_dispatch), which IS allowed. Requires `actions: write` permission.


## 2026-03-27: Convention Enforcement Session

**Cross-Agent Update:** Trinity and Morpheus completed their parts:
- **Trinity (Core Dev):** Project structure restructured (tests/ → src/tests/, samples/ → src/samples/), all csproj updated, Directory.Build.props centralized, global.json created
- **Morpheus (Docs):** README.md updated with new paths and building instructions

**Your Orchestration Log:** `.squad/orchestration-log/2026-03-27T1711-switch.md`

**Decision Merged:** Decision 7 (CI/CD net8.0-only) in `.squad/decisions.md`

All conventions from `.github/copilot-instructions.md` now fully applied across codebase, CI/CD, and docs.

## 2026-03-27: Phase 4a/4b Completion

**Cross-Agent Update:** Trinity and Morpheus completed Phases 4a (tool calling) and 4b (RAG pipeline):

- **Trinity (Core Dev):** 
  - Phase 4a: 6 formatters enhanced with tool support (Qwen, Llama3, Phi3, DeepSeek, Mistral, Gemma)
  - Phase 4b: New `ElBruno.LocalLLMs.Rag` NuGet package with pluggable embeddings, dual storage backends, 25 tests, RagChatbot sample
  - Total: 34 files created, +3,677 lines, 13 projects in solution, 0 build errors, 384 tests passing

- **Morpheus (Docs):** 
  - Created `docs/tool-calling-guide.md` (744 lines) and `docs/rag-guide.md` (853 lines)
  - Updated `docs/CHANGELOG.md` with Phase 4a/4b entries
  - Total: 2 guides, ~1,600 lines added

**Your Updates:** `.slnx` already includes all Phase 4 projects (ElBruno.LocalLLMs.Rag, tests, samples). No additional CI/CD benchmark configuration needed yet (Tank will own Phase 1 benchmark framework next).

**Decisions Merged:** Decision 33 (Phase 4b RAG architecture) with 8 sub-decisions in `.squad/decisions.md`

**Commit:** 8344eb6 pushed to origin/main

## 2026-07-25: Publish Workflow Fix (PR #14)

**Problem:** `publish.yml` restored/built the entire `.slnx` solution with `-p:TargetFrameworks=net8.0`, which broke on `ZeroCloudRag` sample (net10.0-only, depends on `ElBruno.LocalEmbeddings 1.0.1` which is net10.0-only). Also only installed .NET 8.0 SDK and forced single-TFM pack, so packages shipped without net10.0.

**Fix applied to `.github/workflows/publish.yml`:**
1. Setup .NET now installs both `8.0.x` and `10.0.x` SDKs
2. Restore/Build target specific projects (libraries + tests) instead of the whole solution
3. Tests now run both `ElBruno.LocalLLMs.Tests` and `ElBruno.LocalLLMs.Rag.Tests`
4. Pack step removed `-p:TargetFrameworks=net8.0` so packages include both TFMs
5. Both `ElBruno.LocalLLMs` and `ElBruno.LocalLLMs.Rag` are now packed and pushed

**Branch:** `fix/publish-workflow-net10-compat` → PR #14 to main

**Cross-Agent Update (Coordinator):** Added missing `nuget_logo.png` asset reference to `ElBruno.LocalLLMs.Rag.csproj` for NuGet packaging requirement. Both packages now published successfully to NuGet.org with full multi-target support.

**Outcome:** ✓ Both packages published to NuGet.org with net8.0 + net10.0 TFMs. Solution can now include net10.0-only samples without breaking CI/CD.

## 2026-07-25: Gemma 4 Blocker Monitoring Workflow

**File:** `.github/workflows/monitor-gemma4-blocker.yml`

**What it does:** Daily automated check (9 AM UTC + manual trigger) for signals that `onnxruntime-genai` may now support Gemma 4 architecture. Three parallel-then-merge jobs:

1. **check-release** — Fetches latest NuGet version, compares to known blocked `0.12.2`, searches release notes for Gemma/PLE/head_dim keywords
2. **check-issue** — Checks `microsoft/onnxruntime-genai#2062` status and recent maintainer comments via `actions/github-script@v7`
3. **evaluate** — Combines scores: +20 new version, +40 keyword hits, +30 issue closed. Score ≥50 creates/updates a GitHub issue with `gemma4` + `investigation` labels. Dedup prevents duplicate issues.

**Key decisions:**
- Shell scripts for NuGet API (simple curl/jq), `actions/github-script@v7` for all GitHub API interactions
- Minimal permissions: `contents: read`, `issues: write`
- Config via env vars (`KNOWN_BLOCKED_VERSION`, `UPSTREAM_REPO`, `UPSTREAM_ISSUE`) for easy updates
- Auto-creates labels if they don't exist (idempotent)
- Dedup: checks for existing open issue with `gemma4` label before creating; adds comment if found

## 2026-04-07: Gemma 4 Blocker Monitoring Workflow

**Session:** Gemma4-Monitor-Impl (orchestration)

Created .github/workflows/monitor-gemma4-blocker.yml — daily NuGet/GitHub monitoring for Gemma 4 support signals in onnxruntime-genai.

**Implementation:**
- 3 parallel jobs: check-release, check-issue, evaluate
- Confidence scoring (max 90): version +20, keyword +40, issue closed +30
- Auto-create GitHub issue when score ≥ 50
- Config via env vars (KNOWN_BLOCKED_VERSION, UPSTREAM_REPO, UPSTREAM_ISSUE)
- Minimal permissions (contents: read, issues: write)

**Commit:** 5adb604 (initial)

**Cross-Agent Notes:**
- Tank (QA) reviewed & found 3 security bugs (1 critical, 2 medium)
- All bugs fixed via env var indirection + error handling
- Coordinator bumped KNOWN_BLOCKED_VERSION from 0.12.2 to 0.13.0
- Final commit: e85743f (security fixes + version bump)

## BitNet Native NuGet Packages

**Date:** 2026-07-25

Created platform-specific native NuGet packages for shipping pre-built bitnet.cpp binaries:

**Files created:**
- `src/ElBruno.LocalLLMs.BitNet.Native.win-x64/` — content-only .csproj, ships `llama.dll`
- `src/ElBruno.LocalLLMs.BitNet.Native.linux-x64/` — content-only .csproj, ships `libllama.so`
- `src/ElBruno.LocalLLMs.BitNet.Native.osx-arm64/` — content-only .csproj, ships `libllama.dylib`
- `.github/workflows/build-bitnet-native.yml` — reusable workflow, 3-platform matrix build of bitnet.cpp
- `.github/workflows/publish-bitnet-native.yml` — calls build workflow, packs, publishes via OIDC trusted publishing

**Key decisions:**
- `netstandard2.0` target with `NoBuild=true` — these are content-only packages, no compilation
- `NU5128` warning suppressed — expected for native-only packages with no lib/ assemblies
- Build workflow uses `workflow_call` (reusable) + `workflow_dispatch` (manual testing)
- Publish workflow triggers on `release: published` (tag `native-v*`) + `workflow_dispatch`
- Version extraction strips both `native-v` and `v` prefixes from release tags
- Binary search uses recursive find in `bitnet-src/build` because exact path varies by platform/build config
- `BITNET_CPP_COMMIT` env var set to `main` — should be pinned to SHA after first successful build
- Solution file updated with `/src/native/` folder grouping
- Follows existing per-csproj CI pattern (not solution-wide), matching `ci.yml` and `publish.yml`
