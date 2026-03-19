# Getting Started with ElBruno.LocalLLMs

Welcome! This guide will help you set up and run local LLMs in your .NET application within minutes.

---

## Prerequisites

Before you start, ensure you have:

- **.NET 8.0 or .NET 10.0** — [Download](https://dotnet.microsoft.com/en-us/download)
- **~2-8 GB disk space** — to cache downloaded models
- **A compatible processor** — CPU (default), NVIDIA GPU (CUDA 11.8+), or Windows GPU (DirectML)

> **Tip:** First runs download the model (1-8 GB depending on the model). Subsequent runs load from cache instantly.

---

## Installation

Add the NuGet package to your project:

```bash
dotnet add package ElBruno.LocalLLMs
```

Or manually edit your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="ElBruno.LocalLLMs" Version="0.1.0" />
</ItemGroup>
```

---

## Quick Start

Here's the minimal example — 5 lines of code to ask a question:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

using var client = await LocalChatClient.CreateAsync();

var response = await client.GetResponseAsync([
    new(ChatRole.User, "What is the capital of France?")
]);

Console.WriteLine(response.Text);
```

**What happens:**
1. `LocalChatClient.CreateAsync()` creates a client and downloads Phi-3.5-mini-instruct (3.8B parameters) on first run
2. `GetResponseAsync()` sends your message through the model
3. `response.Text` contains the complete response from the LLM

**Output:**
```
The capital of France is Paris.
```

---

## Choosing a Model

ElBruno.LocalLLMs supports 29 models across 5 tiers. Here's how to pick:

### **⚪ Tiny** (0.5B–2B)
- **Use for:** Edge devices, IoT, fast prototyping, testing the library
- **Speed:** ⚡⚡⚡ (super fast)
- **Quality:** ⭐ (limited context understanding)
- **RAM:** 1–4 GB

**Models:**
- `Qwen2.5-0.5B-Instruct` — fastest, minimal quality
- `TinyLlama-1.1B-Chat` — good for small tasks

### **🟢 Small** (3B–4B) — **RECOMMENDED**
- **Use for:** Most local inference tasks, chatbots, content generation
- **Speed:** ⚡⚡ (fast)
- **Quality:** ⭐⭐⭐ (solid reasoning)
- **RAM:** 6–8 GB

**Models:**
- `Phi-3.5-mini-instruct` ✅ *Native ONNX — no conversion needed* — **Best starting point**
- `Qwen2.5-3B-Instruct` — excellent instruction-following
- `Llama-3.2-3B-Instruct` — robust general-purpose

### **🟡 Medium** (7B–14B)
- **Use for:** Production deployments, complex reasoning, coding tasks
- **Speed:** ⚡ (moderate)
- **Quality:** ⭐⭐⭐⭐ (excellent)
- **RAM:** 8–16 GB

**Models:**
- `Phi-4` ✅ *Native ONNX* — superior reasoning, code generation
- `Qwen2.5-7B-Instruct` — powerful instruction-following
- `Mistral-7B-Instruct-v0.3` — strong general model
- `DeepSeek-R1-Distill-Qwen-14B` — exceptional reasoning (slower)

### **🔴 Large** (14B–70B)
- **Use for:** Multi-GPU setups, advanced reasoning, heavy workloads
- **Speed:** 🐢 (slow on consumer hardware)
- **Quality:** ⭐⭐⭐⭐⭐ (state-of-the-art)
- **RAM:** 16–40+ GB

**Models:**
- `Qwen2.5-14B-Instruct`, `Llama-3.3-70B-Instruct`, `Mixtral-8x7B-Instruct-v0.1`

### **🟣 Next-Gen** (Latest releases)
- `Llama-4-Scout`, `Qwen3-8B`, `DeepSeek-V3` (cutting edge)

> **Decision Tree:**
> - New to the library? → Start with **Phi-3.5-mini-instruct**
> - Limited RAM (<8 GB)? → Use **Qwen2.5-0.5B-Instruct** (Tiny)
> - Production quality needed? → Use **Phi-4** or **Qwen2.5-7B-Instruct** (Medium)
> - Advanced reasoning/coding? → Use **DeepSeek-R1-Distill** or **Mistral-7B**

---

## Streaming Responses

For long responses, stream tokens as they're generated instead of waiting for the complete output:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

Console.WriteLine("Streaming response:\n");
await foreach (var update in client.GetStreamingResponseAsync([
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Write a short poem about the ocean.")
]))
{
    Console.Write(update.Text);
}
Console.WriteLine();
```

**Output:**
```
Streaming response:

Waves dance upon the sandy shore,
Their rhythm echoing forevermore,
Blue depths with mysteries untold,
Stories of the ocean, brave and bold...
```

**Why stream?**
- **Responsive UI** — show text as it appears
- **Better UX** — don't make users wait for full responses
- **Real-time processing** — downstream code can act on partial responses

---

## Using Different Models

Switch models by setting the `Model` property in `LocalLLMsOptions`:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Use Phi-4 (14B, medium tier)
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi4
});

var response = await client.GetResponseAsync([
    new(ChatRole.User, "Explain quantum entanglement in 3 sentences.")
]);

Console.WriteLine(response.Text);
```

**Available models via `KnownModels`:**
- `KnownModels.Qwen25_05BInstruct` — Tiny (0.5B)
- `KnownModels.Phi35MiniInstruct` — Small (3.8B) — **Default**
- `KnownModels.Phi4` — Medium (14B)

> **Note:** To use models not listed above (Llama-3.2-3B, Qwen2.5-7B, etc.), create a custom `ModelDefinition` and pass it to options. See [Custom Models](#custom-models) below.

---

## Custom Model Path

If you've already downloaded a model locally, point directly to it instead of re-downloading:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var options = new LocalLLMsOptions
{
    ModelPath = @"C:\Users\you\models\phi-3.5-mini-onnx\cpu_and_mobile\cpu-int4-rtn-block-32-acc-level-4"
};

using var client = await LocalChatClient.CreateAsync(options);

var response = await client.GetResponseAsync([
    new(ChatRole.User, "Hello!")
]);

Console.WriteLine(response.Text);
```

**Benefits:**
- Skip re-downloading if you already have the model
- Use models from external drives or network shares
- Useful in CI/CD pipelines with pre-cached models

---

## Dependency Injection (ASP.NET Core)

Register `LocalChatClient` as `IChatClient` in a web app:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Register LocalChatClient as IChatClient
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
});

var app = builder.Build();

// Inject IChatClient into your endpoints
app.MapPost("/chat", async (IChatClient chatClient, string message) =>
{
    var response = await chatClient.GetResponseAsync([
        new ChatMessage(ChatRole.User, message)
    ]);

    return response.Text;
});

app.Run();
```

**What happens:**
- `AddLocalLLMs()` registers `LocalChatClient` as a singleton implementing `IChatClient`
- ASP.NET Core injects it automatically into handlers, controllers, and services
- The model downloads once on first use (not at startup)

**Using in services:**

```csharp
public class ChatService(IChatClient chatClient)
{
    public async Task<string> Chat(string userMessage)
    {
        var response = await chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.User, userMessage)
        ]);

        return response.Text;
    }
}
```

---

## GPU Acceleration

By default, `LocalChatClient` runs on CPU. To use GPU for faster inference:

### NVIDIA CUDA

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi4,
    ExecutionProvider = ExecutionProvider.Cuda,
    GpuDeviceId = 0  // Device 0 (change if you have multiple GPUs)
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Requirements:**
- NVIDIA GPU (Maxwell generation or newer)
- CUDA 11.8 or newer
- cuDNN 8.0 or newer

### Windows DirectML

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi4,
    ExecutionProvider = ExecutionProvider.DirectML,
    GpuDeviceId = 0
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Requirements:**
- Windows 10 Build 18362 or newer
- Works with any GPU (NVIDIA, AMD, Intel Arc)

### CPU (Default)

```csharp
var options = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cpu  // Explicit, but default
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Performance Tip:**
- GPU inference is **5–20x faster** than CPU for large models
- GPU memory is separate from system RAM (usually 2–24 GB)
- CPU inference works everywhere but is slower (good for testing)

---

## ⚠️ Important: GPU Support Requires Building from Source

**The standard NuGet package is CPU-only.** To enable GPU acceleration (CUDA or DirectML), you must build ONNX Runtime GenAI from source with GPU support flags.

### Why?

The official NuGet packages for `Microsoft.ML.OnnxRuntimeGenAI` are compiled without GPU support by default. This is a limitation of the distributed binaries, not this library.

### How to Enable GPU Support

**Option 1: Build ONNX Runtime GenAI locally (Recommended)**

```bash
# Clone the repository
git clone https://github.com/microsoft/onnxruntime-genai
cd onnxruntime-genai

# Build with GPU support
# For CUDA (NVIDIA):
python build.py --use_cuda

# For DirectML (Windows, any GPU):
python build.py --use_directml

# For both:
python build.py --use_cuda --use_directml
```

Then reference the locally-built binaries in your project's `.csproj`:

```xml
<ItemGroup>
    <Reference Include="Microsoft.ML.OnnxRuntimeGenAI">
        <HintPath>../path-to-built/Microsoft.ML.OnnxRuntimeGenAI.dll</HintPath>
    </Reference>
</ItemGroup>
```

**Option 2: Wait for official GPU NuGet packages**

Monitor the [ONNX Runtime releases](https://github.com/microsoft/onnxruntime-genai/releases) for official GPU-enabled NuGet packages. As of March 2026, these would appear as separate package variants (e.g., `Microsoft.ML.OnnxRuntimeGenAI.Cuda`, `Microsoft.ML.OnnxRuntimeGenAI.DirectML`).

### Verify GPU is Being Used

After configuring, run any sample and check the output:

```
Requested provider: Auto
Active provider:    DirectML    ← GPU is active (not Cpu)
Selection:          Auto selected DirectML
```

If you still see `Active provider: Cpu`, the GPU library build did not include GPU support.

---

## Configuration Options

Here's a complete reference of all `LocalLLMsOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Model` | `ModelDefinition` | `KnownModels.Phi35MiniInstruct` | Which LLM to use. Pre-defined in `KnownModels` or create custom `ModelDefinition`. |
| `ModelPath` | `string?` | `null` | Local directory path to a pre-downloaded model. If set, skips download. |
| `CacheDirectory` | `string?` | `%LOCALAPPDATA%\ElBruno\LocalLLMs\models` | Where to cache downloaded models. |
| `EnsureModelDownloaded` | `bool` | `true` | Automatically download the model if not cached. |
| `ExecutionProvider` | `ExecutionProvider` | `Cpu` | Hardware to run on: `Cpu`, `Cuda`, or `DirectML`. |
| `GpuDeviceId` | `int` | `0` | GPU device ID (if using CUDA/DirectML). Use 0 for single-GPU systems. |
| `MaxSequenceLength` | `int` | `2048` | Maximum tokens the model can generate. Affects memory usage. |
| `Temperature` | `float` | `0.7f` | Creativity level (0.0 = deterministic, 1.0 = very random). Lower = more focused. |
| `TopP` | `float` | `0.9f` | Nucleus sampling threshold (0.0–1.0). Filters to top 90% probability tokens. |

**Example with custom configuration:**

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct,
    CacheDirectory = @"D:\llm-cache",  // Use D: drive for models
    ExecutionProvider = ExecutionProvider.Cuda,
    MaxSequenceLength = 4096,          // Allow longer responses
    Temperature = 0.5f,                // More deterministic
    TopP = 0.8f                        // More focused output
};

using var client = await LocalChatClient.CreateAsync(options);
```

---

## ONNX Conversion

### Native ONNX Models (No Conversion Needed)

These models have ONNX weights pre-published on HuggingFace:
- ✅ `Phi-3.5-mini-instruct` — ready to download and use
- ✅ `Phi-4` — ready to download and use

### Converting Other Models to ONNX

If you want to use a model without native ONNX (e.g., Qwen2.5-7B, Llama-3.2-3B), convert it first:

1. **Navigate to scripts directory:**
   ```bash
   cd scripts/
   ```

2. **Install Python dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

3. **Run conversion:**
   ```bash
   python convert_to_onnx.py \
       --model-id Qwen/Qwen2.5-7B-Instruct \
       --output-dir ./onnx-models/qwen2.5-7b
   ```

4. **Point `ModelPath` to the output:**
   ```csharp
   var options = new LocalLLMsOptions
   {
       ModelPath = @"./onnx-models/qwen2.5-7b"
   };

   using var client = await LocalChatClient.CreateAsync(options);
   ```

For detailed ONNX conversion steps, see [scripts/README.md](../scripts/README.md).

---

## Troubleshooting

### Model Not Found / Download Fails

**Problem:** `ModelNotFoundException: Model 'phi-3.5-mini-instruct' not found in cache.`

**Solutions:**
- Ensure internet connection — the downloader needs to reach HuggingFace
- Check `CacheDirectory` permissions — ensure your app can write to the cache folder
- Check HuggingFace repo exists — verify the model ID is correct in `KnownModels`
- Set `EnsureModelDownloaded = true` to allow auto-download

```csharp
var options = new LocalLLMsOptions
{
    EnsureModelDownloaded = true  // Allow download if not cached
};
```

---

### Out of Memory

**Problem:** `OutOfMemoryException` or system hangs during inference.

**Solutions:**
- **Reduce model size** — switch to a Tiny or Small tier model
- **Lower `MaxSequenceLength`** — use fewer tokens (1024 instead of 2048)
- **Use GPU** — GPU memory is separate; offload to CUDA or DirectML
- **Check background processes** — close heavy applications
- **Monitor RAM** — watch Task Manager during inference

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct,  // Smallest model
    MaxSequenceLength = 1024                  // Shorter responses
};
```

---

### Slow First Run

**Problem:** First inference takes 30+ seconds.

**Why:** The model is being loaded from disk into VRAM/RAM for the first time.

**Solutions:**
- This is **normal** — subsequent calls are faster (often 1–5 seconds)
- **Use GPU** — first load is still slow but inference after is much faster
- **Use `CreateAsync()`** — gives better progress reporting during model load
- **Pre-download** — call `CreateAsync()` during app startup to warm up the model before handling requests

```csharp
// Warm up the model at startup
var client = await LocalChatClient.CreateAsync(options);
Console.WriteLine("Model loaded and ready.");

// Now subsequent calls are fast
```

---

### Gibberish Output

**Problem:** The model generates nonsensical text.

**Possible causes:**
- **Corrupted model file** — delete cache and re-download
- **Wrong chat template** — verify your custom `ModelDefinition` has the correct `ChatTemplate`
- **Generation parameters** — try lowering `Temperature` (more deterministic)

**Fix:**
```csharp
// Clear cache and force re-download
var options = new LocalLLMsOptions
{
    CacheDirectory = @"C:\new-cache"  // Use fresh cache
};

// Or adjust generation
var opts = new ChatOptions
{
    Temperature = 0.3f  // Very deterministic
};

var response = await client.GetResponseAsync(messages, opts);
```

---

### GPU Not Detected

**Problem:** `ExecutionProvider = ExecutionProvider.Cuda` but still using CPU.

**Check:**
- **NVIDIA Control Panel** — verify GPU is installed and working
- **CUDA version** — run `nvidia-smi` in terminal; ensure CUDA 11.8+ is installed
- **Driver updates** — update NVIDIA drivers to latest
- **GpuDeviceId** — if you have multiple GPUs, verify you're targeting the right one

```csharp
// Enable verbose logging (implementation-specific)
var options = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cuda,
    GpuDeviceId = 0  // Verify this is the correct GPU
};
```

---

## Next Steps

- 📖 Read the [Architecture](architecture.md) guide for internal design details
- 🎯 Check [Supported Models](supported-models.md) for the full list and specifications
- 🔧 See [samples/](../samples/) for runnable examples
- 📝 Read [CONTRIBUTING.md](CONTRIBUTING.md) to add a new model

Happy chatting! 🚀
