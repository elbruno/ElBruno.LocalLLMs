# Orchestration Log: Morpheus — Blocked Models Reference Document

**Timestamp:** 2026-03-18T15:00Z  
**Agent:** Morpheus (Lead/Architect)  
**Mode:** background  
**Task:** Write comprehensive `docs/blocked-models.md` reference document

## Outcome

**Result:** Complete blocked models reference document created

### Files Produced

- `docs/blocked-models.md` — Comprehensive reference documenting:
  - 5 blocked models with reasons and status
  - 6 next-generation models with timeline and expectations
  - Architecture support matrix
  - License acceptance workflows
  - Future conversion path recommendations

## Document Structure

**Blocked Models (5 total):**
1. **StableLM-2-Zephyr-1.6B** — Architecture unsupported by builder v0.12.1
2. **Mixtral-8x7B-Instruct-v0.1** — MoE architecture not supported by builder
3. **Command-R (Cohere)** — Gated license, requires acceptance
4. **Llama-3.2-3B-Instruct** — Separate gated license (awaiting Meta review)
5. **Llama-3.3-70B-Instruct** — Separate gated license + likely memory limits at 70B scale

**Next-Generation Models (6 total):**
- **Yi-1.5-9B-Chat**, **Yi-1.5-34B-Chat** — Pending license verification
- **Phi-4-mini**, **Phi-4** — Microsoft releases, pending availability
- **Grok-2-Vision**, **DeepSeek-R1-671B** — Flagship models, conversion methodology pending

## Key Classifications

| Blocker Type | Count | Examples |
|---|---|---|
| Unsupported architecture | 2 | StableLM-2, Mixtral MoE |
| Gated license | 3 | Command-R, Llama-3.2, Llama-3.3 |
| Practical memory limits (70B+) | 1 | DeepSeek-R1-Distill-Llama-70B |

## Cross-Agent Impact

- **Trinity (Core Dev):** Reference for KnownModels.cs exclusion comments
- **Dozer (ML Engineer):** Prioritization guide for next conversion batches post-license approval
- **Squad Lead:** Decision point for supporting alternative conversion paths (e.g., llama.cpp GGUF for 70B models)
