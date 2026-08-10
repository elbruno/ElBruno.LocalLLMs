# Blazor Components Guide

`ElBruno.LocalLLMs.BlazorComponents` is a Razor Class Library that provides
ready-to-use Blazor components for building local-LLM–powered web apps.

## Installation

```bash
dotnet add package ElBruno.LocalLLMs.BlazorComponents
```

## Setup

Register services in your `Program.cs`:

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BlazorComponents.Extensions;

// 1. Register core IChatClient
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.EnsureModelDownloaded = true;
});

// 2. Register BlazorComponents services (IModelDownloader + ModelStateService)
builder.Services.AddLocalLLMsBlazorComponents();
```

Add the `@using` directive in `_Imports.razor`:

```razor
@using ElBruno.LocalLLMs.BlazorComponents.Components
```

---

## Components

### ModelStatusCard

Shows download state (Not Downloaded / Downloading / Downloaded), a progress bar
during active downloads, and action buttons.

```razor
<ModelStatusCard Model="KnownModels.Phi35MiniInstruct"
                 OnModelSelected="HandleModelSelected" />

@code {
    void HandleModelSelected(ModelDefinition model)
    {
        // user clicked "Use" — activate this model
    }
}
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Model` | `ModelDefinition` | — | The model to represent (**required**) |
| `OnModelSelected` | `EventCallback<ModelDefinition>` | — | Fires when the user clicks **Use** |

**Actions shown**

| State | Buttons |
|-------|---------|
| Not Downloaded | ⬇ Download |
| Downloading | ✕ Cancel, progress bar |
| Downloaded | 📂 Open Folder, 🗑 Delete, ▶ Use |
| Error | ⬇ Retry |

---

### ModelGallery

A filterable grid of all `KnownModels`, each rendered as a `ModelStatusCard`.

```razor
<ModelGallery Models="KnownModels.All"
              OnModelSelected="HandleModelSelected" />
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Models` | `IReadOnlyList<ModelDefinition>` | `KnownModels.All` | Model list to display |
| `OnModelSelected` | `EventCallback<ModelDefinition>` | — | Fires when a model is selected |

**Built-in filters:** Tier (XSmall / Small / Medium / Large), Vision-only,
Tool-calling, Downloaded-only.

---

### ModelSelector

A `<select>` dropdown bound to a `ModelDefinition`, grouped by `ModelTier`.

```razor
<ModelSelector @bind-Value="activeModel" />

@code {
    ModelDefinition? activeModel = KnownModels.Phi35MiniInstruct;
}
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Value` | `ModelDefinition?` | — | Currently selected model |
| `ValueChanged` | `EventCallback<ModelDefinition?>` | — | Two-way binding |
| `Models` | `IReadOnlyList<ModelDefinition>` | `KnownModels.All` | Models to list |
| `Placeholder` | `string` | `"Select a model…"` | Shown when no model selected |

---

### ChatBox

A fully functional streaming chat UI backed by any `IChatClient`.

```razor
<ChatBox Client="@myChatClient"
         ModelName="Phi-3.5-mini"
         SystemPrompt="You are a helpful assistant."
         ShowMetrics="true" />
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Client` | `IChatClient` | — | Chat client (**required**) |
| `ModelName` | `string?` | `null` | Shown in the chat header |
| `SystemPrompt` | `string?` | `null` | Prepended as `ChatRole.System` |
| `MaxOutputTokens` | `int?` | `null` | Passed to `ChatOptions` |
| `ShowMetrics` | `bool` | `true` | Display tok/s footer |
| `Placeholder` | `string` | `"Type a message…"` | Input placeholder |

**Features**
- Token-by-token streaming with animated cursor
- Live tokens-per-second metric
- Stop button cancels in-flight generation
- Automatic scroll-to-bottom
- Multi-turn conversation history

---

### EnvironmentDashboard

Shows execution providers, .NET version, OS, and cache information.

```razor
<!-- Full dashboard (default) -->
<EnvironmentDashboard />

<!-- Compact mode for nav-bars / sidebars -->
<EnvironmentDashboard Compact="true" />
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Client` | `LocalChatClient?` | `null` | Optional — used for cache-size info |
| `Compact` | `bool` | `false` | Single-line badge layout |
| `ShowCacheInfo` | `bool` | `true` | Show cache path and size section |

---

### LocalLLMHealthBadge

A compact, colour-coded status indicator designed for top navigation bars.

```razor
<!-- Static (check once on render) -->
<LocalLLMHealthBadge />

<!-- Auto-refresh every 30 s -->
<LocalLLMHealthBadge PollIntervalSeconds="30" />
```

**Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `PollIntervalSeconds` | `int` | `0` | `0` = no polling |
| `ShowLabel` | `bool` | `true` | Show text label next to dot |

**States:** 🟢 Ready · 🔴 Not available · ⚫ Checking

---

### RagPlayground

An interactive document indexer and retrieval tester backed by `LocalRagPipeline`.

```razor
<RagPlayground />
```

No parameters. The component manages its own `LocalRagPipeline` instance.

**Features**
- Add text documents to the index
- Configurable Top-K (1–10) and min-similarity (0–1)
- Shows retrieved chunks ranked by similarity position

---

## Service: ModelStateService

`ModelStateService` is a **singleton** service that tracks download/cache state
for all models across the application. Components use it internally — you
generally don't inject it directly — but it is available if you need to observe
or drive state from custom code. Because it is application-wide, a Blazor Server
circuit disconnect does not cancel an active download.

```csharp
// In a custom component
@inject ModelStateService ModelState

// Listen for state changes
@implements IDisposable

protected override void OnInitialized()
{
    ModelState.OnStateChanged += HandleStateChanged;
}

private void HandleStateChanged()
{
    _ = InvokeAsync(StateHasChanged);
}

public void Dispose()
{
    ModelState.OnStateChanged -= HandleStateChanged;
}
```

**Key members**

| Member | Description |
|--------|-------------|
| `GetStatus(model)` | Returns the current `ModelStatus` for a model |
| `StartDownloadAsync(model)` | Kicks off background download |
| `CancelDownload(model)` | Explicitly cancels the active download for that model |
| `DeleteModelAsync(model)` | Removes cached files |
| `OnStateChanged` | Event raised on every state transition |

---

## Sample App

See [`src/samples/BlazorDemo`](../src/samples/BlazorDemo/) for a complete
Blazor Server app that showcases all seven components with pages for:

- **Home** — `ModelStatusCard` quick view for three models
- **Models** — `ModelGallery` with filters
- **Chat** — `ChatBox` with `ModelSelector`
- **RAG Playground** — `RagPlayground`
- **Environment** — `EnvironmentDashboard`

**Run the sample:**

```bash
dotnet run --project src/samples/BlazorDemo
```

Then open `https://localhost:5001` in your browser.

---

## Dependency Injection reference

`AddLocalLLMsBlazorComponents()` registers:

| Service | Lifetime | Notes |
|---------|----------|-------|
| `IModelDownloader` | Singleton | Shared download manager |
| `ModelStateService` | Singleton | Application-wide state; circuit teardown does not cancel downloads |

Your app must also call `AddLocalLLMs()` (or `AddLocalVisionLLM()`) to register
`IChatClient` before calling `AddLocalLLMsBlazorComponents()`.

---

## Target frameworks

`ElBruno.LocalLLMs.BlazorComponents` targets **net8.0** only (Blazor Server and
Blazor WASM both require ASP.NET Core). It is not compatible with console or
non-web projects.
