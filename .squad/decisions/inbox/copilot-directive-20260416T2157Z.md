### 2026-04-16T21:57Z: User directive
**By:** Bruno Capuano (via Copilot)
**What:** BitNet package should be named `ElBruno.LocalLLMs.BitNet`, not `ElBruno.BitNet`. Even though it uses a completely different runtime (bitnet.cpp instead of ONNX Runtime GenAI), it is still a local LLM and belongs under the LocalLLMs umbrella.
**Why:** User request — captured for team memory. This extends Decision 1 (Single Core Package): extension packages by *concern* are allowed, and BitNet is a concern-level extension.
