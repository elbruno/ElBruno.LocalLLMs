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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
