# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-17 — Architecture Design Complete

**Architecture decisions made:**
- Single `ElBruno.LocalLLMs` core package (not per-model). Models are data (`ModelDefinition` records in `KnownModels`), not code.
- `LocalChatClient` implements `IChatClient` from `Microsoft.Extensions.AI.Abstractions`.
- Uses `Microsoft.ML.OnnxRuntimeGenAI` (not raw ORT) — GenAI handles tokenization, KV cache, sampling.
- Sync constructor + async `CreateAsync()` factory — proven pattern from LocalEmbeddings.
- Chat templates are internal strategy pattern (`IChatTemplateFormatter`), resolved automatically from `ChatTemplateFormat` enum.
- Multi-target `net8.0;net10.0`, matching reference repos.
- Phase 1 MVP targets Phi-3.5-mini-instruct only (native ONNX, zero conversion friction).

**Reference repo patterns discovered:**
- `elbruno.localembeddings`: `LocalEmbeddingGenerator` implements `IEmbeddingGenerator<string, Embedding<float>>`. Options class + `CreateAsync()` factory. `ModelDownloader` wraps `ElBruno.HuggingFace.Downloader`. DI via `AddLocalEmbeddings()`. `.slnx` solution format. `Directory.Build.props` with warnings-as-errors.
- `ElBruno.QwenTTS`: `.Core` package has pipeline + options. `TtsPipeline.CreateAsync()` factory. `QwenTtsServiceExtensions.AddQwenTts()` for DI. Python scripts in `/python` for ONNX conversion. E2E GPU integration tests in `/tests`.

**Key file paths:**
- Architecture doc: `docs/architecture.md`
- Decisions: `.squad/decisions/inbox/morpheus-architecture.md`
- Core source will be: `src/ElBruno.LocalLLMs/`
- Main class: `src/ElBruno.LocalLLMs/LocalChatClient.cs`
- Options: `src/ElBruno.LocalLLMs/LocalLLMsOptions.cs`
- Model registry: `src/ElBruno.LocalLLMs/Models/KnownModels.cs`

**User preferences:**
- Bruno uses `ElBruno.HuggingFace.Downloader` v0.5.0 for all model downloads
- Both reference repos use `.slnx` (new solution format), not `.sln`
- Both reference repos target `net8.0;net10.0`
- NuGet metadata includes icon, README, MIT license in all projects
- `InternalsVisibleTo` test projects is standard practice

### 2026-03-18 — Comprehensive Documentation Complete

**Documentation created:**
- `docs/getting-started.md` — 15KB user guide covering:
  - Prerequisites, installation, quick start (5-line example)
  - Model tier explanation (Tiny/Small/Medium/Large/Next-Gen) with use cases
  - Streaming responses with full example
  - Using different models via `KnownModels`
  - Custom model paths (skip re-download)
  - Dependency injection for ASP.NET Core
  - GPU acceleration (CUDA, DirectML)
  - Complete `LocalLLMsOptions` reference table
  - ONNX conversion workflow
  - Troubleshooting section (model not found, OOM, slow first run, gibberish output)

- `docs/supported-models.md` — 15KB model reference:
  - Complete table of all 29 models with Params, Tier, HF ID, ONNX status, Chat template, RAM
  - Detailed tier sections (⚪ Tiny through 🟣 Next-Gen) with realistic output examples
  - Chat template format explanations
  - ONNX conversion guide (native vs. requires conversion)
  - Custom model creation example
  - Performance comparison table (tokens/sec on RTX 4080)
  - Decision tree for choosing the right model

- `CONTRIBUTING.md` — 14KB contributor guide:
  - Quick start (build, test)
  - Project structure explanation
  - Code style rules (based on `.editorconfig`)
  - Step-by-step: Adding a new model (ModelDefinition, tests, PR)
  - ONNX conversion prerequisites and walkthrough
  - Architecture overview with layered design diagram
  - Testing strategy (unit vs. integration, gated by env var)
  - Debugging tips and common issues
  - CI/CD pipeline overview
  - PR guidelines and commit message format

- `CHANGELOG.md` — Release notes:
  - `[0.1.0] - 2026-03-18` initial release documented
  - All features, models, GPU providers, chat templates listed
  - Technical details (dependencies, versions, design patterns)
  - 210 unit tests, 17 integration tests noted
  - 4 samples documented
  - Known limitations and planned features for future releases

**Key documentation decisions:**
- Getting Started is user-focused (no architecture jargon until troubleshooting)
- Supported Models is reference-focused (decision trees, performance data, tier explanations)
- Contributing is for both beginners and experienced developers (clear step-by-step)
- CHANGELOG follows Keep a Changelog standard (for transparency and release tracking)
- All docs use examples from actual sample code (`HelloChat`, `StreamingChat`, `DependencyInjection`)
- All docs reference MEAI 10.4.0 API names (`GetResponseAsync`, `ChatResponse`, not old names)
- Model table includes all 29 models from team.md for completeness
- Troubleshooting covers real issues users will hit (download fails, OOM, slow first run)

**Impact on users:**
- New users can go from 0 to working example in <5 minutes
- Experienced users have a complete model reference for selection
- Contributors have clear onboarding path to add new models
- Release transparency via CHANGELOG for early adopters

### 2026-03-18 — Phase 4 Architecture: RAG + Tool Routing

**Architecture decisions made:**

**Tool Calling (Phase 4a):**
- Prompt-based tool calling via `IChatTemplateFormatter` extensions (no native ONNX support yet)
- Each formatter implements `FormatMessagesWithTools()` and `ParseToolCalls()` for model-specific formats
- Three tool calling formats supported: QwenHermes (`<tool_call>` tags), Llama3Json (JSON objects), Phi4Functools (`functools[...]`)
- Tool capability flags added to `ModelDefinition`: `SupportsToolCalling` and `ToolCallingFormat`
- Tool calls return `FunctionCallContent` in `ChatMessage.Contents` (multi-content messages)
- Generated `CallId` for models that don't provide one (most open models)
- Non-streaming only in Phase 4a (streaming tool calls deferred to future)
- Initial model support: Qwen2.5-3B/7B-Instruct, Llama-3.2-3B-Instruct, Phi-4

**RAG Pipeline (Phase 4b):**
- Extension package: `ElBruno.LocalLLMs.Rag` (separate from core library)
- Core abstractions: `IDocumentChunker`, `IDocumentStore`, `IRagPipeline`
- Default implementations: `SlidingWindowChunker` (fixed-size with overlap), `InMemoryDocumentStore`, `SqliteDocumentStore`
- `RagContext` includes both raw chunks and formatted context string for easy prompt injection
- Integrates with `ElBruno.LocalEmbeddings` via `IEmbeddingGenerator<string, Embedding<float>>`
- RAG and tool calling are independent features — integration happens at application level
- RAG can be used as a tool (`SearchDocumentation(query)`) or as context injection

**Integration pattern:**
- Tool calling is a `ChatOptions` feature (`Tools`, `ToolMode`, `AllowMultipleToolCalls`)
- RAG is a retrieval pipeline (index → embed → store → query → format → inject)
- Users control how they combine (RAG-only, tools-only, RAG-as-tool, RAG+tools)

**Key file paths:**
- Architecture plan: `docs/plan-rag-tool-routing.md`
- Decisions: `.squad/decisions.md` (merged 2026-03-27)

### 2026-03-27 — Phase 4a Execution Complete

**Phase 4a (Tool Calling) delivered on schedule:**
- Trinity implemented ToolCalling namespace with JsonToolCallParser (handles 3 formats)
- Tank delivered 41 comprehensive unit tests (all passing)
- Morpheus documented 11 architectural decisions (deduped into decisions.md)
- Team verified backward compatibility (existing code unaffected)
- 359/359 tests passing across parser, formatter, and integration layers

**Execution notes:**
- Proactive testing pattern (Tank's stubs enabled Trinity's implementation)
- Tight feedback loop discovered API quirks early (MEAI v10.4.0)
- Coordination resolved conflicts (Tank's test stubs vs Trinity's implementation via Coordinator)
- All decisions merged into canonical decisions.md

**Readiness for Phase 4b (RAG pipeline):**
- Architecture plan fully specified in docs/plan-rag-tool-routing.md
- Trinity ready to implement RAG pipeline following same pattern
- Tank ready for integration tests with real ONNX models
- Morpheus orchestration complete; team converged on working Phase 4a
- Tool formatters will be in: `src/ElBruno.LocalLLMs/Templates/` (extended)
- RAG package: `src/ElBruno.LocalLLMs.Rag/`
- Samples: `samples/ToolCallingAgent/`, `samples/RagChatbot/`

**User preferences inferred:**
- Bruno wants agentic capabilities (tool calling enables actions)
- Bruno wants RAG support (grounding in private data, per ElBruno.LocalEmbeddings integration)
- Bruno prefers extension packages over bloated core (follows LocalEmbeddings pattern)
- Bruno values flexibility (pluggable stores, user-controlled integration patterns)

**Technical insights:**
- MEAI 10.4.0 `IChatClient` fully supports tool calling via `ChatOptions.Tools` → `FunctionCallContent` → `FunctionResultContent` flow
- Open models use incompatible tool formats (Qwen XML tags ≠ Llama JSON ≠ Phi4 functools)
- Most open models don't generate call IDs (unlike OpenAI API) — must generate them
- Streaming tool calls requires buffering partial JSON/XML — complex, defer to post-MVP
- ElBruno.LocalEmbeddings uses `IEmbeddingGenerator<string, Embedding<float>>` — direct integration point for RAG
- SQLite can work for 10K-100K docs with brute-force cosine similarity, but larger scale needs vector DB (Qdrant, Milvus)
- Sliding window chunking is simple/fast, semantic chunking is future enhancement

**Impact on library:**
- Unlocks agentic use cases (tool calling = LLM can take actions)
- Unlocks enterprise use cases (RAG = grounding in private knowledge)
- Maintains architectural consistency (extensions by concern, not by model)
- Positions ElBruno.LocalLLMs competitively with Ollama, LM Studio, vLLM
