### 2026-03-28T00:47: User directive
**By:** Bruno Capuano (via Copilot)
**What:** The prompt distillation + tool routing functionality should live in the ElBruno.ModelContextProtocol.MCPToolRouter library, not in ElBruno.LocalLLMs. The current docs and sample in this repo are temporary — they'll be deleted once the feature is implemented in MCPToolRouter.
**Why:** User request — captured for team memory. MCPToolRouter is the correct home for this feature since it already owns tool filtering via embeddings.
