# Core Implementation Decisions — Trinity

**Date:** 2026-03-17  
**Author:** Trinity (Core Dev)  
**Scope:** Implementation decisions made during core library build

---

## Decision: Lazy Model Initialization

**Context:** Model download + ONNX load is expensive. Should it happen in constructor or on first use?

**Decision:** Lazy initialization on first `CompleteAsync`/`CompleteStreamingAsync` call, with `SemaphoreSlim` for thread safety. Async factory `CreateAsync()` eagerly initializes.

**Rationale:** Constructor stays sync and fast. Lazy init means DI registration doesn't block startup. SemaphoreSlim ensures only one thread does the download/load even under concurrent access.

---

## Decision: TokenizerStream for Streaming

**Context:** For streaming, we can either accumulate tokens and decode the full list each time, or use `TokenizerStream` for incremental decoding.

**Decision:** Use `Tokenizer.CreateStream()` + `TokenizerStream.Decode(tokenId)` for per-token incremental decoding.

**Rationale:** More efficient — avoids re-decoding the full sequence on every token. The GenAI API provides this specifically for streaming use cases.

---

## Decision: Config-based Provider Selection

**Context:** `new Model(path)` defaults to CPU. GPU needs provider configuration.

**Decision:** For CPU, use `new Model(path)` directly. For CUDA/DirectML, use `Config` class to configure providers before model creation.

**Rationale:** Cleanest API path — CPU is the common case and stays simple. GPU configuration is explicit and extensible.
