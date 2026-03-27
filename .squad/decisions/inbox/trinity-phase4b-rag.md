# Phase 4b RAG Pipeline Architecture Decisions

**Date:** 2026-03-27  
**Author:** Trinity (Core Developer)  
**Context:** Implementation of RAG (Retrieval-Augmented Generation) infrastructure for ElBruno.LocalLLMs

## Decision Summary

Implemented complete RAG pipeline as a separate NuGet package (`ElBruno.LocalLLMs.Rag`) with pluggable embedding layer, two storage backends (in-memory and SQLite), and comprehensive test coverage.

---

## Decision 1: Separate RAG Package

**Decision:** Create `ElBruno.LocalLLMs.Rag` as a separate NuGet package, not integrated into core library.

**Rationale:**
- RAG is optional functionality — not all LLM apps need retrieval
- Keeps core library lightweight and focused on chat completions
- Allows independent versioning and release cycles
- Users can choose to add RAG without increasing baseline dependencies
- Follows single-responsibility principle

**Consequences:**
- Users must explicitly add `ElBruno.LocalLLMs.Rag` package reference
- Separate versioning requires coordination for breaking changes
- Clean separation of concerns improves testability

---

## Decision 2: IEmbeddingGenerator Abstraction

**Decision:** RAG pipeline depends on MEAI `IEmbeddingGenerator<string, Embedding<float>>` interface, not a concrete implementation.

**Rationale:**
- Allows users to plug in any embedding provider (ElBruno.LocalEmbeddings, OpenAI, Azure, etc.)
- No hard dependency on ONNX Runtime or specific embedding models
- Maximizes flexibility — users choose embedding strategy
- Follows dependency inversion principle

**Consequences:**
- RAG package has no opinion on embedding implementation
- Sample uses mock embeddings for demonstration
- Users must provide their own embedding generator at runtime
- DI extension accepts embedding generator as parameter

---

## Decision 3: Character-Level Chunking

**Decision:** `SlidingWindowChunker` operates on character offsets, not sentences/paragraphs/tokens.

**Rationale:**
- Simple, predictable, language-agnostic algorithm
- No NLP dependencies (sentence detection, tokenization)
- Configurable chunk size and overlap in characters
- Works for any text regardless of language or structure

**Consequences:**
- May split mid-word or mid-sentence (acceptable for embedding models)
- User controls chunk size to balance context vs. granularity
- Alternative chunkers (semantic, token-based) can be added as implementations of `IDocumentChunker`

---

## Decision 4: Two Storage Backends

**Decision:** Provide both `InMemoryDocumentStore` (default) and `SqliteDocumentStore` (opt-in).

**Rationale:**
- In-memory: Fast, zero setup, perfect for demos/prototypes/tests
- SQLite: Persistent, survives restarts, suitable for production
- Both implement same `IDocumentStore` interface
- DI extension makes switching trivial

**Consequences:**
- In-memory store lost on process restart
- SQLite adds dependency on `Microsoft.Data.Sqlite` package
- Both stores implement cosine similarity (consistency)

---

## Decision 5: Cosine Similarity in Application Code

**Decision:** Cosine similarity computed in C# code, not delegated to external library or database.

**Rationale:**
- Simple mathematical operation (dot product / norms)
- No external dependencies required
- Works identically in both in-memory and SQLite stores
- Fast enough for small-to-medium document collections

**Consequences:**
- Linear scan performance (O(n) for n chunks)
- Suitable for < 10k chunks; larger collections may need vector DB (Qdrant, Milvus)
- Consistent behavior across storage backends

---

## Decision 6: Immutable Records for Data Models

**Decision:** All data types (`Document`, `DocumentChunk`, `RagContext`, `RagIndexProgress`) are immutable `sealed record` types.

**Rationale:**
- Records provide value equality, ToString, deconstruction
- Immutability prevents accidental mutation bugs
- Thread-safe by design
- Clean, concise syntax

**Consequences:**
- Cannot modify chunks after creation (must create new instances)
- IDictionary<string, object> for metadata allows flexible extension without breaking changes

---

## Decision 7: Progress Reporting via IProgress<T>

**Decision:** Indexing progress reported via standard .NET `IProgress<RagIndexProgress>` pattern.

**Rationale:**
- Standard .NET pattern for async progress
- Opt-in (pass null to disable)
- Works with UI frameworks (IProgress marshals to UI thread)
- Simple record type tracks processed/total

**Consequences:**
- Users can wire up progress UI, logging, or ignore it
- No dependencies on specific logging framework

---

## Decision 8: Formatter Tool Support Pattern

**Decision:** All 6 chat formatters enhanced with tool support using consistent pattern from `ChatMLFormatter`.

**Rationale:**
- Tool calling is format-specific (different tokens, structures)
- ChatMLFormatter established canonical pattern
- Each format adapts pattern to its token structure
- Tool schemas injected into system message, calls/results formatted per-format

**Consequences:**
- All formatters now support tools parameter
- Tool support backward compatible (tools=null falls back to original behavior)
- Each format has format-specific tool call representation

---

## Impact

- **New projects:** 3 (Rag library, Rag tests, RagChatbot sample)
- **Files created:** 33 new files across library, tests, sample
- **Files modified:** 7 (6 formatters + solution file)
- **Test coverage:** 25 new tests (100% passing)
- **Solution size:** 13 projects total
- **Breaking changes:** None (all additive)

---

## Approval

This decision record documents implementation choices made during Phase 4b RAG pipeline development. All code complete, tested, and integrated into solution.
