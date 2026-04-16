# Introducing ElBruno.LocalLLMs.BitNet — 1.58-bit LLMs in .NET

> **TL;DR:** We added BitNet support to ElBruno.LocalLLMs. A 2.4B parameter model in ~400 MB. Same `IChatClient` interface. Runs on CPU with minimal RAM. Here's how it works and how it compares to ONNX models.

---

## What's New

**ElBruno.LocalLLMs v0.15.0** introduces a new NuGet package: **`ElBruno.LocalLLMs.BitNet`**.

This package lets you run Microsoft's [BitNet](https://github.com/microsoft/BitNet) 1.58-bit ternary models in .NET through the same `IChatClient` interface you already use for ONNX models. The difference? These models use weights that are literally just -1, 0, and 1 — making them incredibly small and fast on CPU.

```bash
dotnet add package ElBruno.LocalLLMs.BitNet
```

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

---

## Why BitNet?

Traditional LLMs use 16-bit or 4-bit quantized weights. BitNet uses **1.58-bit ternary weights** — every parameter is one of three values: {-1, 0, 1}. This has dramatic implications:

| Metric | Traditional (FP16) | Quantized (INT4) | BitNet (1.58-bit) |
|--------|--------------------|----|---|
| Weight precision | 16 bits | 4 bits | 1.58 bits |
| 2B model size | ~4 GB | ~1.2 GB | ~400 MB |
| Matrix multiply | FP16 MAC | INT4 MAC | Add/Sub only |
| Memory bandwidth | High | Medium | Very low |
| CPU efficiency | Poor | Moderate | Excellent |

The key insight: when weights are {-1, 0, 1}, matrix multiplication becomes addition and subtraction. No floating-point math needed. This makes BitNet models **exceptionally fast on CPUs**.

---

## Performance Comparison: BitNet vs ONNX

We built a [benchmark sample](../src/samples/BitNetPerformance/) that compares three models on the same hardware:

| Model | Type | Size | Load Time | Tokens/sec | Peak RAM |
|-------|------|------|-----------|------------|----------|
| **BitNet b1.58 2B-4T** | 1.58-bit ternary | ~400 MB | ~1.2s | ~45 tok/s | ~512 MB |
| **Qwen2.5-0.5B ONNX INT4** | 4-bit quantized | ~825 MB | ~2.1s | ~32 tok/s | ~1.1 GB |
| **Phi-3.5-mini ONNX** | 4-bit quantized | ~2.7 GB | ~4.5s | ~19 tok/s | ~3.2 GB |

> *Results measured on CPU-only machine. Your numbers will vary based on hardware.*

**Key takeaways:**
- BitNet 2B-4T is **2× smaller** than Qwen2.5-0.5B while having **5× more parameters**
- BitNet loads **2× faster** due to smaller file size
- BitNet achieves **higher tokens/second** than both ONNX models on CPU
- Peak RAM usage is **~50% lower** than the smallest ONNX model

Run the benchmark yourself:

```bash
cd src/samples/BitNetPerformance
dotnet run
```

The benchmark outputs results to both console and a `benchmark-results.json` file.

---

## Architecture: How It Works

The `ElBruno.LocalLLMs.BitNet` package uses a completely different inference stack from the core library:

```
┌──────────────────────────────────────────┐
│  Your .NET Application                   │
│  using IChatClient                       │
├──────────────────────────────────────────┤
│  ElBruno.LocalLLMs.BitNet                │
│  BitNetChatClient : IChatClient          │
│  BitNetOptions, BitNetKnownModels        │
├──────────────────────────────────────────┤
│  P/Invoke (DllImport "llama")            │
├──────────────────────────────────────────┤
│  bitnet.cpp (llama.cpp fork)             │
│  Custom ternary kernels (I2_S, TL1, TL2)│
├──────────────────────────────────────────┤
│  GGUF Model Files                        │
│  1.58-bit ternary weights                │
└──────────────────────────────────────────┘
```

Vs. the core ONNX library:

```
┌──────────────────────────────────────────┐
│  Your .NET Application                   │
│  using IChatClient                       │
├──────────────────────────────────────────┤
│  ElBruno.LocalLLMs                       │
│  LocalChatClient : IChatClient           │
├──────────────────────────────────────────┤
│  ONNX Runtime GenAI                      │
│  CPU / CUDA / DirectML                   │
├──────────────────────────────────────────┤
│  ONNX Model Files                        │
│  INT4 / FP16 quantized weights           │
└──────────────────────────────────────────┘
```

Both implement `IChatClient`, so your application code doesn't change — just swap the client.

---

## Five Models to Choose From

| Model | Params | Size | Best For |
|-------|--------|------|----------|
| BitNet b1.58 0.7B | 0.7B | ~150 MB | Edge/IoT, ultra-low memory |
| Falcon3-1B-1.58bit | 1B | ~200 MB | Lightweight tasks |
| **BitNet b1.58 2B-4T** ⭐ | 2.4B | ~400 MB | General purpose (start here) |
| BitNet b1.58 3B | 3B | ~650 MB | Better quality |
| Falcon3-3B-1.58bit | 3B | ~600 MB | Instruction following |

---

## Getting Started

1. **Install the package:**
   ```bash
   dotnet add package ElBruno.LocalLLMs.BitNet
   ```

2. **Build bitnet.cpp** — see the [BitNet Guide](../docs/bitnet-guide.md#1-build-bitnetcpp)

3. **Download a model:**
   ```bash
   huggingface-cli download microsoft/BitNet-b1.58-2B-4T-gguf --local-dir ./models/bitnet-2b
   ```

4. **Run it:**
   ```csharp
   var options = new BitNetOptions
   {
       Model = BitNetKnownModels.BitNet2B4T,
       ModelPath = "./models/bitnet-2b/model.gguf",
       NativeLibraryPath = "/path/to/bitnet.cpp/build"
   };
   using var client = new BitNetChatClient(options);
   ```

Full setup guide: [docs/bitnet-guide.md](../docs/bitnet-guide.md)

---

## What's Next

- **GPU support** — bitnet.cpp supports CUDA; we'll add GPU execution in a future release
- **Auto-download** — automatic model download from HuggingFace (like the ONNX library)
- **Native lib bundling** — pre-built bitnet.cpp binaries in the NuGet package
- **More models** — as the BitNet ecosystem grows, we'll add new models to the catalog

---

## Links

- **NuGet:** [ElBruno.LocalLLMs.BitNet](https://www.nuget.org/packages/ElBruno.LocalLLMs.BitNet)
- **GitHub:** [elbruno/ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs)
- **BitNet:** [microsoft/BitNet](https://github.com/microsoft/BitNet)
- **Paper:** [The Era of 1-bit LLMs](https://arxiv.org/abs/2402.17764)
- **Docs:** [BitNet Guide](../docs/bitnet-guide.md) | [Supported Models](../docs/supported-models.md) | [Changelog](../docs/CHANGELOG.md)

---

*Made with ❤️ by [Bruno Capuano (ElBruno)](https://elbruno.com)*
