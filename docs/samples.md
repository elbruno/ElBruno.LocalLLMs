# Samples Guide

Runnable examples demonstrating how to use `ElBruno.LocalLLMs` in different scenarios. Each sample is a standalone .NET project you can build and run directly.

## Prerequisites

- .NET 8.0+ SDK
- ~2-4 GB free disk space (models are downloaded on first run)
- CPU is sufficient; GPU (CUDA/DirectML) is optional

---

## HelloChat — Minimal Console Chat

**What it demonstrates:** The simplest possible usage — create a client and ask a question.

**Run it:**

```bash
dotnet run --project samples/HelloChat
```

**Key code:**

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Create a local chat client (downloads Phi-3.5 mini on first run)
using var client = await LocalChatClient.CreateAsync();

var response = await client.GetResponseAsync([
    new(ChatRole.User, "What is the capital of France?")
]);

Console.WriteLine(response.Text);
```

**What happens:**

1. `CreateAsync()` downloads the default model (Phi-3.5 mini) from HuggingFace on first run
2. Sends a single user message through the ONNX Runtime GenAI inference pipeline
3. Prints the complete response

**Expected output:**

```
The capital of France is Paris. Paris is the largest city in France and serves as
the country's political, economic, and cultural center.
```

> **Note:** First run takes longer due to the model download (~2.4 GB). Subsequent runs start much faster.

---

## StreamingChat — Token-by-Token Streaming

**What it demonstrates:** Real-time token streaming — see the response appear word by word as it's generated.

**Run it:**

```bash
dotnet run --project samples/StreamingChat
```

**Key code:**

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

Console.WriteLine("Streaming response:");
await foreach (var update in client.GetStreamingResponseAsync([
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Explain quantum computing in simple terms.")
]))
{
    Console.Write(update.Text);
}
Console.WriteLine();
```

**What happens:**

1. Creates a client with an explicit model selection (`Phi35MiniInstruct`)
2. Sends a system prompt and user message
3. Uses `await foreach` over `GetStreamingResponseAsync` to receive `ChatResponseUpdate` objects
4. Each update contains one or more tokens — printed immediately with `Console.Write`

**Expected output:**

```
Streaming response:
Quantum computing is a type of computing that uses quantum bits, or qubits, instead
of classical bits. While a classical bit can be either 0 or 1, a qubit can be both
0 and 1 at the same time — a property called superposition...
```

> **Tip:** Streaming is ideal for chatbot UIs where users expect to see the response progressively.

---

## MultiModelChat — Switch Models at Runtime

**What it demonstrates:** Using different models for the same question — compare model outputs side by side.

**Run it:**

```bash
dotnet run --project samples/MultiModelChat
```

**Key code:**

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var question = new ChatMessage(ChatRole.User, "What is machine learning? Answer in one sentence.");

// Try with Phi-3.5 mini
Console.WriteLine("=== Phi-3.5 mini ===");
using (var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
}))
{
    var response = await client.GetResponseAsync([question]);
    Console.WriteLine(response.Text);
}

// Try with Qwen 2.5 0.5B (tiny model)
Console.WriteLine("\n=== Qwen 2.5 0.5B ===");
using (var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct
}))
{
    var response = await client.GetResponseAsync([question]);
    Console.WriteLine(response.Text);
}
```

**What happens:**

1. Defines a single question as a `ChatMessage`
2. Creates a `LocalChatClient` with Phi-3.5 mini, asks the question, and prints the answer
3. Creates a second `LocalChatClient` with Qwen 2.5 0.5B, asks the same question
4. Each client downloads its model on first use (if not already cached)

**Expected output:**

```
=== Phi-3.5 mini ===
Machine learning is a subset of artificial intelligence where algorithms learn patterns
from data to make predictions or decisions without being explicitly programmed.

=== Qwen 2.5 0.5B ===
Machine learning is a field of AI that enables computers to learn from data and improve
their performance over time without explicit programming.
```

> **Note:** Larger models generally produce more nuanced and accurate responses.

---

## DependencyInjection — ASP.NET Core DI Registration

**What it demonstrates:** Using `AddLocalLLMs()` to register `IChatClient` in ASP.NET Core's dependency injection container, then injecting it into endpoints.

**Run it:**

```bash
dotnet run --project samples/DependencyInjection
```

Then test with:

```bash
curl -X POST http://localhost:5000/chat -d "What is the capital of France?"
```

**Key code:**

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
});

var app = builder.Build();

app.MapPost("/chat", async (IChatClient client, HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var message = await reader.ReadToEndAsync();

    var response = await client.GetResponseAsync([
        new ChatMessage(ChatRole.User, message)
    ]);

    return response.Text;
});

app.MapGet("/", () => "ElBruno.LocalLLMs — POST /chat with a message to chat!");

app.Run();
```

**What happens:**

1. `AddLocalLLMs()` registers `LocalChatClient` as `IChatClient` in the DI container
2. The model is lazily initialized on the first request (no blocking during startup)
3. The `/chat` endpoint receives `IChatClient` via constructor injection
4. Any request body text is sent as a user message, and the model's response is returned

**Expected output:**

```
# Terminal shows:
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000

# curl response:
The capital of France is Paris.
```

> **Tip:** Because `LocalChatClient` implements `IChatClient`, you can swap it for Azure OpenAI, Ollama, or any other MEAI provider without changing your service code.

---

## FaraVisionAgent — Vision + Text with Fara1.5-9B

**What it demonstrates:** End-to-end usage of `KnownModels.Fara15_9B` with `LocalVisionChatClient`, including image prompts and optional local model-path overrides.

**Run it:**

```bash
dotnet run --project src/samples/FaraVisionAgent
```

With an image:

```bash
dotnet run --project src/samples/FaraVisionAgent -- --image-path C:\images\screen.png
```

**Key code:**

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true,
    ExecutionProvider = ExecutionProvider.Cpu
};

await using var client = new LocalVisionChatClient(options);

var response = await client.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "Describe the image in one sentence.")
],
new VisionChatOptions
{
    ImagePaths = [@"C:\images\screen.png"],
    MaxOutputTokens = 64
});
```

**What happens:**

1. The first run downloads `elbruno/Fara1.5-9B-onnx` into local cache.
2. The client loads the multimodal ONNX artifacts (`model`, `vision`, `embedding`).
3. The model processes image + text and returns a generated response.

See sample source:

- [`src/samples/FaraVisionAgent/Program.cs`](../src/samples/FaraVisionAgent/Program.cs)

---

## GptOssChat — GPT-OSS 20B with Harmony Channels

**What it demonstrates:** Chat, streaming, reasoning-effort selection, and tool calling against
`KnownModels.GptOss20B` — OpenAI's Apache-2.0 open-weight mixture-of-experts model.

**Run it:**

```bash
dotnet run --project src/samples/GptOssChat
```

Reuse an already-downloaded model directory instead of downloading ~12 GB again:

```bash
$env:GPTOSS_MODEL_PATH = "C:\models\gpt-oss-20b-onnx\cpu_and_mobile\cpu-int4-rtn-block-32-acc-level-4"
dotnet run --project src/samples/GptOssChat
```

**Key code:**

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.GptOss20B,          // or KnownModels.GptOss20BCuda
    ReasoningEffort = ReasoningEffort.Low,  // GPT-OSS only; ignored by other models
    MaxSequenceLength = 4096
};

await using var client = await LocalChatClient.CreateAsync(options);

var response = await client.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "What is the capital of France? Answer in one sentence.")
],
new ChatOptions { MaxOutputTokens = 128 });
```

**What happens:**

1. The first run downloads ~12 GB of INT4 weights from `onnxruntime/gpt-oss-20b-onnx`.
2. Messages are formatted using the **Harmony** template.
3. GPT-OSS reasons on an `analysis` channel and answers on a `final` channel. The library
   returns only the `final` channel — the chain-of-thought is filtered out and never shown.
4. Tool calls arrive on the `commentary` channel and surface as `FunctionCallContent`.

> ⚠️ GPT-OSS is a mixture-of-experts model. On CPU each response can take minutes. Use
> `KnownModels.GptOss20BCuda` with `Microsoft.ML.OnnxRuntimeGenAI.Cuda` for interactive speed.

See sample source:

- [`src/samples/GptOssChat/Program.cs`](../src/samples/GptOssChat/Program.cs)

---

## MagenticBrainAgent — Agentic Tool-Calling Loop (Qwen3 Template)

**What it demonstrates:** A round-based OmniAgent pattern with tools, using `KnownModels.MagenticBrain` or `KnownModels.Qwen3_14BInstruct`.

**Run it:**

```bash
dotnet run --project src/samples/MagenticBrainAgent
```

**Key code:**

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.MagenticBrain,
    EnsureModelDownloaded = true,
    Temperature = 0.7f,
    MaxSequenceLength = 32768
};

using var client = await LocalChatClient.CreateAsync(options);
```

**What happens:**

1. Loads MagenticBrain locally (auto-download on first run).
2. Sends the task and available tools to the model.
3. Repeats tool-call rounds until the model emits `submit`.

See sample source:

- [`src/samples/MagenticBrainAgent/Program.cs`](../src/samples/MagenticBrainAgent/Program.cs)

---

## MagenticUIServer — Local Multi-Agent Web Sample

**What it demonstrates:** A local web UX for agentic orchestration with SignalR streaming, React client, and Magentic-style orchestration loop.

**Run it:**

```bash
# Terminal 1
cd src/samples/MagenticUIServer/MagenticUIServer/ClientApp
npm install
npm run dev

# Terminal 2
dotnet run --project src/samples/MagenticUIServer/MagenticUIServer
```

**What happens:**

1. React client submits tasks through SignalR.
2. ASP.NET host routes tasks to the orchestrator service.
3. Orchestrator executes multi-round tool calls and streams progress.

See sample sources:

- [`src/samples/MagenticUIServer/MagenticUIServer`](../src/samples/MagenticUIServer/MagenticUIServer)
- [`src/samples/MagenticUIServer/MagenticUIServer/ClientApp`](../src/samples/MagenticUIServer/MagenticUIServer/ClientApp)
- [`docs/magentic-ui-dotnet.md`](magentic-ui-dotnet.md)

---

---

## BlazorDemo — Full Blazor Server showcase

**What it demonstrates:** All seven components from `ElBruno.LocalLLMs.BlazorComponents`
in a single Blazor Server app — model gallery, streaming chat, RAG playground,
environment dashboard, and a health badge in the nav bar.

**Run it:**

```bash
dotnet run --project src/samples/BlazorDemo
```

Then open `https://localhost:5001` in your browser.

**Pages:**

| Page | URL | Component(s) shown |
|------|-----|--------------------|
| Home | `/` | `ModelStatusCard` (×3 quick view) |
| Models | `/models` | `ModelGallery` with tier/vision/tools filters |
| Chat | `/chat` | `ChatBox` + `ModelSelector` |
| RAG Playground | `/rag` | `RagPlayground` |
| Environment | `/environment` | `EnvironmentDashboard` |
| (nav bar) | all pages | `LocalLLMHealthBadge` |

**Key code (`Program.cs`):**

```csharp
// Register core IChatClient
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.EnsureModelDownloaded = true;
});

// Register all BlazorComponents services
builder.Services.AddLocalLLMsBlazorComponents();
```

See the full [Blazor Components Guide](blazor-components.md) for component
API reference and configuration options.

---

## Next Steps

- 📖 [Getting Started](getting-started.md) — full setup guide with GPU configuration
- 🎯 [Supported Models](supported-models.md) — find the right model for your use case
- 📊 [Benchmarks](benchmarks.md) — measure performance on your hardware
- 🏗️ [Architecture](architecture.md) — understand the internal design
- 🧩 [Blazor Components](blazor-components.md) — ready-to-use UI components for Blazor apps
