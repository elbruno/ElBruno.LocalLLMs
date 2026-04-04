# Decision: ZeroCloudRag targets net10.0

**Date:** 2026-07-04
**Author:** Trinity (Core Dev)
**Status:** Active

**Context:** Issue #9 requires a zero-cloud RAG sample using `ElBruno.LocalEmbeddings` for real local embeddings. The package (v1.0.1) only targets `net10.0`.

**Decision:** The `ZeroCloudRag` sample targets `net10.0` (not `net8.0` like other samples) because `ElBruno.LocalEmbeddings` 1.0.1 only ships a net10.0 TFM.

**Rationale:**
- The library projects (`ElBruno.LocalLLMs`, `ElBruno.LocalLLMs.Rag`) already multi-target `net8.0;net10.0` so they are compatible.
- The SDK on the dev machine is .NET 10.0.201, so builds work.
- This is the first sample requiring net10.0 — if a net8.0-compatible version of LocalEmbeddings ships in the future, the TFM can be downgraded.

**Impact:** Developers need .NET 10 SDK to build/run this sample. All other samples remain net8.0 compatible.
