# BitNet Guide

Run Microsoft's 1.58-bit ternary models locally in .NET using `ElBruno.LocalLLMs.BitNet`.

---

## What is BitNet?

[BitNet](https://github.com/microsoft/BitNet) is Microsoft's 1.58-bit Large Language Model architecture where every weight is ternary: **{-1, 0, 1}**. This means:

- **Extreme compression** — a 2.4B parameter model fits in ~400 MB (vs ~5 GB for FP16)
- **CPU-friendly** — matrix multiplications become additions and subtractions
- **Low memory** — models run comfortably on 4-8 GB RAM machines
- **Fast inference** — up to 6× faster than equivalent FP16 models on CPU

The `ElBruno.LocalLLMs.BitNet` package wraps [bitnet.cpp](https://github.com/microsoft/BitNet) (a llama.cpp fork with optimized ternary kernels) via P/Invoke, implementing the same `IChatClient` interface from Microsoft.Extensions.AI.

---

## Installation

```bash
dotnet add package ElBruno.LocalLLMs.BitNet
```

> **Note:** Unlike the core `ElBruno.LocalLLMs` package, BitNet does **not** use ONNX Runtime. It requires a pre-built `bitnet.cpp` native library.

---

## Prerequisites

### 1. Build bitnet.cpp

Clone and build the bitnet.cpp native library:

```bash
# Clone bitnet.cpp
git clone https://github.com/microsoft/BitNet.git
cd BitNet

# Build (Linux/macOS)
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release

# Build (Windows)
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

The native library will be at:
- **Windows:** `build/Release/llama.dll`
- **Linux:** `build/libllama.so`
- **macOS:** `build/libllama.dylib`

### 2. Download a GGUF Model

Download a BitNet GGUF model from HuggingFace:

```bash
# Recommended: BitNet b1.58 2B-4T (MIT license, ~400 MB)
huggingface-cli download microsoft/BitNet-b1.58-2B-4T-gguf --local-dir ./models/bitnet-2b
```

Or use the Python setup script from the BitNet repo:

```bash
python setup_env.py --hf-repo microsoft/BitNet-b1.58-2B-4T-gguf -q i2_s
```

---

## Quick Start

```csharp
using ElBruno.LocalLLMs.BitNet;
using Microsoft.Extensions.AI;

var options = new BitNetOptions
{
    Model = BitNetKnownModels.BitNet2B4T,
    ModelPath = "/path/to/bitnet-2b/model.gguf",
    NativeLibraryPath = "/path/to/bitnet.cpp/build"
};

using var client = new BitNetChatClient(options);

var response = await client.GetResponseAsync([
    new(ChatRole.User, "What is quantum computing?")
]);

Console.WriteLine(response.Text);
```

### Using Environment Variables

```bash
# Set once, use everywhere
export BITNET_NATIVE_PATH=/path/to/bitnet.cpp/build
export BITNET_MODEL_PATH=/path/to/bitnet-2b/model.gguf
```

```csharp
var options = new BitNetOptions
{
    Model = BitNetKnownModels.BitNet2B4T,
    ModelPath = Environment.GetEnvironmentVariable("BITNET_MODEL_PATH")!,
    NativeLibraryPath = Environment.GetEnvironmentVariable("BITNET_NATIVE_PATH")!
};
```

---

## Streaming

```csharp
await foreach (var update in client.GetStreamingResponseAsync([
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Explain quantum computing in simple terms.")
]))
{
    Console.Write(update.Text);
}
```

---

## Dependency Injection

```csharp
builder.Services.AddBitNetChatClient(options =>
{
    options.Model = BitNetKnownModels.BitNet2B4T;
    options.ModelPath = "/path/to/model.gguf";
    options.NativeLibraryPath = "/path/to/native/lib";
    options.MaxTokens = 2048;
    options.Temperature = 0.7f;
});

// Inject IChatClient anywhere
public class MyService(IChatClient chatClient) { ... }
```

---

## Supported Models

| Model | Params | Size | Kernel | License | Best For |
|-------|--------|------|--------|---------|----------|
| BitNet b1.58 0.7B | 0.7B | ~150 MB | I2_S | MIT | Edge devices, ultra-low memory |
| Falcon3-1B-1.58bit | 1B | ~200 MB | TL2 | Apache-2.0 | Lightweight tasks |
| **BitNet b1.58 2B-4T** ⭐ | 2.4B | ~400 MB | TL2 | MIT | General purpose (recommended) |
| BitNet b1.58 3B | 3B | ~650 MB | I2_S | MIT | Better quality, still small |
| Falcon3-3B-1.58bit | 3B | ~600 MB | TL2 | Apache-2.0 | Instruction following |

### Kernel Types

- **I2_S** — Original BitNet ternary kernel (integer 2-bit storage)
- **TL1** — Table lookup kernel v1 (optimized for ARM)
- **TL2** — Table lookup kernel v2 (best performance on x86/ARM)

---

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Model` | `BitNetModelDefinition` | `BitNet2B4T` | Model definition from catalog |
| `ModelPath` | `string` | `null` | Path to GGUF model file |
| `NativeLibraryPath` | `string?` | `null` | Path to bitnet.cpp native library directory |
| `MaxTokens` | `int` | `2048` | Maximum tokens to generate |
| `Temperature` | `float` | `0.7` | Sampling temperature (0.0 = deterministic) |
| `TopP` | `float` | `0.9` | Nucleus sampling probability |
| `TopK` | `int` | `40` | Top-K sampling |
| `RepeatPenalty` | `float` | `1.1` | Repetition penalty |
| `ContextSize` | `int` | `2048` | Context window size in tokens |
| `ThreadCount` | `int` | CPU count | Number of inference threads |
| `Seed` | `int?` | `null` | Random seed for reproducibility |

---

## BitNet vs ONNX: When to Use Which

| Aspect | BitNet (`ElBruno.LocalLLMs.BitNet`) | ONNX (`ElBruno.LocalLLMs`) |
|--------|------|------|
| **Model format** | GGUF (1.58-bit ternary) | ONNX (INT4/FP16) |
| **Runtime** | bitnet.cpp (native C++) | ONNX Runtime GenAI |
| **GPU support** | CPU only (Phase 1) | CPU, CUDA, DirectML |
| **Model size** | Extremely small (150 MB–650 MB) | Larger (825 MB–40+ GB) |
| **Memory usage** | Very low (1–6 GB) | Higher (2–40+ GB) |
| **Model quality** | Good for size, but limited model selection | Wide selection, state-of-the-art models |
| **Auto-download** | Manual (user provides model) | Automatic from HuggingFace |
| **Setup** | Requires building bitnet.cpp | Just add NuGet packages |

**Use BitNet when:**
- Running on CPU-only machines
- Memory is severely constrained
- You need the smallest possible model footprint
- You want maximum CPU inference speed

**Use ONNX when:**
- You have a GPU available
- You need the best model quality
- You want zero-setup model downloads
- You need a wider selection of models

---

## Troubleshooting

### Native library not found

```
BitNetNativeLibraryException: Failed to load native library 'llama'
```

**Fix:** Set `BitNetOptions.NativeLibraryPath` to the directory containing the native library, or add the directory to your system PATH.

### Model file not found

```
FileNotFoundException: Model file not found
```

**Fix:** Ensure `BitNetOptions.ModelPath` points to a valid `.gguf` file.

### Wrong kernel type

If inference produces garbage output, check that the model's kernel type matches. Models quantized with `i2_s` need `BitNetKernelType.I2_S`, and TL2 models need `BitNetKernelType.TL2`.

---

## See Also

- [BitNet GitHub Repository](https://github.com/microsoft/BitNet)
- [BitNet b1.58 Paper](https://arxiv.org/abs/2402.17764)
- [Supported Models](supported-models.md#-bitnet-models-158-bit-ternary)
- [Changelog](CHANGELOG.md)
