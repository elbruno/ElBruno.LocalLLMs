# Squad Decisions

## Active Decisions

### Decision 1: Single Core Package (Not Per-Model)

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** With 20+ target models, we could create `ElBruno.LocalLLMs.Phi`, `ElBruno.LocalLLMs.Llama`, etc.

**Decision:** Single `ElBruno.LocalLLMs` NuGet package. Models are data (`ModelDefinition` records), not separate assemblies.

**Rationale:**
- ONNX Runtime GenAI is model-agnostic — same API for all models
- Model-specific knowledge is config (HuggingFace ID, ONNX paths, chat template enum)
- 20+ NuGet packages = unmaintainable, confusing for users
- Both reference repos use single core packages with options-based model selection

**Consequences:** Extension packages by *concern* (e.g., `.SemanticKernel`) are still possible. Just not by model.

---

### Decision 2: IChatClient as Primary Interface

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** We could define our own chat interface or implement multiple MEAI interfaces.

**Decision:** Implement `IChatClient` from `Microsoft.Extensions.AI.Abstractions`. This is the only public interface contract.

**Rationale:** Non-negotiable. The entire library exists to plug into the MEAI ecosystem. Users should be able to swap `LocalChatClient` for any other `IChatClient` provider.

---

### Decision 3: ONNX Runtime GenAI (Not Raw ONNX Runtime)

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** We could use `Microsoft.ML.OnnxRuntime` directly (like LocalEmbeddings does) or use `Microsoft.ML.OnnxRuntimeGenAI`.

**Decision:** Use `Microsoft.ML.OnnxRuntimeGenAI` for LLM inference.

**Rationale:** GenAI handles tokenization, KV cache management, beam search / sampling, and streaming — all things we'd have to build from scratch with raw ONNX Runtime. Embeddings are simpler (single forward pass), which is why LocalEmbeddings uses raw ORT.

---

### Decision 4: Sync Constructor + Async Factory Pattern

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** How should users create `LocalChatClient`?

**Decision:** Both `new LocalChatClient(options)` (sync) and `LocalChatClient.CreateAsync(options)` (async factory).

**Rationale:** Proven pattern from `LocalEmbeddingGenerator`. Sync constructor for tools/tests/console apps. Async factory for ASP.NET Core / hosted services to avoid sync-over-async.

---

### Decision 5: ModelDefinition as Immutable Record

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** How to represent model-specific configuration.

**Decision:** `sealed record ModelDefinition` with required init properties. `KnownModels` static class holds pre-defined instances.

**Rationale:** Models are data, not behavior. Adding a new model = adding a record instance. No inheritance, no virtual methods. Clean and testable.

---

### Decision 6: Chat Templates as Internal Strategy

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** Each model family uses a different prompt format (ChatML, Llama3, Phi3, etc.).

**Decision:** Internal `IChatTemplateFormatter` interface with per-format implementations. Users select a model; the template resolves automatically from `ChatTemplateFormat`.

**Rationale:** Users should never see chat templates. They send `IList<ChatMessage>` through `IChatClient`. The formatting is an implementation detail.

---

### Decision 7: Multi-Target net8.0 + net10.0

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** Which .NET versions to support.

**Decision:** `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>` matching both reference repos.

**Rationale:** .NET 8 is current LTS. .NET 10 is current. Same pattern used by LocalEmbeddings and QwenTTS.

---

### Decision 8: Directory.Build.props for Shared Settings

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** How to manage shared build properties across all projects.

**Decision:** Root `Directory.Build.props` with `LangVersion`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`.

**Rationale:** Exact same pattern as `elbruno.localembeddings`. Ensures consistency across all projects.

---

### Decision 9: Phase 1 MVP = Phi-3.5 Only

**Date:** 2026-03-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** Which models to support first.

**Decision:** Phase 1 targets Phi-3.5-mini-instruct only (native ONNX, no conversion needed).

**Rationale:** Smallest friction to a working library. Native ONNX = no Python conversion pipeline needed. Phi-3.5 is well-tested, Microsoft-published. Proves the architecture before adding complexity.

---

### Decision 10: Package Versions Updated from Architecture Doc

**Date:** 2026-03-17  
**Author:** Switch (DevOps)  
**Status:** Active

**Context:** The architecture doc specified specific package versions that were outdated by the time of implementation. NuGet packages had newer stable releases.

**Decision:** Updated all package versions to latest stable:

| Package | Architecture Doc | Actual Used |
|---------|-----------------|-------------|
| Microsoft.Extensions.AI.Abstractions | 10.3.0 | 10.4.0 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.3 | 10.0.5 |
| Microsoft.ML.OnnxRuntimeGenAI | 0.6.* | 0.8.3 |
| ElBruno.HuggingFace.Downloader | 0.5.0 | 0.6.0 |
| xunit | 2.* | 2.9.0 |
| NSubstitute | 5.* | 5.3.0 |

**Consequences:** MEAI 10.4.0 introduced breaking API changes (method renames, type renames). All source code updated accordingly. OnnxRuntimeGenAI 0.8.3 changed the token retrieval API. Generator wrapper updated. Pinned exact versions to avoid NuGet resolution warnings with TreatWarningsAsErrors.

**Impact:** All projects in the solution. Any future code must use the new API names (`GetResponseAsync`, `ChatResponse`, etc.).

---

### Decision 11: NSubstitute over Moq for Mocking

**Date:** 2026-03-17  
**Author:** Tank (Tester/QA)  
**Status:** Active

**Context:** Architecture doc Section 9 says "xUnit + Moq (or NSubstitute)".

**Decision:** Use NSubstitute. Already in the .csproj from Switch's scaffolding.

**Rationale:** NSubstitute has cleaner syntax, no `Setup()`/`Verify()` ceremony. Fits the project's "minimal noise" philosophy. The internal `LocalChatClient(options, downloader)` constructor enables clean injection of mocked `IModelDownloader`.

---

### Decision 12: Exact String Tests for Chat Templates

**Date:** 2026-03-17  
**Author:** Tank (Tester/QA)  
**Status:** Active

**Context:** Chat template output is the most critical correctness surface — wrong tokens = broken model inference.

**Decision:** All five template formatters (ChatML, Phi3, Llama3, Qwen, Mistral) have tests asserting exact output strings for system+user, user-only, multi-turn, and edge cases. Additionally, negative assertions verify no cross-contamination of template tokens.

**Rationale:** Fuzzy "contains" assertions miss subtle bugs (wrong newline counts, missing end tokens). Exact string equality catches regressions immediately.

---

### Decision 13: Integration Tests Gated by Environment Variable

**Date:** 2026-03-17  
**Author:** Tank (Tester/QA)  
**Status:** Active

**Context:** Integration tests require real model downloads (GB-scale) and GPU/CPU inference time.

**Decision:** All integration tests use `[Trait("Category", "Integration")]` AND check `RUN_INTEGRATION_TESTS=true` env var at runtime. Tests use `[SkippableFact]` with `Skip.If()` for graceful skipping when disabled.

**Rationale:** CI unit test jobs should never accidentally trigger multi-GB downloads. Dual gating (xUnit trait + runtime env check) provides defense in depth. xUnit's `SkippableFact` ensures tests skip cleanly without errors.

---

### Decision 14: MEAI v10 API Names as Canonical

**Date:** 2026-03-17  
**Author:** Tank (Tester/QA)  
**Status:** Active

**Context:** MEAI v10.4.0 renamed the core IChatClient methods: `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`, etc.

**Decision:** All tests and samples use the v10 API names. Architecture doc examples (which used old names) are treated as aspirational, not literal.

**Rationale:** Tests must compile and pass against the actual code. The source was updated to v10 API by Trinity. This is now the canonical API surface for the library.

---

### Decision 15: Lazy Model Initialization with SemaphoreSlim

**Date:** 2026-03-17  
**Author:** Trinity (Core Dev)  
**Status:** Active

**Context:** Model download + ONNX load is expensive. Should it happen in constructor or on first use?

**Decision:** Lazy initialization on first `GetResponseAsync`/`GetResponseAsyncStreaming` call, with `SemaphoreSlim` for thread safety. Async factory `CreateAsync()` eagerly initializes.

**Rationale:** Constructor stays sync and fast. Lazy init means DI registration doesn't block startup. SemaphoreSlim ensures only one thread does the download/load even under concurrent access.

---

### Decision 16: TokenizerStream for Streaming

**Date:** 2026-03-17  
**Author:** Trinity (Core Dev)  
**Status:** Active

**Context:** For streaming, we can either accumulate tokens and decode the full list each time, or use `TokenizerStream` for incremental decoding.

**Decision:** Use `Tokenizer.CreateStream()` + `TokenizerStream.Decode(tokenId)` for per-token incremental decoding.

**Rationale:** More efficient — avoids re-decoding the full sequence on every token. The GenAI API provides this specifically for streaming use cases.

---

### Decision 17: Config-based Provider Selection

**Date:** 2026-03-17  
**Author:** Trinity (Core Dev)  
**Status:** Active

**Context:** `new Model(path)` defaults to CPU. GPU needs provider configuration.

**Decision:** For CPU, use `new Model(path)` directly. For CUDA/DirectML, use `Config` class to configure providers before model creation.

**Rationale:** Cleanest API path — CPU is the common case and stays simple. GPU configuration is explicit and extensible.

---

### Decision 18: Comprehensive Documentation Suite

**Date:** 2026-03-18  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active

**Context:** With MVP feature-complete (23 models, 7 formatters, 246 tests), users and contributors need clear pathways for onboarding, model selection, contributing, and release transparency.

**Decision:** Create and maintain a **4-part documentation suite**:

1. **docs/getting-started.md** — User-focused onboarding (prerequisites, 5-minute quick start, decision tree, full examples, troubleshooting)
2. **docs/supported-models.md** — Complete model reference (23-model table, tier explanations, benchmarks, decision tree)
3. **CONTRIBUTING.md** — Developer onboarding (build quick start, project structure, step-by-step model addition, code style, CI/CD overview)
4. **CHANGELOG.md** — Release history (v0.1.0 features, dependencies, known limitations, roadmap, Keep a Changelog format)

**Rationale:**

- **Separation of concerns:** Getting Started (how?), Supported Models (which?), Contributing (how to extend?), Changelog (what's new?)
- **Cross-linking:** All docs link to each other, forming a cohesive knowledge base
- **Fidelity:** All examples from actual sample code; all API names from MEAI 10.4.0 canonical API
- **Pain point coverage:** Getting Started includes troubleshooting (OOM, slow first run); Supported Models includes decision tree; Contributing includes step-by-step model addition; Changelog provides transparency
- **Maintenance clarity:** Clear ownership (Morpheus: docs, Tank: tests/benchmarks, Trinity: code samples, Switch: CI/CD)

**Consequences:**

- ✅ New users can ship their first LLM app in <1 hour
- ✅ Experienced users can compare 23 models on a single page
- ✅ Contributors have clear onboarding path (build → test → PR checklist)
- ✅ Release transparency builds trust with early adopters
- 📝 Maintenance burden: CONTRIBUTING.md must stay in sync with project structure; Supported Models table must be updated on model additions; CHANGELOG.md on every release

---

### Decision 19: Rewrite Squad Workflows for C# Project

**Date:** 2026-03-18  
**Author:** Switch (DevOps/Packaging)  
**Status:** Implemented

**Context:** Eight GitHub Actions workflows were scaffolded from a Node.js Squad template but applied to `ElBruno.LocalLLMs`, which is a C# .NET 8/10 project. All workflows were failing because they referenced:
- `actions/setup-node@v4`
- `node --test test/*.test.js`
- `node -e "console.log(require('./package.json').version)"`
- Non-existent `docs/build.js` script

The project structure is:
- Solution: `ElBruno.LocalLLMs.slnx`
- Main project: `src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj`
- Tests: `tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj`
- Version: `<Version>0.1.0</Version>` in `.csproj`
- CHANGELOG format: `## [0.1.0] - 2026-03-18`

**Decision:** Rewrote all 8 workflows to be C#-native:

1. **squad-release.yml** — .NET setup (8.0.x + 10.0.x), `dotnet restore/build/test`, version extraction via `grep` from `.csproj`
2. **squad-main-guard.yml** — Allowed `.squad/` and `.ai-team/` on main (worktree-local strategy); blocked templates and proposal dirs
3. **publish.yml** — Conditional NuGet push (checks `NUGET_API_KEY` secret), always uploads artifact
4. **squad-ci.yml** — .NET restore/build/test for PR/dev branches
5. **squad-preview.yml** — .NET tests, version extraction, CHANGELOG validation; validates `.squad/` not tracked on preview
6. **squad-promote.yml** — dev → preview → main promotion with .NET version reads via `grep`
7. **squad-insider-release.yml** — Insider builds with `0.1.0-insider+SHA` format, NuGet install in release notes
8. **squad-docs.yml** — Validates `docs/` exists, lists `.md` files (no build system yet)

**Rationale:** All workflows now valid for C# project. Consistent patterns across all 8 workflows. Release flow unblocked. NuGet publishing resilient to missing credentials.

**Consequences:** 
- ✅ All workflows now valid for C# project
- ✅ squad-release.yml can tag releases on push to main
- ✅ Consistent .NET patterns in all workflows
- 📝 Maintenance: Update .NET versions across all workflows when .NET 11+ released

**Implementation:** Commits f351e71 and 141716c; all 8 workflow files rewritten.

---

### Decision 20: OIDC Trusted Publishing for NuGet

**Date:** 2026-03-18
**Author:** Switch (DevOps/Packaging)
**Status:** Implemented

**Context:** The `publish.yml` workflow used a long-lived `NUGET_API_KEY` secret for NuGet pushes, triggered by `push: tags: v*`. This has security concerns (key rotation, leak risk) and didn't integrate cleanly with the squad-release flow (which creates GitHub Releases, not just tags).

**Decision:** Migrated to **NuGet OIDC Trusted Publishing** using `NuGet/login@v1`:

1. **Trigger:** Changed from `push: tags: v*` to `release: published` + `workflow_dispatch` with optional version input
2. **Auth:** Replaced `NUGET_API_KEY` secret with OIDC token exchange via `NuGet/login@v1` + `NUGET_USER` secret
3. **Environment:** Added `environment: release` (required for OIDC policy matching)
4. **Permissions:** `id-token: write` + `contents: read` (for OIDC token minting)
5. **Version:** 3-tier priority (release tag → manual input → csproj fallback) with validation

**Release Chain:**

```
Push to main → squad-release.yml (creates GitHub Release) → release event triggers publish.yml → OIDC → NuGet
```

**Consequences:**

- ✅ No long-lived API keys to rotate or leak
- ✅ Scoped temp tokens per workflow run
- ✅ Clean integration with squad-release flow
- ✅ Manual dispatch for ad-hoc/pre-release publishes
- 📝 Requires one-time NuGet.org Trusted Publishing policy setup
- 📝 Requires GitHub `release` environment + `NUGET_USER` secret
- 📝 See `docs/publishing.md` for full setup guide

**Files Changed:**

- `.github/workflows/publish.yml` — Rewritten for OIDC
- `docs/publishing.md` — New publishing guide

---

### 2026-03-18T1200Z: User Directive — Docs Location

**By:** Bruno Capuano (via Copilot)
**What:** Only README.md and LICENSE should live at the repo root. All other documentation must be stored in the docs/ folder. This is a repo rule.
**Why:** User request — captured for team memory

---

### Decision 21: Tiny Tier ONNX Conversions (4 of 6 Succeeded)

**Date:** 2026-03-18  
**Author:** Dozer (ML/ONNX Conversion Engineer)  
**Status:** Implemented

**Scope:** Converted 6 Tiny tier models from HuggingFace to ONNX GenAI INT4 format.

**Outcome:**
- ✅ 4 succeeded: Qwen2.5-0.5B, TinyLlama-1.1B, Qwen2.5-1.5B, SmolLM2-1.7B
- ❌ 2 blocked: StableLM-2 (unsupported architecture), Gemma-2B (gated repo)

**Model Sizes (INT4):**
| Model | Size |
|-------|------|
| Qwen2.5-0.5B-Instruct | 841 MB |
| TinyLlama-1.1B-Chat | 871 MB |
| Qwen2.5-1.5B-Instruct | 1.85 GB |
| SmolLM2-1.7B-Instruct | 1.41 GB |

**Blockers:**
- StableLM-2: `NotImplementedError` — unsupported by onnxruntime_genai v0.12.1 builder
- Gemma-2B: 403 gated repo — requires license acceptance at HuggingFace

**Next:** Trinity to update KnownModels.cs; Bruno to accept Gemma license; Dozer to retry Gemma and evaluate alternative for StableLM-2.

---

### Decision 22: Small + Medium Tier ONNX Conversions (6 of 9 Succeeded)

**Date:** 2026-03-18  
**Author:** Dozer (ML/ONNX Conversion Engineer)  
**Status:** Implemented

**Scope:** Converted 9 Small + Medium tier models to ONNX GenAI INT4 format.

**Outcome:**
- ✅ 6 succeeded: Qwen2.5-3B/7B, Mistral-7B, Llama-3.1-8B, DeepSeek-R1-Distill-Qwen-14B, Mistral-Small-24B
- ❌ 3 blocked: Llama-3.2-3B, Gemma-2-2B/9B (all gated repos)

**Model Sizes (INT4):**
| Model | Size |
|-------|------|
| Qwen2.5-3B-Instruct | 3.0 GB |
| Qwen2.5-7B-Instruct | 6.3 GB |
| Mistral-7B-Instruct-v0.3 | 4.8 GB |
| Llama-3.1-8B-Instruct | 6.5 GB |
| DeepSeek-R1-Distill-Qwen-14B | 11.4 GB |
| Mistral-Small-24B-Instruct | 16.2 GB |

**Gated Models:** Llama-3.2-3B, Gemma-2-2B-IT, Gemma-2-9B-IT (require license acceptance)

**Observations:** Qwen2.5 family maintaining 100% success rate. INT4 size scales linearly (~0.7 GB per billion params). Mistral-Small-24B regex tokenizer warning noted but did not block conversion.

**Next:** Trinity to update KnownModels.cs; Bruno to accept Llama-3.2 and Gemma licenses; Dozer to retry gated models.

---

### Decision 23: Large Tier ONNX Conversions (2 of 6 Succeeded)

**Date:** 2026-03-18  
**Author:** Dozer (ML/ONNX Conversion Engineer)  
**Status:** Implemented

**Scope:** Attempted conversion of 6 Large tier models to ONNX GenAI INT4 format.

**Outcome:**
- ✅ 2 succeeded: Qwen2.5-14B, Qwen2.5-32B
- ❌ 4 blocked: Command-R (gated), Mixtral-8x7B (unsupported MoE), DeepSeek-70B (OOM), Llama-3.3-70B (gated + likely OOM)

**Model Sizes (INT4):**
| Model | Size |
|-------|------|
| Qwen2.5-14B-Instruct | 11.3 GB |
| Qwen2.5-32B-Instruct | 22.1 GB |

**Blockers by Type:**
| Reason | Models | Details |
|--------|--------|---------|
| Gated | Command-R (35B), Llama-3.3-70B | Require license acceptance |
| Architecture | Mixtral-8x7B (46.7B MoE) | MoE unsupported by builder v0.12.1 |
| Memory | DeepSeek-70B, (Llama-3.3-70B) | INT4 quantizer OOM during graph processing |

**Key Finding:** 70B model quantization requires chunked/streaming approach — builder cannot hold full graph in memory simultaneously. Alternative tools (optimum, llama.cpp) needed.

**Next:** Trinity to update KnownModels.cs; Bruno to accept Cohere Command-R and Meta Llama-3.3 licenses; Dozer to evaluate alternative quantization tools for 70B and MoE support.

---

### Decision 24: Documentation Overhaul — File Locations & New Guides

**Date:** 2026-03-18  
**Author:** Trinity (Core Dev)  
**Status:** Implemented

**Scope:** Comprehensive documentation reorganization per Bruno's directive; integration with 23-model registry.

**Changes:**
1. **Moved to docs/:** CONTRIBUTING.md, CHANGELOG.md
2. **Updated README:** Full 23-model table (Tiny/Small/Medium/Large) with ONNX status
3. **Created new docs:**
   - docs/samples.md — Usage examples and patterns
   - docs/benchmarks.md — Performance data across models
   - docs/onnx-conversion.md — ONNX conversion process
4. **Updated CI workflows:** All references now point to docs/CHANGELOG.md

**Rationale:**
- Clean repo root (only README.md + LICENSE)
- Comprehensive model reference for user decision-making
- Clear pathway for contributors and model additions
- Transparent changelog and benchmarking

**Consequences:**
- ✅ All documentation centralized in docs/
- 📝 Future model additions require README table + KnownModels.cs updates
- 📝 CI workflows locked to docs/CHANGELOG.md paths

**Integration:** All 246 tests passing; CI workflows validated against new paths.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
