# ElBruno.LocalLLMs

[![NuGet](https://img.shields.io/nuget/v/ElBruno.LocalLLMs.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.LocalLLMs)
[![CI](https://github.com/elbruno/ElBruno.LocalLLMs/actions/workflows/ci.yml/badge.svg)](https://github.com/elbruno/ElBruno.LocalLLMs/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

Run local LLMs in .NET through `IChatClient` — the same interface you'd use for Azure OpenAI, Ollama, or any other provider. Powered by ONNX Runtime GenAI.

## Features

- 🔌 **`IChatClient` implementation** — seamless integration with [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- 📦 **Automatic model download** — models are fetched from HuggingFace on first use
- 🚀 **Zero friction** — works out of the box with sensible defaults (Phi-3.5 mini)
- 🖥️ **Multi-hardware** — CPU, CUDA, and DirectML execution providers
- 💉 **DI-friendly** — register with `AddLocalLLMs()` in ASP.NET Core
- 🔄 **Streaming** — token-by-token streaming via `GetStreamingResponseAsync`
- 📊 **Multi-model** — switch between Phi-3.5, Phi-4, Qwen2.5, Llama 3.2, and more

## Installation

```bash
dotnet add package ElBruno.LocalLLMs
```

## Quick Start

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

## Streaming

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

await foreach (var update in client.GetStreamingResponseAsync([
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Explain quantum computing in simple terms.")
]))
{
    Console.Write(update.Text);
}
```

## Dependency Injection

```csharp
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.ExecutionProvider = ExecutionProvider.DirectML;
});

// Inject IChatClient anywhere
public class MyService(IChatClient chatClient) { ... }
```

## Supported Models

| Model | Size | ID |
|-------|------|----|
| Qwen2.5-0.5B-Instruct | Tiny (~0.5B) | `qwen2.5-0.5b-instruct` |
| Phi-3.5 mini instruct | Small (~3.8B) | `phi-3.5-mini-instruct` |
| Phi-4 | Medium (~14B) | `phi-4` |

More models are added as they are converted and validated. See [scripts/README.md](scripts/README.md) for the ONNX conversion pipeline.

## Samples

| Sample | Description |
|--------|-------------|
| [HelloChat](samples/HelloChat) | Minimal console chat |
| [StreamingChat](samples/StreamingChat) | Token-by-token streaming |
| [MultiModelChat](samples/MultiModelChat) | Switch models at runtime |
| [DependencyInjection](samples/DependencyInjection) | ASP.NET Core DI registration |

## Requirements

- .NET 8.0 or .NET 10.0
- CPU (default), NVIDIA GPU (CUDA), or Windows GPU (DirectML)
- ~2-8 GB disk space per model (depending on size and quantization)

## Documentation

- [Architecture](docs/architecture.md) — design decisions and internal structure

## License

[MIT](LICENSE) © Bruno Capuano
