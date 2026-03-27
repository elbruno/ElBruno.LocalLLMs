### 2026-03-27T15:58: User directives — RAG tool routing constraints

**By:** Bruno Capuano (via Copilot)

**What:**
1. Target both CPU and GPU — must work on both
2. Goal is to help with many tools (20+), not small catalogs
3. SLM task is tool selection only — no argument generation
4. Latency: as low as possible for local execution
5. Additional models to investigate: Gemma-3-270M (Google nano, native function calling) and TinyAgent-1.1B (Berkeley, specialized tool calling)

**Why:** User request — captured for team memory. These constraints narrow the model selection and architecture decisions for the RAG tool routing plan.
