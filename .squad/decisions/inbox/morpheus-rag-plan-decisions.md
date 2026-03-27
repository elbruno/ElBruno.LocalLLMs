# Decision: RAG Tool Routing Implementation Plan Approved

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
