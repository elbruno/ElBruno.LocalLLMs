---
updated_at: 2026-07-23T16:38:30-04:00
focus_area: Phase 3A complete — MagenticUIServer (.NET magentic-ui port), Phase 3B (UserProxy HIL, WSL2 coder) and Phase 3C (React fork, QEMU) pending
active_issues: []
---

# What We're Focused On

Phase 3A .NET magentic-ui port delivered. Phase 3B (UserProxy full HIL wiring, WSL2 coder, BrowserSurferAgent, SQLite persistence) and Phase 3C (full React fork, QEMU sandbox, Auth) are next pending phases.

**Phase 3A Deliverables (2026-07-23):**
- ✅ `MagenticUIServer` — ASP.NET Core 8.0 host; SignalR `AgentHub` (8 client→server methods); `AgentSessionService` (`ConcurrentDictionary<string, AgentSession>`)
- ✅ `MagenticUIServer.Agents` — MEAI `MagenticUIOrchestrator` (OmniAgent loop); `FileSurferAgent`, `WebFetcherAgent`, `UserProxyAgent`, `CoderAgentStub`; `FileSurferTool` (sandboxed), `WebFetchTool` (HTML→Markdown via MarkItDotNet), `CodeExecutorTool` (stub), `MarkItDownTool`; 4 model records
- ✅ `MagenticUIServer.Agents.Tests` — 40 tests (FileSurferToolTests 14, WebFetchToolTests 9, CodeExecutorToolTests 6, AgentMessageTests 10), all passing
- ✅ React 19 minimal SPA + `@microsoft/signalr` in `ClientApp/` — `agentHubClient.ts`, placeholder components
- ✅ Architecture ADR (10 decisions + Amendment A1) merged to `.squad/decisions.md` (Decision 35)
- ✅ SK `Agents.Magentic` dropped — confirmed incompatible with `IChatClient` (MEAI); custom OmniAgent loop adopted
- ✅ `ElBruno.MarkItDotNet` v0.9.1 integrated for WebFetcher HTML→Markdown conversion
- ✅ All 3 projects added to `ElBruno.LocalLLMs.slnx`; `dotnet build -p:TargetFrameworks=net8.0` — 0 errors

**Phase 2 VLM Deliverables (2026-07-23):**
- ✅ `IVisionGenerationModel` interface extending `ITextGenerationModel`
- ✅ `OnnxVisionModel` — wraps `Model + MultiModalProcessor` (ORT-GenAI three-stage VLM pipeline)
- ✅ `FaraFormatter` — Qwen VL vision tokens, no tool support
- ✅ `ChatTemplateFormat.Fara` + `OnnxModelType.VisionGenAI` enum values
- ✅ `LocalVisionChatClient : IChatClient, IAsyncDisposable` — `LocalChatClient` unchanged
- ✅ `VisionChatOptions : ChatOptions` with `string[] ImagePaths`
- ✅ `KnownModels.Fara15_9B` — `ModelTier.Medium`, `HasNativeOnnx=false`
- ✅ `AddLocalVisionLLM` service extension
- ✅ `FaraVisionAgent` sample project
- ✅ 37 new unit tests (50 Fara + Qwen3 total passing)
- ✅ `docs/onnx-conversion-fara.md` — practical ONNX conversion guide (17KB)
- ✅ Architecture ADR (14 decisions + Amendment A1) merged to `.squad/decisions.md`

**Prior Phase Deliverables (Qwen3/MagenticBrain Phase 1, 2026-07-23):**
- ✅ `Qwen3Formatter` — standalone sealed, thinking mode, XML tool call support
- ✅ `ChatTemplateFormat.Qwen3`
- ✅ `KnownModels.Qwen3_14BInstruct` + `MagenticBrain`
- ✅ `MagenticBrainAgent` sample
- ✅ 13 Phase 1 unit tests

**Next:** Phase 3B — UserProxy full human-in-the-loop wiring; `CodeExecutorTool` WSL2 subprocess bridge; `BrowserSurferAgent` (Playwright); SQLite session persistence via EF Core.

