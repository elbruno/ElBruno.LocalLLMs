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
**Status:** Superseded by Decision 28

**Context:** `new Model(path)` defaults to CPU. GPU needs provider configuration.

**Decision:** For CPU, use `new Model(path)` directly. For CUDA/DirectML, use `Config` class to configure providers before model creation.

**Rationale:** Cleanest API path — CPU is the common case and stays simple. GPU configuration is explicit and extensible.

**Superseded Notes:** Decision 28 keeps config-based provider wiring but changes defaults to `ExecutionProvider.Auto` and adds deterministic fallback order.

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

### 2026-03-27T15:58: User directives — RAG tool routing constraints

**By:** Bruno Capuano (via Copilot)

**What:**
1. Target both CPU and GPU — must work on both
2. Goal is to help with many tools (20+), not small catalogs
3. SLM task is tool selection only — no argument generation
4. Latency: as low as possible for local execution
5. Additional models to investigate: Gemma-3-270M (Google nano, native function calling) and TinyAgent-1.1B (Berkeley, specialized tool calling)

**Why:** User request — captured for team memory. These constraints narrow the model selection and architecture decisions for the RAG tool routing plan.

---

### Decision: RAG Tool Routing Implementation Plan Approved

**Date:** 2026-03-27  
**Author:** Morpheus (Lead/Architect)  
**Status:** Approved — ready for execution

## Context

Bruno confirmed constraints for the RAG tool routing pipeline: CPU+GPU, 20+ tools, tool selection only (no argument generation), minimize latency. Dozer's model research identified 6 candidate tiny SLMs. Morpheus's architecture evaluation recommended composition over integration.

## Decisions Made

### D1: Four-Phase Implementation Structure
Phases: (0) Model Conversion, (1) Benchmark Framework, (2) Sample/Integration, (3) Optimization, plus a documentation phase. This ordering ensures we have data before we optimize.

### D2: Benchmark-First Approach
No architectural commitments to a specific model or pipeline until benchmarks produce data. The benchmark framework (Phase 1) measures accuracy, latency, and memory across all 6 models on 3 catalog sizes with 5 prompt categories. Decisions about "recommended model" and "default pipeline" come from data, not intuition.

### D3: ToolSelectionService as Sample Code, Not a Library
The composition layer (`ToolSelectionService`) lives in `samples/`, not `src/`. Users copy and adapt it. This avoids creating a fourth NuGet package and keeps MCPToolRouter dependency-free. If the pattern proves popular, a separate `ElBruno.LocalRAG` package can be extracted later.

### D4: JSON Parsing Fallback Chain
Tiny models produce valid JSON only ~14% of the time. The parser uses a 5-strategy fallback chain: strict JSON → regex extraction → line-by-line matching → fuzzy matching → give up (fall back to embeddings). This is non-negotiable for production use with sub-1B models.

### D5: Cross-Encoder Re-Ranking as Alternative
If SLM re-ranking proves too slow or inaccurate at 0.5B, cross-encoder re-ranking (~100-300ms) is the planned alternative. Task 3.4 is explicitly included as a hedge against SLM underperformance.

### D6: Graceful Degradation is Mandatory
The SLM layer must never block or crash the pipeline. Timeout (default 5s), exception handling, and automatic fallback to embedding-only results are required in all code paths.

## Team Assignments

| Phase | Owner | Support |
|---|---|---|
| Phase 0 (Models) | Dozer | Trinity (KnownModels registration) |
| Phase 1 (Benchmarks) | Tank | Morpheus (scenario review) |
| Phase 2 (Sample) | Trinity | Morpheus (API review) |
| Phase 3 (Optimization) | Trinity + Dozer | Morpheus (design review) |
| Phase 4 (Docs) | Morpheus | — |

## Impact

- No changes to existing `ElBruno.LocalLLMs` core library API
- No changes to `MCPToolRouter` library
- New projects: 1 benchmark, 1 sample
- New docs: tool routing guide, architecture update
- Up to 5 new `ModelDefinition` entries in `KnownModels.cs` (pending conversion success)

## Reference

Full plan: `docs/plan-rag-tool-routing.md`

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

---

### Decision 25: 70B ONNX Conversions Not Feasible on This Machine

**Date:** 2026-03-18  
**Author:** Dozer (ML / ONNX Conversion Engineer)  
**Status:** Active

**Context**

Bruno accepted the Meta Llama 3.3 license on HuggingFace. Attempted to convert `meta-llama/Llama-3.3-70B-Instruct` to ONNX GenAI INT4 CPU format using `onnxruntime_genai.models.builder`.

The conversion progressed through:
1. ✅ Download: 30 files (~130 GB safetensors), ~16 minutes
2. ✅ Load checkpoint: 30 shards, ~1 minute
3. ✅ Read all 80 decoder layers + embedding + LM head
4. ❌ **"Saving ONNX model" (INT4 quantization + serialization):** Process killed by OS after 40+ minutes. Zero output files. Out of memory.

This is the **second confirmed 70B OOM failure** — DeepSeek-R1-Distill-Llama-70B (also 70B params) failed identically on 2026-03-18.

**Decision**

**70B parameter models cannot be converted to ONNX GenAI format on this machine (440 GB RAM).** Do not attempt further 70B conversions locally.

**Rationale**

The `onnxruntime_genai` builder's INT4 quantization step requires holding the entire ONNX graph plus all quantized weight tensors in memory simultaneously during protobuf serialization. For 70B models, this exceeds 440 GB. The OS OOM killer terminates the process silently.

This has now been validated across two different 70B architectures:
- **DeepSeek-R1-Distill-Llama-70B** (Qwen2 architecture) — MemoryError
- **Llama-3.3-70B-Instruct** (Llama architecture) — OOM kill

**Alternatives**

1. **Cloud VM with 512+ GB RAM** — Rent an Azure/AWS/GCP instance with sufficient memory for the conversion. Estimated cost: ~$5-10 for a single conversion run.
2. **Pre-converted models** — Check if ONNX GenAI versions are published by Microsoft or community on HuggingFace (e.g., under `microsoft/` or `onnx-community/`).
3. **GGUF format via llama.cpp** — `llama.cpp`'s quantization uses memory-mapped I/O and can handle 70B models on machines with less RAM. However, GGUF is not compatible with our `Microsoft.ML.OnnxRuntimeGenAI` library.
4. **Wait for builder improvements** — Future versions of `onnxruntime_genai` may implement streaming/chunked quantization that doesn't require the full graph in memory.

**Impact**

The following models in our conversion list are affected:
- `meta-llama/Llama-3.3-70B-Instruct` — ❌ Cannot convert locally
- `deepseek-ai/DeepSeek-R1-Distill-Llama-70B` — ❌ Already confirmed OOM

All models ≤32B params convert successfully on this machine.


**Next:** Trinity to update KnownModels.cs; Bruno to accept Gemma license; Dozer to retry Gemma and evaluate alternative for StableLM-2.

---

### Decision 26: Llama-3.3-70B-Instruct CUDA Conversion

**Date:** 2026-03-18  
**Author:** Dozer (ML Engineer)  
**Status:** Completed

**Context**

Llama-3.3-70B-Instruct previously failed twice with `-e cpu` — OOM after 40+ minutes, exhausting 440+ GB of the machine's 450 GB RAM. The ONNX serialization step tries to hold the entire quantized graph in memory at once. Bruno asked to retry with CUDA execution provider on what was reported as an A100 GPU (actually an A10-24Q with 24 GB VRAM).

**Decision**

Used `-e cuda` with `onnxruntime-genai-cuda` 0.12.2. This succeeded where CPU failed — the CUDA quantization path serializes INT4 weights incrementally (966 chunks) instead of building the full graph in RAM. Peak RAM stayed under 250 GB.

**Outcome**

- **Model converted:** 39.3 GB INT4 ONNX (80 layers, 128K context, GQA)
- **Uploaded:** `elbruno/Llama-3.3-70B-Instruct-onnx` on HuggingFace
- **Conversion time:** ~25-30 minutes (model was cached from prior attempt)
- **Local files cleaned up** after upload

**Implications**

- **DeepSeek-R1-Distill-Llama-70B should also work with `-e cuda`** — same 70B scale, same OOM pattern on CPU. Recommend retrying.
- **All future large model conversions (32B+) should default to `-e cuda`** even if a GPU isn't strictly needed — it uses a more memory-efficient quantization path.
- The GPU hardware (A10-24Q, 24 GB) was irrelevant — CUDA EP changes the algorithm, not the compute device. GPU utilization stayed at 0%.

---

### Decision 27: ConsoleAppDemo Sample Structure

**Date:** 2026-03-18  
**Author:** Trinity (Core Dev)  
**Status:** Implemented

**Context**

Bruno requested a comprehensive console sample app that demonstrates the full LocalLLMs API surface — download progress, metadata, Q&A, streaming, and multi-turn — following the LocalEmbeddings ConsoleApp visual pattern.

**Decision**

Created `samples/ConsoleAppDemo/` with a single `Program.cs` top-level program containing 4 sequential examples. Each example is self-contained but reuses the same `LocalChatClient` instance (created once with progress tracking in Example 1).

**Key choices**

- **Single client instance** for all examples — avoids re-downloading the model 4 times
- **`List<ChatMessage>`** for multi-turn — demonstrates the explicit history pattern rather than hiding it behind an abstraction
- **Expected cache path** via `Environment.SpecialFolder.LocalApplicationData` — since `_resolvedModelPath` is private, this shows users where to find their cached models
- **Box-drawn UI** matching the LocalEmbeddings pattern — consistency across samples

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

### Decision 25: Gemma Architecture Support Confirmed

**Date:** 2026-03-18  
**Author:** Dozer (ML/ONNX Conversion Engineer)  
**Status:** Implemented

**Scope:** Conversion of 5 target models: Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT, Llama-3.2-3B-Instruct, Llama-3.3-70B-Instruct

**Outcome:**
- ✅ 3 succeeded: Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT
- ❌ 2 blocked: Llama-3.2-3B (awaiting Meta review), Llama-3.3-70B (not authorized yet)

**Model Sizes (INT4):**
| Model | Size | HuggingFace Repo |
|-------|------|------------------|
| Gemma-2B-IT | 3.5 GB | elbruno/Gemma-2B-IT-onnx |
| Gemma-2-2B-IT | 3.8 GB | elbruno/Gemma-2-2B-IT-onnx |
| Gemma-2-9B-IT | 9.0 GB | elbruno/Gemma-2-9B-IT-onnx |

**Key Discovery:** Gemma architecture (both v1 and v2) is **FULLY SUPPORTED** by `onnxruntime_genai` builder v0.12.1. This was previously unknown. Both Gemma v1 (2B) and Gemma v2 (2B, 9B) convert cleanly to ONNX GenAI INT4 CPU format without warnings or errors.

**Architecture Support Matrix Update:**
| Architecture | Status | Models Tested |
|-------------|--------|---------------|
| Gemma v1 | ✅ Supported | Gemma-2B-IT |
| Gemma v2 | ✅ Supported | Gemma-2-2B-IT, Gemma-2-9B-IT |

**Technical Notes:**
- Gemma uses 256K vocab (256000 tokens), resulting in large embed_tokens weights even for small params
- Conversion times: Gemma-2B ~2 min, Gemma-2-2B ~3 min, Gemma-2-9B ~8 min
- INT4 quantization successful on all three; no memory issues

**Next:** Trinity to update KnownModels.cs with 3 Gemma entries; update README model table; run full test suite; commit validation.

---

## Decision: Gemma Models Now Use Native ONNX Repos

**Date:** 2026-03-18  
**Author:** Trinity (Core Dev)  
**Status:** Implemented  

### Context

Dozer converted 3 Gemma models to ONNX GenAI format and uploaded them to elbruno HuggingFace repos. KnownModels.cs previously pointed at Google's original repos with `HasNativeOnnx = false`.

### Decision

Updated all 3 Gemma entries in KnownModels to point at elbruno ONNX repos with `RequiredFiles = ["*"]` and `HasNativeOnnx = true`:
- `elbruno/Gemma-2B-IT-onnx` (Tiny tier)
- `elbruno/Gemma-2-2B-IT-onnx` (Small tier)
- `elbruno/Gemma-2-9B-IT-onnx` (Medium tier)

### Rationale

- GenAI format repos contain all necessary files (model, tokenizer, genai_config.json), so `["*"]` is the correct RequiredFiles pattern
- Consistent with how all other converted models (TinyLlama, Qwen, Llama-3.1, Mistral, etc.) are configured
- Users no longer need to run ONNX conversion for any Gemma v1/v2 model

### Impact

- Gemma models now work out of the box (no conversion step)
- 16 of 23 KnownModels now have native ONNX — only 7 still need conversion
- Remaining blocked Llama models (3.2-3B, 3.3-70B) are gated by Meta license, not architecture

---

## Decision: Llama Model ONNX Conversion Retry (2026-03-18)

**Author:** Dozer (ML Engineer)  
**Requested by:** Bruno Capuano  
**Status:** Completed

### Context

Bruno accepted the Meta Llama licenses on HuggingFace and asked for a retry of the two remaining Llama models:
1. Llama-3.2-3B-Instruct
2. Llama-3.3-70B-Instruct

### Results

| Model | Status | HuggingFace Repo | Notes |
|-------|--------|------------------|-------|
| Llama-3.2-3B-Instruct | ✅ Success | [elbruno/Llama-3.2-3B-Instruct-onnx](https://huggingface.co/elbruno/Llama-3.2-3B-Instruct-onnx) | INT4 CPU, ~3.5 GB. Converted and uploaded cleanly. |
| Llama-3.3-70B-Instruct | ❌ Failed — Still Gated | N/A | 403: "Your request to access model meta-llama/Llama-3.3-70B-Instruct is awaiting a review from the repo authors." Llama 3.3 has a separate license from 3.2 — Meta has not yet approved it. |

### Details: Llama-3.2-3B-Instruct ✅

- **Conversion:** Completed successfully with `onnxruntime_genai` builder v0.12.1, INT4 CPU
- **Output files:** genai_config.json, model.onnx (210 KB), model.onnx.data (3,482 MB), tokenizer.json (16.4 MB), tokenizer_config.json, special_tokens_map.json, chat_template.jinja
- **Total size:** ~3.5 GB INT4 (consistent with 3B parameter model)
- **Upload:** Completed to elbruno/Llama-3.2-3B-Instruct-onnx at ~290 MB/s
- **Local cleanup:** Done

### Details: Llama-3.3-70B-Instruct ❌

- **Error:** `GatedRepoError: 403 Client Error` — access is "awaiting a review from the repo authors"
- **Root cause:** Llama 3.3 has a separate gated license from Llama 3.2 on HuggingFace. Bruno accepted the Llama 3.2 license (which worked), but the Llama 3.3 license request is still pending Meta's approval.
- **Note:** Even if access is granted, this 70B model will very likely hit the same MemoryError seen with DeepSeek-R1-Distill-Llama-70B — the INT4 quantization step exceeds 450 GB RAM.

### Action Items

1. **Bruno:** Check Llama 3.3 license status at https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct — it may need a separate acceptance from Llama 3.2.
2. **When approved:** Retry conversion, but expect MemoryError. Document if it fails for the same reason as the 70B DeepSeek.
3. **Alternative for 70B:** Consider `optimum` library, `llama.cpp` GGUF, or a higher-RAM machine if the builder fails.

---

### Decision 28: Auto Provider Default + GPU-First Fallback + Stable Console Progress

**Date:** 2026-03-19  
**Author:** Trinity (Core Dev)  
**Status:** Active

**Context:**

- Console sample progress output was noisy and fragmented.
- Runtime and model defaults were effectively CPU-biased for common paths.

**Decision:**

- Introduce `ExecutionProvider.Auto` as the default provider.
- Resolve `Auto` provider at runtime in deterministic order: `Cuda -> DirectML -> Cpu`.
- Keep explicit provider behavior unchanged (`Cpu`, `Cuda`, `DirectML` use only requested provider).
- Update default Phi model definitions to GPU variant subpaths where available.
- Render console download progress in-place using carriage return updates with one final newline.

**Rationale:**

- Better out-of-box performance while preserving CPU compatibility.
- Deterministic fallback is easy to validate with tests.
- GPU variant defaults prevent accidental CPU-variant selection when GPU paths exist.
- Single-line progress output improves readability and avoids console spam.

**Validation:**

- `LocalLLMsOptionsTests` passed.
- `ProviderSelectionTests` passed.
- `samples/ConsoleAppDemo` build passed.

**Consequences:**

- New default path favors GPU acceleration automatically when available.
- Samples can display requested vs active execution provider, including CPU fallback.
- Existing explicit provider configurations remain backward-compatible.

---

### Decision 29: Model Management Scripts Safety and Operations Contract

**Date:** 2026-03-19
**Author:** Trinity (Core Dev), Tank (Tester/QA)
**Status:** Active

**Context:**

- Model cache operations needed a full management flow beyond deletion only.
- `scripts/manage-models.ps1` was introduced for inventory, locations, and deletion workflows.
- QA updated `scripts/delete-models.ps1` to reinforce native WhatIf/Confirm behavior and safer output handling.
- Script documentation in `scripts/README.md` was updated and must stay aligned with behavior.

**Decision:**

- Keep `scripts/manage-models.ps1` as the primary operational script with explicit parameter sets for list, locations, report, delete-one, and delete-all.
- Keep `scripts/delete-models.ps1` for backward compatibility, but require native `SupportsShouldProcess` semantics for dry-run safety (`-WhatIf`/`-Confirm`) and explicit delete intent.
- Preserve interactive confirmation for destructive actions unless force is explicitly provided.
- Treat `scripts/README.md` as the canonical operator reference for both scripts.

**Rationale:**

- Separates daily model inventory/reporting from destructive cleanup operations.
- Improves safety by standardizing around built-in PowerShell dry-run and confirmation patterns.
- Reduces accidental deletion risk while preserving compatibility with existing automation.

**Consequences:**

- Users get a single discoverable model-management flow without losing legacy delete commands.
- QA coverage should include both management and legacy delete script safety gates.
- Documentation drift between scripts and `scripts/README.md` becomes a release risk and must be checked.

---

### Inbox Merge Note: 2026-03-19 Progress Rendering and Provider Fallback Proposal

**Date:** 2026-03-19
**Source:** `.squad/decisions/inbox/2026-03-19-progress-and-provider-fallback.md`
**Disposition:** Merged into Decision 28

The proposal content (Auto fallback scope, provider-unavailable-only fallback, and progress renderer behavior) was already captured by Decision 28 and related implementation/test updates. No separate decision was added to avoid duplication.

---

### Decision 30: GPU Support via Additive NuGet Packages

**Date:** 2026-03-19
**Author:** Trinity (Core Dev)
**Status:** Implemented

**Context**

The library ships `Microsoft.ML.OnnxRuntimeGenAI` (CPU-only) as its base dependency. The runtime fallback code in `OnnxGenAIModel.cs` already tries DirectML→CUDA→CPU on Windows and CUDA→CPU on Linux, but GPU providers were never available because only the CPU package was referenced.

**Decision**

GPU support is **additive at the app level**, not baked into the library package:

1. **Library** (`ElBruno.LocalLLMs`) keeps CPU-only NuGet ref — works everywhere
2. **Consumers** add `Microsoft.ML.OnnxRuntimeGenAI.Cuda` or `.DirectML` to their app project
3. Runtime auto-detects available providers — zero code changes required

**Rationale**

- Bundling CUDA (~800MB) or DirectML in the library NuGet would bloat it and break users without compatible hardware
- The additive pattern mirrors how Microsoft ships ONNX Runtime itself (base + provider packages)
- v0.12.2 confirmed: QNN has no .NET NuGet, WinML is a system component — no new enum values needed

**Consequences**

- README documents the pattern clearly with `dotnet add` commands
- ConsoleAppDemo shows commented-out GPU refs as a template
- `ExecutionProvider` enum stays at 4 values: Auto, Cpu, Cuda, DirectML
- Error pattern matching in `ShouldFallbackToNextProvider` expanded for robustness

---

### Decision 31: BuildProviderFailureReason Visibility Changed to Internal

**Date:** 2026-03-19
**Author:** Tank (Tester)
**Status:** Active

**Context**

`OnnxGenAIModel.BuildProviderFailureReason` was `private static`, making it untestable directly. The method has non-trivial behavior (truncation at 180 chars, newline replacement, formatting) that warrants direct unit tests.

**Decision**

Changed `BuildProviderFailureReason` from `private static` to `internal static`. The test project already has `InternalsVisibleTo` configured, so no additional plumbing was needed.

**Rationale**

Testing this through the constructor would require mocking `Model` creation (ONNX native interop) — impractical without a factory seam. Direct testing is cleaner and catches regressions in truncation/formatting logic. This follows the same pattern used for `ShouldFallbackToNextProvider` and `GetProviderFallbackOrder`, which are already `internal static`.

**Consequences**

None negative. The method is a pure function with no side effects. Exposing it to `internal` does not change any public API surface.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
---

### Decision 31: Tiny SLM Model Selection for RAG Tool Routing

**Date:** 2026-03-27
**Author:** Dozer (ML/ONNX Conversion Engineer)
**Status:** Active

**Context:** MCPToolRouter needs an optional SLM-enhanced routing capability for scenarios with large tool catalogs or ambiguous queries. Research evaluated 15+ tiny models (sub-1B to 1.5B params).

**Decision:** Use **Qwen2.5-0.5B-Instruct** as the primary recommendation, with **SmolLM2-360M-Instruct** as the tested backup.

**Rationale:**
- Qwen2.5-0.5B is already converted to INT4 (825 MB), reducing implementation friction
- Native tool/function calling support explicitly built into Qwen2.5 architecture
- 32K context window sufficient for 3-10 tool descriptions
- Research shows specialized sub-1B models can achieve 77%+ accuracy on tool-calling tasks (OPT-350M fine-tuned: 77.55% vs. ChatGPT-CoT: 26%, ToolLLaMA-7B: 30%)
- Small vocab (151936) provides good tokenization speed

**Alternatives:**
- **SmolLM2-360M-Instruct** — Smaller (faster), purpose-built for edge deployment, smaller vocab (49152)
- **Qwen3-0.6B-Instruct** — Newest model (Apr 2025), thinking mode for reasoning, 36T token training
- **Gemma-3-1B** — Official ONNX support from Google, mixed local/global attention, 32K context

**Consequences:**
- 1.2s latency per routing decision (vs. 15-40ms for embeddings-only)
- Adds dependency on ElBruno.LocalLLMs to applications using enhanced routing
- Requires testing on real MCPToolRouter prompts to validate accuracy

---

### Decision 32: Keep MCPToolRouter Pure + Composition Pattern for Optional SLM Layer

**Date:** 2026-03-27
**Author:** Morpheus (Lead/Architect)
**Status:** Active

**Context:** Proposed adding SLM reasoning to MCPToolRouter for better tool selection on large catalogs. Architecture evaluation found tradeoffs between accuracy and latency.

**Decision:** Keep MCPToolRouter as a pure embedding-search library. Create a composition pattern (sample/extension) demonstrating how users can optionally add SLM reasoning.

**Rationale:**
- Embedding-only routing (40ms, ~90% accuracy) is sufficient for 80% of use cases
- SLM adds 85-227x latency (1.2-3.4s) for minimal accuracy gain (2%)
- MCPToolRouter is already clean, focused, and testable
- MEAI interfaces (IEmbeddingGenerator) allow composition without source code changes
- Users with large catalogs (100+ tools) or ambiguous queries can opt-in via composition
- Keeps library minimal and maintainable

**Alternative Approaches Considered:**
- **Cross-encoder re-ranking** — Better accuracy ranking, 100-300ms latency
- **Multi-stage embedding search** — Coarse filter + fine filter, no SLM
- **Rule-based + embedding hybrid** — Specialized rules for known patterns
- **Embedding + SLM inside MCPToolRouter** — Rejected (adds bloat, makes pure routing harder)

**Consequences:**
- MCPToolRouter remains a focused, small library
- Sample code shows composition pattern for advanced users
- Documentation explains decision tree: when embeddings suffice vs. when SLM helps
- No architectural change to core library

**Sweet Spot Model (if SLM added later):**
- **Qwen2.5-1.5B-Instruct** — Best structured JSON output in <2B category, 16% exact match rate
- Provides ~92% tool selection accuracy (vs. 90% for embeddings alone)
- Runs on GPU (NVIDIA/AMD/Intel) for 5-30x speedup if needed

---

---

# Decision: Phase 4 RAG + Tool Routing Architecture

**Date:** 2026-03-18  
**Author:** Morpheus (Lead/Architect)  
**Status:** Active  
**Phase:** 4 (Tool Calling + RAG Pipeline)

---

## Context

Phase 4 adds two major capabilities to ElBruno.LocalLLMs:

1. **Tool Calling (Tool Routing):** Enable `LocalChatClient` to support function calling through prompt-based parsing, since ONNX Runtime GenAI lacks native tool calling support.

2. **RAG Pipeline:** Provide abstractions for Retrieval-Augmented Generation, integrating with ElBruno.LocalEmbeddings for local vector search.

These features are critical for building agentic applications that can:
- Take actions via tool/function calls (query databases, call APIs, perform calculations)
- Ground responses in private/domain-specific knowledge via RAG
- Run entirely on-premises without cloud dependencies

---

## Decision 1: Prompt-Based Tool Calling (Not Native ONNX)

**Decision:** Implement tool calling through **prompt engineering and output parsing**, not native ONNX Runtime features.

**Rationale:**
- ONNX Runtime GenAI (v0.12.2) does not provide native function calling APIs like OpenAI's Chat Completions API
- Open-source models (Qwen2.5, Llama3, Phi4) have established prompt-based tool calling conventions
- This matches the pattern used by vLLM, Ollama, and other local inference engines
- We can support model-specific formats (Qwen's `<tool_call>` tags, Llama's JSON, Phi4's `functools[...]`) through formatters

**Consequences:**
- Each chat template formatter must implement tool injection and parsing
- Tool calling accuracy depends on model quality (smaller models may hallucinate or malform tool calls)
- No streaming tool calls in Phase 4a (requires buffering and partial parsing — defer to future phase)
- Models without tool calling training cannot support this feature

**Alternatives considered:**
- Wait for native ONNX GenAI support → Rejected: no timeline, blocks critical feature
- Use a separate tool routing model → Rejected: adds complexity, latency, memory overhead
- Only support one tool format → Rejected: limits model choice, poor user experience

---

## Decision 2: Tool Support as Model Capability Flag

**Decision:** Add `SupportsToolCalling` and `ToolCallingFormat` properties to `ModelDefinition`.

**Rationale:**
- Not all models can do tool calling (e.g., Phi-3.5-mini is too small)
- Different model families use incompatible formats (Qwen ≠ Llama ≠ Phi4)
- Early validation prevents confusing runtime errors
- Clear documentation of which models support tools

**API Design:**
```csharp
public sealed record ModelDefinition
{
    // ... existing properties ...
    public bool SupportsToolCalling { get; init; }
    public ToolCallingFormat? ToolFormat { get; init; }
}

public enum ToolCallingFormat
{
    QwenHermes,      // <tool_call>...</tool_call>
    Llama3Json,      // {"name":"...","arguments":{...}}
    Llama3Pythonic,  // [func(param=value), ...]
    Phi4Functools,   // functools[{...}]
    ChatMLJson       // JSON blocks
}
```

**Consequences:**
- When `ChatOptions.Tools` is provided but model doesn't support it → throw `NotSupportedException` with helpful message
- Users can query model capabilities before sending tool requests
- Each new model must declare tool support in `KnownModels`

---

## Decision 3: Extend IChatTemplateFormatter (Not New Interface)

**Decision:** Add tool-related methods to existing `IChatTemplateFormatter` interface.

**Rationale:**
- Tool formatting is an extension of message formatting (same role-based structure)
- Chat templates that don't support tools can throw `NotSupportedException` from default base class
- Keeps all prompt formatting logic in one place
- Avoids proliferation of formatter types

**API Design:**
```csharp
internal interface IChatTemplateFormatter
{
    string FormatMessages(IList<ChatMessage> messages);
    
    string FormatMessagesWithTools(
        IList<ChatMessage> messages, 
        IList<AITool> tools,
        ChatToolMode toolMode);
    
    ToolCallParseResult? ParseToolCalls(string modelOutput);
}
```

**Consequences:**
- Non-tool formatters inherit default "throw NotSupportedException" from base class
- Tool-capable formatters override both `FormatMessagesWithTools` and `ParseToolCalls`
- Parsing logic is formatter-specific (Qwen uses regex for XML tags, Llama uses JSON deserialization)

---

## Decision 4: FunctionCallContent with Generated CallId

**Decision:** If the model's output doesn't include a call ID, generate one (GUID).

**Rationale:**
- Microsoft.Extensions.AI requires `FunctionCallContent.CallId` to match results
- Most open models don't generate call IDs (unlike OpenAI)
- Generating consistent IDs enables proper multi-turn flow

**Implementation:**
```csharp
foreach (var parsedCall in parseResult.ToolCalls)
{
    var callId = parsedCall.CallId ?? Guid.NewGuid().ToString();
    contents.Add(new FunctionCallContent(
        callId: callId,
        name: parsedCall.Name,
        arguments: ParseArgumentsAsDict(parsedCall.ArgumentsJson)
    ));
}
```

**Consequences:**
- Call IDs are stable within a single request/response cycle
- Users don't need to track IDs manually
- Matches behavior of other MEAI providers

---

## Decision 5: No Streaming Tool Calls in Phase 4a

**Decision:** Tool calling only works with `GetResponseAsync` (non-streaming). If tools are passed to `GetStreamingResponseAsync`, fall back to non-streaming internally or throw.

**Rationale:**
- Streaming tool calls requires buffering partial JSON/XML until parseable
- Different models emit tool calls at different token positions (some prefix, some suffix)
- Complex state machine for multi-call parsing
- Non-streaming is sufficient for most tool calling use cases (tools are CPU-bound, not token-bound)

**Future work:**
- Phase 4c (post-MVP) can add streaming with buffered parsing

**Consequences:**
- Users calling tools must use `GetResponseAsync`
- Documentation must clarify this limitation
- Streaming remains available for normal chat (no tools)

---

## Decision 6: ToolCallingAgent Sample Pattern

**Date:** 2026-03-27  
**Author:** Trinity  
**Status:** Active

**Context:** Phase 4a tool calling feature needed reference implementation to demonstrate agent patterns and tool design.

**Decisions:**

1. **Model default: Qwen2.5-0.5B-Instruct** — smallest model with `SupportsToolCalling = true`. Fast to download (~1 GB), low RAM, good for first-run experience. Comments suggest Phi-3.5-mini/Qwen-7B for production quality.

2. **Agent loop pattern** — the sample demonstrates the canonical multi-turn pattern: send message with tools → check for `FunctionCallContent` → invoke tools → send `FunctionResultContent` back → repeat until text response. This is the recommended pattern for users to follow.

3. **Tool invocation via `AIFunctionArguments`** — MEAI 10.x requires wrapping `call.Arguments` in `new AIFunctionArguments(dict)` for `AIFunction.InvokeAsync`. Documented in history for future samples.

4. **Three orthogonal tool types** — time (real system call), math (pure computation), weather (mock data) — covers the typical tool categories users will implement.

**Rationale:**
- Sample provides concrete working code users can reference and run immediately
- Multi-turn loop is the fundamental pattern for tool use
- Demonstrates real, synthetic, and mock tool categories
- Qwen2.5-0.5B balances model quality with accessibility for first-time users

**Consequences:**
- New `samples/ToolCallingAgent/` directory created and integrated into solution
- Feature documented in supported-models.md, getting-started.md, CHANGELOG.md
- Reference implementation available for users exploring Phase 4a

---

## Decision 7: RAG as Extension Package (ElBruno.LocalLLMs.Rag)

**Decision:** Create a separate NuGet package `ElBruno.LocalLLMs.Rag` instead of adding RAG to the core library.

**Rationale:**
- RAG is optional — many users only need chat completions
- Keeps core library focused and lightweight
- Allows separate versioning (RAG may evolve faster than core)
- Follows pattern from ElBruno.LocalEmbeddings (`.VectorData`, `.KernelMemory` are extensions)
- Users can implement custom RAG pipelines without depending on our abstractions

**Consequences:**
- Users must install both `ElBruno.LocalLLMs` and `ElBruno.LocalLLMs.Rag`
- RAG integration is at application level (not built into `LocalChatClient`)
- Clear separation of concerns: core = chat, extension = retrieval

**Alternatives considered:**
- Include RAG in core → Rejected: bloats core, adds dependencies (SQLite, etc.)
- No official RAG package → Rejected: users expect guidance, reference implementation

---

## Decision 7: IDocumentStore Abstraction (Not Tied to Specific Vector DB)

**Decision:** Define `IDocumentStore` interface with in-memory and SQLite implementations. Allow users to plug in Qdrant, Milvus, etc.

**Rationale:**
- Different users have different scale/deployment needs
- In-memory store is great for demos, prototypes, tests
- SQLite is sufficient for 10K-100K documents (no external dependencies)
- Production users can integrate Qdrant/Milvus/Weaviate via adapter pattern
- Interface-based design keeps RAG pipeline testable and swappable

**API Design:**
```csharp
public interface IDocumentStore
{
    Task AddAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default);
    
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

**Consequences:**
- Users can swap stores without changing pipeline code
- SQLite store does brute-force cosine similarity (no vector indexing) → fine for small-medium datasets
- For large scale (1M+ docs), users should use a proper vector DB
- Documentation must guide users on when to migrate

---

## Decision 8: RagContext as Formatted String (Not Raw Chunks)

**Decision:** `IRagPipeline.RetrieveContextAsync` returns `RagContext` with both raw chunks and a pre-formatted context string.

**Rationale:**
- Most users want "just inject this into the prompt" simplicity
- Advanced users can access raw chunks for custom formatting
- Default formatting is opinionated but overridable

**API Design:**
```csharp
public sealed record RagContext(
    string Query,
    IReadOnlyList<DocumentChunk> RetrievedChunks,
    string FormattedContext  // "Relevant context:\n\n[1] ...\n\n[2] ..."
);
```

**Usage:**
```csharp
var context = await ragPipeline.RetrieveContextAsync(userQuery);
var messages = new List<ChatMessage>
{
    new(ChatRole.System, "Answer using context:\n\n" + context.FormattedContext),
    new(ChatRole.User, userQuery)
};
```

**Consequences:**
- Simple default path for most users
- Power users can ignore `FormattedContext` and format chunks themselves
- Formatting logic is centralized in `LocalRagPipeline`

---

## Decision 9: SlidingWindowChunker as Default (Not Semantic)

**Decision:** Default chunker uses fixed-size sliding window with overlap. Semantic/recursive chunking is future enhancement.

**Rationale:**
- Sliding window is simple, deterministic, fast
- Works for any text (no parsing, no model inference)
- Overlap preserves context across chunk boundaries
- Good enough for 80% of RAG use cases

**Parameters:**
```csharp
public IEnumerable<DocumentChunk> ChunkDocument(
    Document document,
    int chunkSize = 512,      // ~512 tokens worth of characters
    int overlapSize = 50);     // 50 char overlap
```

**Future work:**
- Semantic chunking (embed each sentence, cluster similar ones)
- Recursive splitting (markdown-aware, respects headings/lists)
- Custom chunkers via `IDocumentChunker`

**Consequences:**
- Users must tune chunk size based on model context window and embedding model limits
- Chunks may split mid-sentence (acceptable with overlap)
- Simple to implement, test, and debug

---

## Decision 10: Tool Calling + RAG Integration at Application Level

**Decision:** RAG pipeline and tool calling are independent features. Combining them is done in user code, not library code.

**Rationale:**
- Tool calling is a chat client feature (request/response protocol)
- RAG is a retrieval feature (document indexing and search)
- Use cases vary wildly:
  - Some apps: RAG only (no tools)
  - Some apps: Tools only (no RAG)
  - Some apps: RAG as a tool (`SearchDocumentation(query)`)
  - Some apps: RAG context + separate tools
- Forcing an opinionated integration would limit flexibility

**Example (RAG as a tool):**
```csharp
var tools = new List<AITool>
{
    AIFunctionFactory.Create(SearchDocumentation)
};

[Description("Search technical documentation")]
async Task<string> SearchDocumentation(string query)
{
    var context = await ragPipeline.RetrieveContextAsync(query);
    return context.FormattedContext;
}
```

**Consequences:**
- Users have full control over how RAG and tools interact
- Documentation must provide clear patterns and samples
- `samples/RagWithTools` demonstrates best practices

---

## Decision 11: Initial Model Support for Tool Calling

**Decision:** Phase 4a supports 4 models:
1. Qwen2.5-3B-Instruct (QwenHermes format)
2. Qwen2.5-7B-Instruct (QwenHermes format)
3. Llama-3.2-3B-Instruct (Llama3Json format)
4. Phi-4 (Phi4Functools format)

**Rationale:**
- These models are already in `KnownModels` with ONNX support
- Cover three major tool calling formats (proves flexibility)
- Range of sizes (3B to 7B to 14B)
- Well-documented tool calling conventions

**Future additions:**
- DeepSeek-R1-Distill-Qwen-7B (uses QwenHermes)
- Llama-3.2-1B-Instruct (if tool calling works at 1B scale)
- Any new models as they're added to the library

**Consequences:**
- Phi-3.5-mini-instruct does NOT support tools (too small, no training data)
- Documentation must clearly show which models support tools
- Tool calling tests must cover all three formats

---

## Summary of Architectural Decisions

| # | Decision | Rationale | Impact |
|---|----------|-----------|--------|
| 1 | Prompt-based tool calling | No native ONNX support, matches ecosystem | Each formatter implements tool logic |
| 2 | Tool capability flags | Clear model support validation | Users know which models support tools |
| 3 | Extend IChatTemplateFormatter | Keeps formatting centralized | Default base class throws NotSupported |
| 4 | Generate CallId if missing | MEAI requires IDs, models don't provide | Stable IDs within request cycle |
| 5 | No streaming tools in Phase 4a | Complex buffering, defer to future | Tool calls use GetResponseAsync only |
| 6 | RAG as extension package | Optional, focused, swappable | Separate NuGet install |
| 7 | IDocumentStore abstraction | Pluggable backends (in-memory, SQLite, Qdrant) | Users choose scale vs simplicity |
| 8 | RagContext with formatted string | Simple default, raw chunks available | One-line prompt injection |
| 9 | Sliding window chunker | Simple, fast, deterministic | Users tune chunk size |
| 10 | Tool + RAG integration in user code | Maximum flexibility, no forced patterns | Samples demonstrate patterns |
| 11 | Support 4 models in Phase 4a | Qwen, Llama, Phi4 — three formats | Proves flexibility, covers ecosystem |

---

## Next Steps

1. **Trinity:** Implement Phase 4a (tool calling) following the plan in `docs/plan-rag-tool-routing.md`
2. **Neo:** Create unit tests for tool parsing (Qwen, Llama3, Phi4 formats)
3. **Switch:** Build integration tests with real models
4. **Cypher:** Create `samples/ToolCallingAgent` and `samples/RagChatbot`
5. **Trinity:** Implement Phase 4b (RAG pipeline)
6. **Tank:** Update docs (`tool-calling-guide.md`, `rag-guide.md`, update getting-started)

**Timeline:** 4-5 weeks total (2-3 weeks tool calling, 2 weeks RAG)

---

**Consequences if NOT done:**
- Users cannot build agentic applications (no tool calling = no actions)
- Users cannot ground LLMs in private data (no RAG = hallucinations on domain-specific queries)
- Competitive disadvantage vs Ollama, LM Studio, vLLM (all support tool calling)
- ElBruno.LocalLLMs remains a "chat demo" library, not production-ready

**Consequences of doing it this way:**
- Clean abstraction boundaries (core vs extensions)
- Model-specific complexity is isolated in formatters
- Users have flexibility to implement custom RAG/tool patterns
- Phase 4a/4b can be released independently
- Foundation for future enhancements (streaming tools, semantic chunking, vector DB integrations)

---

**Approved:** Morpheus, 2026-03-18  
**Review status:** Ready for team review


---

# Tool Calling Implementation: Prompt-Based Approach

**Date:** 2026-03-27  
**Author:** Trinity  
**Status:** Implemented

## Context

LocalChatClient needed tool/function calling support to comply with `IChatClient` from Microsoft.Extensions.AI. Since ONNX Runtime GenAI doesn't have native function calling APIs, we needed a prompt-based approach.

## Decision

Implement tool calling via **prompt injection + JSON parsing**:

1. **Tool Definition Formatting**: Inject tool schemas as JSON into system message
2. **Tool Call Parsing**: Parse `FunctionCallContent` from LLM's text output using regex + JSON
3. **Tool Result Handling**: Format `FunctionResultContent` back into prompts for next turn

## Implementation Details

### Architecture

- **ToolCalling namespace** (`src/ElBruno.LocalLLMs/ToolCalling/`):
  - `ParsedToolCall` record — intermediate format before MEAI types
  - `IToolCallParser` interface — extensible for different model formats
  - `JsonToolCallParser` — handles `<tool_call>` tags, raw JSON, arrays
  - `ToolCallParserFactory` — resolves parser by `ChatTemplateFormat`

- **Template Formatter Changes**:
  - Extended `IChatTemplateFormatter` with `FormatMessages(messages, tools)` overload
  - Backward compatible — single-arg version delegates to two-arg with `null`
  - ChatML format fully implemented with tool injection + result formatting
  - Other formats stubbed (TODOs for future)

- **LocalChatClient Integration**:
  - `GetResponseAsync` passes tools to formatter, parses output, builds `FunctionCallContent`
  - `GetStreamingResponseAsync` accumulates text, parses at end, emits tool calls as updates
  - `BuildResponseMessage` helper combines text + tool calls into multi-content message

### Model Support

Added `SupportsToolCalling` to `ModelDefinition`. Enabled for:
- Phi-3.5-mini-instruct
- Phi-4
- Qwen2.5-0.5B-Instruct
- Qwen2.5-1.5B-Instruct
- Qwen2.5-3B-Instruct
- Qwen2.5-7B-Instruct

ChatML and Qwen formats are most compatible — Phi-3 can work with ChatML formatter.

### Parsing Strategy

`JsonToolCallParser` handles multiple LLM output styles:

1. **Tagged format** (Qwen, ChatML):
   ```
   <tool_call>
   {"name": "get_weather", "arguments": {"city": "Seattle"}}
   </tool_call>
   ```

2. **Raw JSON**:
   ```json
   {"name": "get_weather", "arguments": {"city": "Seattle"}}
   ```

3. **Array format**:
   ```json
   [{"name": "tool1", ...}, {"name": "tool2", ...}]
   ```

Auto-generates CallId if model doesn't provide one.

## Consequences

### Positive

- ✅ Full `IChatClient` compliance with tool calling
- ✅ No ONNX Runtime modifications required
- ✅ Extensible to other models/formats via `IToolCallParser`
- ✅ Backward compatible — existing code unaffected

### Negative

- ⚠️ Requires LLM to output structured JSON (not all models trained for this)
- ⚠️ Parsing is best-effort — malformed JSON won't be detected as tool calls
- ⚠️ Other formatters (Llama3, Mistral, etc.) need individual implementations

### Future Work

- Implement tool formatting for Phi3Formatter, QwenFormatter, Llama3Formatter
- Add validation: reject tool calls for models with `SupportsToolCalling = false`
- Consider streaming tool calls token-by-token (currently parsed at end)
- Add `ToolMode.RequireAny` enforcement (currently Auto/None distinction not enforced)

## Alternatives Considered

1. **Wait for ONNX Runtime native support** — timeline unknown, blocks MEAI compliance
2. **Fine-tune models for tool calling** — out of scope for library
3. **External orchestration** (LangChain-style) — defeats purpose of `IChatClient` abstraction

## References

- Microsoft.Extensions.AI 10.4.0 — `FunctionCallContent`, `FunctionResultContent`, `AIFunction`
- ChatML format spec — tool injection pattern
- Qwen documentation — `<tool_call>` tag convention


---

# Decision: Tool Calling Test Strategy

**Date:** 2026-03-19  
**Author:** Tank (Tester)  
**Status:** Proposed  
**Context:** Trinity is implementing tool calling support. Tank created proactive tests from spec.

## Decision

Created comprehensive test suite for tool calling with **41 tests** across 3 test files:

### 1. JsonToolCallParserTests (29 tests)
- **Happy path:** Single calls (tagged/raw), multiple calls (array), nested args, various types
- **Edge cases:** No calls, empty string, malformed JSON, empty args, missing keys
- **Format-specific:** Qwen tags, ChatML plain JSON, array format, whitespace variations
- **Robustness:** Unique CallId generation, RawText capture, null handling

### 2. ChatMLFormatterToolTests (12 tests)
- **Backwards compat:** null/empty tools behave like original formatter
- **Tool formatting:** Single tool with description/parameters, multiple tools
- **Edge cases:** Tools with no description, tools with no parameters
- **Integration:** Tools with system messages, multi-turn conversations
- **Structure:** Correct ChatML format, ends with assistant prompt

### 3. FunctionCallContentIntegrationTests (20 tests)
- **Round-trip:** Tools → model output → FunctionCallContent
- **FunctionResultContent:** Formatting in messages, conversation continuation
- **Multiple calls:** All parsed correctly, mapped to FunctionCallContent
- **ChatOptions:** Tools parameter passing, null handling
- **MEAI types:** FunctionCallContent/FunctionResultContent API correctness

## Implementation Approach

**Proactive testing pattern:**
1. Created stub implementations (`ParsedToolCall`, `IToolCallParser`, `JsonToolCallParser`) so tests compile
2. Marked stubs with `// TODO: Trinity to implement` 
3. Tests define **expected behavior** via assertions
4. 24/41 tests pass (stubs + Trinity's partial implementation)
5. 17 tests fail waiting for Trinity's JsonToolCallParser logic

**Co-development coordination:**
- Trinity already implemented ChatMLFormatter tool support (found during test creation)
- Fixed API issues: AIFunction uses `.Name`/`.Description` (not `.Metadata`)
- Fixed constructor: ChatResponseUpdate(role, contents) not object initializer
- All tests now **compile successfully** and are ready for Trinity

## Test Coverage Rationale

**Why 41 tests?**
- Tool calling has complex surface area: multiple formats, edge cases, MEAI integration
- Each test validates a **specific invariant** (not just "does it work")
- Parser must handle 3 formats (Qwen tags, ChatML JSON, arrays) robustly
- Formatter must maintain backwards compatibility while adding new behavior
- Integration tests verify end-to-end flow through MEAI types

**What's NOT tested:**
- Actual ONNX model responses (requires real model, covered by integration tests later)
- Tool execution logic (that's AIFunction's responsibility, not ours)
- Parameter schema generation (TODO for Trinity, stubbed as empty object)

## Files Created

```
src/ElBruno.LocalLLMs/ToolCalling/
  ├── ParsedToolCall.cs (stub record)
  ├── IToolCallParser.cs (stub interface)
  └── JsonToolCallParser.cs (stub implementation)

tests/ElBruno.LocalLLMs.Tests/ToolCalling/
  ├── JsonToolCallParserTests.cs (29 tests)
  ├── ChatMLFormatterToolTests.cs (12 tests)
  └── FunctionCallContentIntegrationTests.cs (20 tests)
```

## Key Learnings

1. **MEAI v10.4.0 API quirks:**
   - FunctionCallContent(callId, name, arguments)
   - FunctionResultContent(callId, result) — no "name" parameter
   - ChatResponseUpdate(role, contents) — not object initializer

2. **AIFunction properties:**
   - Direct properties: `.Name`, `.Description`
   - NOT `.Metadata.Name` (that's a different MEAI version)

3. **Test-first collaboration:**
   - Tests discovered Trinity's in-progress work (ChatMLFormatter already had tool support)
   - Tests found API mismatches early (before runtime failures)
   - Stubs enable **compilation** → Trinity can run tests as implementation progresses

## Next Steps

1. **Trinity:** Implement JsonToolCallParser.Parse() logic
2. **Trinity:** Add parameter schema to ChatMLFormatter.FormatToolDefinitions
3. **Tank:** Re-run tests when implementation completes, verify all 41 pass
4. **Tank:** Add integration test with real model once Trinity wires up ChatOptions.Tools

## Success Criteria

✅ All 41 tests compile  
✅ 24 tests pass with current stubs/partial implementation  
⏳ 17 tests waiting for Trinity's parser implementation  
⏳ ChatMLFormatter parameter schema (stubbed as empty object)

---

**Recommendation:** Merge decision after Trinity reviews test coverage and approach.


