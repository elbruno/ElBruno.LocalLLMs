# MagenticUI .NET — Architecture & Setup Guide

> **Phase 3A** — Local multi-agent web application powered by [ElBruno.LocalLLMs](../README.md) and a .NET port of [microsoft/magentic-ui](https://github.com/microsoft/magentic-ui).

---

## Architecture

```
Browser (React + SignalR)
    │  SignalR /hubs/agent
    ▼
MagenticUIServer  (ASP.NET Core 8)
    AgentHub.cs              ← SignalR hub (SubmitTask / CancelTask)
    AgentSessionService.cs   ← session lifecycle + CancellationToken
    │
    │  ProjectReference
    ▼
MagenticUIServer.Agents  (class library, no ASP.NET dep)
    MagenticUIOrchestrator   ← OmniAgent round-based loop
    ├── FileSurferAgent       ← sandboxed file read/write/list
    ├── WebFetcherAgent       ← HTTP fetch + HTML→Markdown
    ├── UserProxyAgent        ← auto-respond stub (Phase 3A)
    └── CoderAgentStub        ← Phase 3B (WSL2/QEMU)
    │
    │  ProjectReference
    ▼
ElBruno.LocalLLMs
    LocalChatClient (IChatClient)  ← MagenticBrain / Qwen3
```

### Agent Topology

| Agent | Role | Tools | Status |
|---|---|---|---|
| **MagenticBrain** | Orchestrator (Qwen3 template) | — | ✅ Phase 3A |
| **FileSurfer** | File operations | read, write, list (sandboxed) | ✅ Phase 3A |
| **WebFetcher** | Web research | HTTP fetch + Markdown conversion | ✅ Phase 3A |
| **UserProxy** | Human-in-the-loop | auto-respond stub | ⚠️ Stub (Phase 3B) |
| **Coder** | Code execution | sandbox stub | ⚠️ Stub (Phase 3B) |

### SignalR Hub Contract

**Client → Server:**
| Method | Payload | Description |
|---|---|---|
| `SubmitTask` | `{ taskId, prompt, workingDirectory? }` | Start a new task |
| `CancelTask` | `taskId: string` | Cancel running task |

**Server → Client:**
| Event | Payload | Description |
|---|---|---|
| `AgentMessage` | `{ agentName, role, text, round }` | Agent turn output |
| `ToolEvent` | `{ toolName, args, result, agentName }` | Tool invocation |
| `TokenStream` | `(agentName, token)` | Streaming token |
| `TaskComplete` | `(taskId, finalAnswer)` | Task finished |
| `TaskError` | `(taskId, error)` | Task failed/cancelled |

---

## Prerequisites

- .NET 8 SDK
- Node.js 20+ (for React ClientApp)
- A converted Qwen3-14B ONNX model (see [onnx-conversion guide](onnx-conversion-fara.md) for methodology; use `onnx-community/Qwen3-14B-ONNX` for MagenticBrain)

---

## Building

```bash
# Restore + build the agents library
dotnet restore src/samples/MagenticUIServer/MagenticUIServer.Agents/MagenticUIServer.Agents.csproj
dotnet build src/samples/MagenticUIServer/MagenticUIServer.Agents/MagenticUIServer.Agents.csproj

# Run all Phase 3 tests
dotnet test src/tests/MagenticUIServer.Agents.Tests/MagenticUIServer.Agents.Tests.csproj --framework net8.0

# Build the web host
dotnet build src/samples/MagenticUIServer/MagenticUIServer/MagenticUIServer.csproj

# Install React dependencies
cd src/samples/MagenticUIServer/MagenticUIServer/ClientApp
npm install
```

---

## Running

```bash
# Terminal 1 — React dev server (hot reload)
cd src/samples/MagenticUIServer/MagenticUIServer/ClientApp
npm run dev   # starts on http://localhost:5173

# Terminal 2 — ASP.NET Core host
cd src/samples/MagenticUIServer/MagenticUIServer
dotnet run    # starts on https://localhost:7070
```

Open `https://localhost:7070` — the React SPA is served automatically in production; in dev mode the SPA proxy forwards to Vite.

---

## Configuration

`appsettings.json` (or environment variables):

```json
{
  "MagenticUI": {
    "DefaultWorkingDirectory": ".",
    "MaxRounds": 15,
    "ModelPath": "C:\\path\\to\\qwen3-14b-onnx"
  }
}
```

| Key | Default | Description |
|---|---|---|
| `ModelPath` | `""` | Path to the converted Qwen3/MagenticBrain ONNX directory |
| `MaxRounds` | `15` | Maximum orchestrator rounds before auto-termination |
| `DefaultWorkingDirectory` | `"."` | Sandbox root for FileSurfer |

---

## NuGet Packages (MagenticUIServer.Agents)

| Package | Version | Purpose |
|---|---|---|
| `ElBruno.LocalLLMs` | (ProjectRef) | LocalChatClient, IChatClient |
| `Microsoft.SemanticKernel` | 1.78.0 | Kernel infrastructure |
| `Microsoft.SemanticKernel.Agents.Core` | 1.78.0 | Agent types |
| `ElBruno.MarkItDotNet` | 0.9.1 | HTML/PDF → Markdown |
| `Microsoft.Extensions.Http` | 8.x | HttpClient for WebFetchTool |

---

## Phase Roadmap

| Phase | Scope | Status |
|---|---|---|
| **3A** | Core orchestration loop, FileSurfer, WebFetcher, SignalR hub, React SPA | ✅ Done |
| **3B** | Human-in-the-loop (UserProxy real pausing via `TaskCompletionSource`), WSL2 code executor | 🔲 Planned |
| **3C** | QEMU sandbox isolation, full magentic-ui React fork (WebSurfer screenshots, approval UI) | 🔲 Planned |
| **3D** | Docker packaging, multi-model support, session persistence | 🔲 Planned |

---

## Known Limitations (Phase 3A)

- `CodeExecutorTool` is a **stub** — code execution requires Phase 3B WSL2 bridge
- `UserProxyAgent` **auto-responds** — real human-in-the-loop pausing is Phase 3B
- React ClientApp is a **minimal SPA** — full magentic-ui UI (browser screenshots, approvals) is Phase 3C
- `Microsoft.SemanticKernel.Agents.Magentic` (MagenticOne orchestrator) is **preview-only** — Phase 3A uses the proven manual OmniAgent loop from `MagenticBrainAgent`
