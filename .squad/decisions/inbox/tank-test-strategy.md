# Test Strategy Decisions — Tank

**Date:** 2026-03-17  
**Author:** Tank (Tester/QA)  
**Scope:** Unit and integration test strategy for ElBruno.LocalLLMs

---

## Decision 1: NSubstitute over Moq for Mocking

**Context:** Architecture doc Section 9 says "xUnit + Moq (or NSubstitute)".

**Decision:** Use NSubstitute. Already in the .csproj from Switch's scaffolding.

**Rationale:** NSubstitute has cleaner syntax, no `Setup()`/`Verify()` ceremony. Fits the project's "minimal noise" philosophy. The internal `LocalChatClient(options, downloader)` constructor enables clean injection of mocked `IModelDownloader`.

---

## Decision 2: Exact String Tests for Chat Templates

**Context:** Chat template output is the most critical correctness surface — wrong tokens = broken model inference.

**Decision:** All five template formatters (ChatML, Phi3, Llama3, Qwen, Mistral) have tests asserting exact output strings for system+user, user-only, multi-turn, and edge cases. Additionally, negative assertions verify no cross-contamination of template tokens.

**Rationale:** Fuzzy "contains" assertions miss subtle bugs (wrong newline counts, missing end tokens). Exact string equality catches regressions immediately.

---

## Decision 3: Integration Tests Gated by Environment Variable

**Context:** Integration tests require real model downloads (GB-scale) and GPU/CPU inference time.

**Decision:** All integration tests use `[Trait("Category", "Integration")]` AND check `RUN_INTEGRATION_TESTS=true` env var at runtime. Tests throw `SkipException` if not enabled.

**Rationale:** CI unit test jobs should never accidentally trigger multi-GB downloads. Dual gating (xUnit trait + runtime env check) provides defense in depth.

---

## Decision 4: MEAI v10 API Names in Tests

**Context:** MEAI v10.4.0 renamed the core IChatClient methods: `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`, etc.

**Decision:** All tests use the v10 API names. Architecture doc examples (which used old names) are treated as aspirational, not literal.

**Rationale:** Tests must compile and pass against the actual code. The source was updated to v10 API by Trinity.

---

## For Team Review

These decisions align with architecture doc Section 9. Flag any concerns.
