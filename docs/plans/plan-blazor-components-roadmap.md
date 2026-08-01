# BlazorComponents Roadmap

Planned additional components for `ElBruno.LocalLLMs.BlazorComponents`,
ordered by developer impact. See also the companion issue on
[ElBruno.LocalEmbeddings](https://github.com/elbruno/ElBruno.LocalEmbeddings/issues)
for the embeddings-specific component work.

---

## Phase 1 — High impact (next release)

### `<VisionChatBox>`

**Why first:** Fara1.5-9B is already in the library; there is zero UI for it.
A vision chat box immediately differentiates the package.

**What it does:**
- Extends `ChatBox` with an image drop zone (drag-and-drop + file picker)
- Previews the selected image inline
- Sends image path + text to `LocalVisionChatClient` via `VisionChatOptions`
- Streams the response token-by-token exactly like `ChatBox`
- Shows "Vision model required" guard when a non-vision model is selected

**API sketch:**
```razor
<VisionChatBox Client="@visionClient"
               ModelName="Fara 1.5-9B"
               MaxOutputTokens="256"
               ShowMetrics="true" />
```

**Key parameter:** `Client` accepts `IChatClient` (same as `ChatBox`) — the
component checks `LocalVisionChatClient` internally and shows a warning if
the client doesn't support images.

---

### `<InferenceMetricsPanel>`

**Why:** Developers tuning prompts or comparing hardware configs need a
persistent perf dashboard. Currently metrics only appear in `ChatBox`.

**What it does:**
- Subscribes to `ILocalLLMsDiagnostics` (already registered by `AddLocalLLMs`)
- Shows: time-to-first-token, total generation time, tokens/sec, prompt
  tokens, completion tokens
- Sparkline chart of tok/s across the last 10 generations
- Compact / expanded toggle

**API sketch:**
```razor
<InferenceMetricsPanel />          <!-- reads from DI automatically -->
<InferenceMetricsPanel Compact="true" />
```

---

### `<DownloadQueueManager>`

**Why:** `ModelStatusCard` handles one model at a time. Developers
pre-warming multiple models need a queue view.

**What it does:**
- Accepts a list of `ModelDefinition` items
- Shows each as a row: name, status dot, progress bar
- Parallel download up to N models (configurable, default 1)
- Aggregate footer: total remaining size, estimated time
- "Download All" / "Cancel All" buttons

**API sketch:**
```razor
<DownloadQueueManager Models="@myModels"
                      MaxParallel="2"
                      OnAllComplete="HandleReady" />
```

---

## Phase 2 — Developer tools

### `<PromptBuilder>`

**What it does:**
- System-prompt textarea with token count estimate
- Few-shot example pairs (add/remove rows)
- One-click "Send to ChatBox" integration
- Exports the resulting `ChatMessage[]` via `@bind-Messages`

**API sketch:**
```razor
<PromptBuilder @bind-Messages="messages"
               OnSend="SendToChat" />
```

---

### `<ConversationHistory>`

**What it does:**
- Read-only, shareable view of `ChatMessage[]`
- Role-coloured bubbles (user = right, assistant = left)
- Copy-to-clipboard per message
- "Export as JSON" and "Export as Markdown" buttons
- Plugs into `ChatBox` via a shared `@bind-Messages`

**API sketch:**
```razor
<ChatBox @bind-Messages="messages" Client="@client" />
<ConversationHistory Messages="messages" />
```

---

### `<ModelCompareView>`

**What it does:**
- Two side-by-side `ChatBox` panels, same prompt sent to both simultaneously
- Shows which model responded first
- Optional: configurable eval metric (e.g. response length, latency)

**API sketch:**
```razor
<ModelCompareView ClientA="@phi35" ClientB="@qwen25"
                  ModelNameA="Phi-3.5 mini" ModelNameB="Qwen2.5-0.5B" />
```

---

### `<ToolCallInspector>`

**What it does:**
- Renders structured tool-call / tool-result turns from `ChatMessage[]`
- Collapsible JSON viewer for arguments and results
- Highlights round number, tool name, duration
- Useful for debugging `SupportsToolCalling` models (Qwen3, MagenticBrain)

**API sketch:**
```razor
<ChatBox @bind-Messages="messages" Client="@agentClient" />
<ToolCallInspector Messages="messages" />
```

---

## Phase 3 — Niche / advanced

### `<EmbeddingExplorer>`

Requires `ElBruno.LocalEmbeddings` package reference (optional peer dependency).

- Enter 2–10 sentences
- Generate embeddings via `IEmbeddingGenerator`
- Display cosine-similarity heatmap (colour-coded matrix)
- Sort sentences by similarity cluster

---

### `<ModelRecommender>`

- Wizard-style questionnaire (task type, hardware tier, latency budget)
- Outputs the recommended `ModelDefinition` from `KnownModels`
- Pure UI logic, no inference required

---

### `<LocalLLMsStatusPage>`

- A full `/status` page component
- All `KnownModels` with cache state, size, last-used timestamp
- Environment providers section
- Last-inference metrics summary
- Printable / exportable as HTML

---

## Implementation conventions (for all phases)

All new components must follow the patterns in `ElBruno.LocalLLMs.BlazorComponents`:

1. Paired `.razor.css` for component-scoped styles
2. `IDisposable` + unsubscribe from `ModelStateService.OnStateChanged`
3. `InvokeAsync(StateHasChanged)` for all cross-thread updates
4. Parameters use `EventCallback<T>` not `Action<T>`
5. Target `net8.0` only (no multi-targeting for Blazor components)
6. Add to `BlazorDemo` sample app with a dedicated page
7. Add parameter table to `docs/blazor-components.md`
8. Bump version and update `publish.yml` pack step

---

## Release plan

| Version | Components |
|---------|-----------|
| v0.20.8 | `VisionChatBox`, `InferenceMetricsPanel`, `DownloadQueueManager` |
| v0.21.0 | `PromptBuilder`, `ConversationHistory`, `ModelCompareView` |
| v0.21.x | `ToolCallInspector`, `EmbeddingExplorer` (with peer dep), `ModelRecommender` |
| future  | `LocalLLMsStatusPage` |
