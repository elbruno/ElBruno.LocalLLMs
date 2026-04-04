# Decision: RAG Pipeline Test Strategy

**Date:** 2026-04-04
**Author:** Tank (Tester)
**Status:** Proposed

## Context

The RAG pipeline (`LocalRagPipeline`) needed comprehensive test coverage for both unit and integration scenarios. The existing RAG test project had 25 tests covering low-level components (chunker, store, cosine similarity) but no tests for the pipeline orchestration itself.

## Decision

1. **Unit tests use `SynchronousProgress<T>`** instead of `Progress<T>` for deterministic callback ordering in test assertions.
2. **Mock embedding tests use `minSimilarity: -1.0f`** when guaranteed retrieval is needed, because hash-based random vectors produce unpredictable cosine similarities (can be negative).
3. **Integration tests gated by `RUN_INTEGRATION_TESTS=true`** env var + `[TestCategory("Integration")]` — skipped in CI by default, opt-in for full pipeline validation.
4. **RAG record types tested in xUnit project** (not MSTest) since they're shared types used across the solution.

## Consequences

- Pipeline orchestration (index → retrieve → clear cycle) is now fully covered
- Mock embedding generator pattern is reusable for future RAG-related tests
- Integration tests validate scale (15+ docs) and reindexing without requiring real models
