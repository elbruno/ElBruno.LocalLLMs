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

| Tier | Model | Parameters | ONNX | ID |
|------|-------|-----------|------|----|
| ⚪ Tiny | TinyLlama-1.1B-Chat | 1.1B | ✅ Native | `tinyllama-1.1b-chat` |
| ⚪ Tiny | SmolLM2-1.7B-Instruct | 1.7B | ✅ Native | `smollm2-1.7b-instruct` |
| ⚪ Tiny | Qwen2.5-0.5B-Instruct | 0.5B | ✅ Native | `qwen2.5-0.5b-instruct` |
| ⚪ Tiny | Qwen2.5-1.5B-Instruct | 1.5B | ✅ Native | `qwen2.5-1.5b-instruct` |
| ⚪ Tiny | Gemma-2B-IT | 2B | 🔄 Convert | `gemma-2b-it` |
| ⚪ Tiny | StableLM-2-1.6B-Chat | 1.6B | 🔄 Convert | `stablelm-2-1.6b-chat` |
| 🟢 Small | Phi-3.5 mini instruct | 3.8B | ✅ Native | `phi-3.5-mini-instruct` |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | ✅ Native | `qwen2.5-3b-instruct` |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | 🔄 Convert | `llama-3.2-3b-instruct` |
| 🟢 Small | Gemma-2-2B-IT | 2B | 🔄 Convert | `gemma-2-2b-it` |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | ✅ Native | `qwen2.5-7b-instruct` |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | ✅ Native | `llama-3.1-8b-instruct` |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | ✅ Native | `mistral-7b-instruct-v0.3` |
| 🟡 Medium | Gemma-2-9B-IT | 9B | 🔄 Convert | `gemma-2-9b-it` |
| 🟡 Medium | Phi-4 | 14B | ✅ Native | `phi-4` |
| 🟡 Medium | DeepSeek-R1-Distill-Qwen-14B | 14B | ✅ Native | `deepseek-r1-distill-qwen-14b` |
| 🟡 Medium | Mistral-Small-24B-Instruct | 24B | ✅ Native | `mistral-small-24b-instruct` |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | ✅ Native | `qwen2.5-14b-instruct` |
| 🔴 Large | Qwen2.5-32B-Instruct | 32B | ✅ Native | `qwen2.5-32b-instruct` |
| 🔴 Large | Llama-3.3-70B-Instruct | 70B | 🔄 Convert | `llama-3.3-70b-instruct` |
| 🔴 Large | Mixtral-8x7B-Instruct-v0.1 | 8x7B | 🔄 Convert | `mixtral-8x7b-instruct-v0.1` |
| 🔴 Large | DeepSeek-R1-Distill-Llama-70B | 70B | 🔄 Convert | `deepseek-r1-distill-llama-70b` |
| 🔴 Large | Command-R (35B) | 35B | 🔄 Convert | `command-r-35b` |

See the [Supported Models Guide](docs/supported-models.md) for detailed model cards, performance benchmarks, and selection guidance.

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

- [Getting Started](docs/getting-started.md) — installation, first steps, configuration
- [Supported Models](docs/supported-models.md) — full model reference with tiers, specs, decision tree
- [Architecture](docs/architecture.md) — design decisions and internal structure
- [Samples Guide](docs/samples.md) — walkthrough of each sample application
- [Benchmarks](docs/benchmarks.md) — how to run and interpret performance benchmarks
- [ONNX Conversion](docs/onnx-conversion.md) — converting HuggingFace models to ONNX format
- [Publishing](docs/publishing.md) — NuGet package publishing with OIDC
- [Contributing](docs/CONTRIBUTING.md) — how to contribute
- [Changelog](docs/CHANGELOG.md) — version history

## License

[MIT](LICENSE) © Bruno Capuano
