# ElBruno.LocalLLMs

[![NuGet](https://img.shields.io/nuget/v/ElBruno.LocalLLMs.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.LocalLLMs)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.LocalLLMs.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.LocalLLMs)
[![Build Status](https://github.com/elbruno/ElBruno.LocalLLMs/actions/workflows/ci.yml/badge.svg)](https://github.com/elbruno/ElBruno.LocalLLMs/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![HuggingFace](https://img.shields.io/badge/🤗_HuggingFace-ONNX_Models-orange?style=flat-square)](https://huggingface.co/elbruno)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.LocalLLMs?style=social)](https://github.com/elbruno/ElBruno.LocalLLMs)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

## Run local LLMs in .NET through IChatClient 🧠

Run local LLMs in .NET through `IChatClient` — the same interface you'd use for Azure OpenAI, Ollama, or any other provider. Powered by ONNX Runtime GenAI.

## Features

- 🔌 **`IChatClient` implementation** — seamless integration with [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- 📦 **Automatic model download** — models are fetched from HuggingFace on first use
- 🚀 **Zero friction** — works out of the box with sensible defaults (Phi-3.5 mini)
- 🖥️ **Multi-hardware** — CPU, CUDA, and DirectML execution providers
- 💉 **DI-friendly** — register with `AddLocalLLMs()` in ASP.NET Core
- 🔄 **Streaming** — token-by-token streaming via `GetStreamingResponseAsync`
- 📊 **Multi-model** — switch between Phi-3.5, Phi-4, Qwen2.5, Llama 3.2, and more
- 🎯 **Fine-tuned models** — pre-trained Qwen2.5 variants for tool calling and RAG ([guide](docs/fine-tuning-guide.md))

## Installation

```bash
dotnet add package ElBruno.LocalLLMs
```

Then add **one** runtime package depending on your target hardware:

```bash
# 🖥️ CPU (works everywhere — required for CPU-only apps):
dotnet add package Microsoft.ML.OnnxRuntimeGenAI

# 🟢 NVIDIA GPU (CUDA):
dotnet add package Microsoft.ML.OnnxRuntimeGenAI.Cuda

# 🔵 Any Windows GPU — AMD, Intel, NVIDIA (DirectML):
dotnet add package Microsoft.ML.OnnxRuntimeGenAI.DirectML
```

> ⚠️ **Add exactly one runtime package.** Do not reference both `Microsoft.ML.OnnxRuntimeGenAI`
> and `Microsoft.ML.OnnxRuntimeGenAI.Cuda` simultaneously — the native binaries conflict and
> GPU support will silently fail.

> 🚀 The library defaults to `ExecutionProvider.Auto` — it tries GPU first and falls back to CPU automatically. No code changes needed.

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

## First Run

The first time you create a `LocalChatClient`, the model is downloaded from HuggingFace to your local cache directory (~2-4 GB). This typically takes **30-60 seconds** depending on your internet connection.

**Track download progress:**
```csharp
using var client = await LocalChatClient.CreateAsync(
    new LocalLLMsOptions { Model = KnownModels.Phi35MiniInstruct },
    progress: new Progress<ModelDownloadProgress>(p =>
    {
        var percent = (p.BytesDownloaded * 100) / p.TotalBytes;
        Console.WriteLine($"{p.FileName}: {percent:F1}%");
    })
);
```

**Subsequent runs load instantly** from cache (`%LOCALAPPDATA%/ElBruno/LocalLLMs/models`).

**Skip auto-download** if using a pre-downloaded model:
```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct,
    ModelPath = "/path/to/local/model",
    EnsureModelDownloaded = false
};
using var client = await LocalChatClient.CreateAsync(options);
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

## GPU Acceleration

By default, `ExecutionProvider.Auto` tries GPU first (CUDA → DirectML) and falls back to CPU automatically:

```csharp
// Use explicit GPU provider (fails if CUDA not installed; use Auto to fallback to CPU)
var options = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cuda
};

// Multi-GPU systems: select device ID
var options2 = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cuda,
    GpuDeviceId = 1  // Use second GPU
};
```

**Auto fallback behavior:**
- **CUDA available** → uses NVIDIA GPU
- **CUDA unavailable, DirectML available** → uses AMD/Intel Arc GPU
- **GPU unavailable** → falls back to CPU (no errors, just slower)

See [Troubleshooting: GPU Setup](docs/troubleshooting-guide.md#gpu-setup-validation) for debugging GPU issues.

## Model Metadata

Inspect model capabilities at runtime — context window size, model name, and vocabulary:

```csharp
using var client = await LocalChatClient.CreateAsync();

var metadata = client.ModelInfo;
Console.WriteLine($"Model:          {metadata?.ModelName}");
Console.WriteLine($"Context window: {metadata?.MaxSequenceLength}");
Console.WriteLine($"Vocab size:     {metadata?.VocabSize}");
```

This is useful for prompt-length validation, adaptive chunking, and model selection logic.

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

## Error Handling

The library provides structured exception types for graceful error handling:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

try
{
    using var client = await LocalChatClient.CreateAsync();
    var response = await client.GetResponseAsync([
        new(ChatRole.User, "Your question here")
    ]);
}
catch (ExecutionProviderException ex)
{
    // GPU/provider-specific error (no CUDA, DirectML not available, etc.)
    Console.WriteLine($"Provider error: {ex.Message}");
}
catch (ModelCapacityExceededException ex)
{
    // Prompt/response too long for model's context window
    Console.WriteLine($"Capacity error: {ex.Message}");
    // Solution: use a larger model or truncate the prompt
}
catch (InvalidOperationException ex)
{
    // General operation error (model not found, download failed, etc.)
    Console.WriteLine($"Operation error: {ex.Message}");
}
```

## Troubleshooting

**GPU not working?** Use `ExecutionProvider.Cpu` explicitly. See [GPU Setup Validation](docs/troubleshooting-guide.md#gpu-setup-validation).

**Out of memory?** Try a smaller model:
```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct  // 0.5B instead of 3.8B
};
```

**Model download fails?**
- Check your internet connection
- For private HuggingFace models, set the `HF_TOKEN` environment variable

For detailed troubleshooting, see [docs/troubleshooting-guide.md](docs/troubleshooting-guide.md).

## Supported Models

| Tier | Model | Parameters | ONNX | ID |
|------|-------|-----------|------|----|
| ⚪ Tiny | TinyLlama-1.1B-Chat | 1.1B | ✅ Native | `tinyllama-1.1b-chat` |
| ⚪ Tiny | SmolLM2-1.7B-Instruct | 1.7B | ✅ Native | `smollm2-1.7b-instruct` |
| ⚪ Tiny | Qwen2.5-0.5B-Instruct | 0.5B | ✅ Native | `qwen2.5-0.5b-instruct` |
| ⚪ Tiny | Qwen2.5-1.5B-Instruct | 1.5B | ✅ Native | `qwen2.5-1.5b-instruct` |
| ⚪ Tiny | Gemma-2B-IT | 2B | ✅ Native | `gemma-2b-it` |
| ⚪ Tiny | Gemma-4-E2B-IT | 5.1B (2B active) | ⏳ Pending | `gemma-4-e2b-it` |
| ⚪ Tiny | StableLM-2-1.6B-Chat | 1.6B | 🔄 Convert | `stablelm-2-1.6b-chat` |
| 🟢 Small | Phi-3.5 mini instruct | 3.8B | ✅ Native | `phi-3.5-mini-instruct` |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | ✅ Native | `qwen2.5-3b-instruct` |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | ✅ Native | `llama-3.2-3b-instruct` |
| 🟢 Small | Gemma-2-2B-IT | 2B | ✅ Native | `gemma-2-2b-it` |
| 🟢 Small | Gemma-4-E4B-IT | 8B (4B active) | ⏳ Pending | `gemma-4-e4b-it` |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | ✅ Native | `qwen2.5-7b-instruct` |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | ✅ Native | `llama-3.1-8b-instruct` |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | ✅ Native | `mistral-7b-instruct-v0.3` |
| 🟡 Medium | Gemma-2-9B-IT | 9B | ✅ Native | `gemma-2-9b-it` |
| 🟡 Medium | Phi-4 | 14B | ✅ Native | `phi-4` |
| 🟡 Medium | DeepSeek-R1-Distill-Qwen-14B | 14B | ✅ Native | `deepseek-r1-distill-qwen-14b` |
| 🟡 Medium | Mistral-Small-24B-Instruct | 24B | ✅ Native | `mistral-small-24b-instruct` |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | ✅ Native | `qwen2.5-14b-instruct` |
| 🔴 Large | Qwen2.5-32B-Instruct | 32B | ✅ Native | `qwen2.5-32b-instruct` |
| 🔴 Large | Llama-3.3-70B-Instruct | 70B | ✅ ONNX | `llama-3.3-70b-instruct` |
| 🔴 Large | Mixtral-8x7B-Instruct-v0.1 | 8x7B | 🔄 Convert | `mixtral-8x7b-instruct-v0.1` |
| 🔴 Large | DeepSeek-R1-Distill-Llama-70B | 70B | 🔄 Convert | `deepseek-r1-distill-llama-70b` |
| 🔴 Large | Command-R (35B) | 35B | 🔄 Convert | `command-r-35b` |
| 🔴 Large | Gemma-4-26B-A4B-IT | 25.2B (3.8B active) | ⏳ Pending | `gemma-4-26b-a4b-it` |
| 🔴 Large | Gemma-4-31B-IT | 30.7B | ⏳ Pending | `gemma-4-31b-it` |

> **⏳ Pending** = Model definitions are ready but ONNX conversion requires runtime support from [onnxruntime-genai](https://github.com/microsoft/onnxruntime-genai). Gemma 4's novel PLE architecture is not yet supported.

### Fine-Tuned Models

Pre-trained variants optimized for specific tasks. A fine-tuned 0.5B model often matches or exceeds a base 1.5B on its specialized task.

| Model | Size | Task | HuggingFace ID |
|-------|------|------|----------------|
| Qwen2.5-0.5B-ToolCalling | ~1 GB | Tool/function calling | `elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling` |
| Qwen2.5-0.5B-RAG | ~1 GB | RAG with citations | `elbruno/Qwen2.5-0.5B-LocalLLMs-RAG` |
| Qwen2.5-0.5B-Instruct | ~1 GB | General-purpose | `elbruno/Qwen2.5-0.5B-LocalLLMs-Instruct` |

See the [Supported Models Guide](docs/supported-models.md) for detailed model cards, performance benchmarks, and selection guidance.

## Samples

| Sample | Description |
|--------|-------------|
| [HelloChat](src/samples/HelloChat) | Minimal console chat |
| [StreamingChat](src/samples/StreamingChat) | Token-by-token streaming |
| [MultiModelChat](src/samples/MultiModelChat) | Switch models at runtime |
| [DependencyInjection](src/samples/DependencyInjection) | ASP.NET Core DI registration |
| [ToolCallingAgent](src/samples/ToolCallingAgent) | Function calling and tool use |
| [FineTunedToolCalling](src/samples/FineTunedToolCalling) | Fine-tuned model for improved tool calling |
| [RagChatbot](src/samples/RagChatbot) | RAG pipeline with document retrieval |
| [ZeroCloudRag](src/samples/ZeroCloudRag) | Zero-cloud RAG pipeline with real local embeddings and LLM inference |
| [ConsoleAppDemo](src/samples/ConsoleAppDemo) | Interactive console application |

## Requirements

- .NET 8.0 or .NET 10.0
- CPU (default), NVIDIA GPU (CUDA), or Windows GPU (DirectML)
- ~2-8 GB disk space per model (depending on size and quantization)

## Building from Source

```bash
git clone https://github.com/elbruno/ElBruno.LocalLLMs.git
cd ElBruno.LocalLLMs
dotnet restore ElBruno.LocalLLMs.slnx
dotnet build ElBruno.LocalLLMs.slnx
dotnet test ElBruno.LocalLLMs.slnx --framework net8.0
```

## Documentation

- [Getting Started](docs/getting-started.md) — installation, first steps, configuration
- [Supported Models](docs/supported-models.md) — full model reference with tiers, specs, decision tree
- [Architecture](docs/architecture.md) — design decisions and internal structure
- [Samples Guide](docs/samples.md) — walkthrough of each sample application
- [Benchmarks](docs/benchmarks.md) — how to run and interpret performance benchmarks
- [Fine-Tuning Guide](docs/fine-tuning-guide.md) — using and training fine-tuned models
- [ONNX Conversion](docs/onnx-conversion.md) — converting HuggingFace models to ONNX format
- [Publishing](docs/publishing.md) — NuGet package publishing with OIDC
- [Contributing](docs/CONTRIBUTING.md) — how to contribute
- [Changelog](docs/CHANGELOG.md) — version history

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## 👋 About the Author

**Made with ❤️ by [Bruno Capuano (ElBruno)](https://github.com/elbruno)**

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)

## 🙏 Acknowledgments

- [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai) — inference engine
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) — IChatClient interface
- [Hugging Face](https://huggingface.co/) — model hosting and community
