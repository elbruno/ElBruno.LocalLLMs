# Troubleshooting Guide

## GPU Setup Validation

### NVIDIA GPU (CUDA)

**Checklist:**
1. ✅ NVIDIA GPU installed (`nvidia-smi` returns device info)
2. ✅ CUDA 11.8+ installed ([NVIDIA CUDA Toolkit](https://developer.nvidia.com/cuda-toolkit))
3. ✅ CUDA runtime DLLs in PATH (usually automatic; check `PATH` env var)
4. ✅ NuGet package: `Microsoft.ML.OnnxRuntimeGenAI.Cuda` (not CPU package)
5. ✅ NO mixed packages: Don't reference both `Microsoft.ML.OnnxRuntimeGenAI` and `.Cuda` simultaneously

**Verify CUDA is working:**
```csharp
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cuda  // Fail fast if CUDA unavailable
});

Console.WriteLine($"Using GPU: {client.ModelInfo?.ExecutionProvider}");
```

**Common issues:**
- **"CUDA not found"** → Install CUDA 11.8+, add to PATH, restart IDE
- **"Conflicting packages"** → Remove `Microsoft.ML.OnnxRuntimeGenAI` (CPU) if you have `.Cuda`
- **"Out of memory"** → GPU memory exhausted; use smaller model or `ExecutionProvider.Cpu`

---

### DirectML (Windows AMD/Intel Arc)

**Checklist:**
1. ✅ Windows 10/11
2. ✅ AMD Radeon, Intel Arc, or newer NVIDIA GPU
3. ✅ DirectML runtime installed (usually built-in on Windows 11)
4. ✅ NuGet package: `Microsoft.ML.OnnxRuntimeGenAI.DirectML` (not CPU package)
5. ✅ NO mixed packages

**Verify DirectML:**
```csharp
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.DirectML
});

Console.WriteLine($"Using GPU: {client.ModelInfo?.ExecutionProvider}");
```

**Common issues:**
- **"DirectML not available"** → Windows 11 has it built-in; Windows 10 may need update
- **Performance degradation** → Try AMD/Intel driver updates
- **Multi-GPU conflicts** → Use `GpuDeviceId` to select specific GPU

---

### CPU (Default Fallback)

**Recommended when:**
- GPU not available
- Testing/development without hardware
- Reproducible cross-platform behavior

```csharp
var options = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cpu
};
using var client = await LocalChatClient.CreateAsync(options);
```

**Performance tips:**
- Reduce `MaxSequenceLength` (default: 2048)
- Use smaller models (Tiny tier: 0.5B–2B)
- Enable multi-threading: set `OMP_NUM_THREADS` environment variable

---

## Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `ExecutionProviderException` | GPU not available or incompatible | Use `ExecutionProvider.Auto` to fallback to CPU, or install required GPU drivers |
| `ModelCapacityExceededException` | Prompt too long for model's context window | Truncate prompt or use larger model with higher `MaxSequenceLength` |
| `FileNotFoundException` (model download) | Model files missing after download attempt | Check `CacheDirectory` setting; ensure HuggingFace repo is public or `HF_TOKEN` is set |
| `OutOfMemoryException` | System RAM exhausted | Reduce model size, close other applications, increase virtual memory |
| `DivideByZeroException` in generation | Invalid temperature/top-p values | Ensure `Temperature > 0` and `0 < TopP < 1` |
| `InvalidOperationException` on `.GetResponseAsync()` | Model not initialized or disposed | Ensure `LocalChatClient` is created before use; don't call after `Dispose()` |

---

## Package Conflicts

### ❌ DO NOT do this:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI" Version="0.7.0" />
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.Cuda" Version="0.7.0" />
</ItemGroup>
```

**Why:** Native binaries conflict. CUDA version will fail silently; CPU will be used instead.

### ✅ DO this:

**For CPU-only:**
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI" Version="0.7.0" />
</ItemGroup>
```

**For CUDA:**
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.Cuda" Version="0.7.0" />
</ItemGroup>
```

**For DirectML:**
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.DirectML" Version="0.7.0" />
</ItemGroup>
```

---

## Model Capacity Issues

### MaxSequenceLength vs ConfigMaxSequenceLength

- **`MaxSequenceLength`** (LocalLLMsOptions) — Max tokens the library will generate. Limits output length.
- **`ConfigMaxSequenceLength`** (ModelInfo) — The model's actual context window. Total tokens = input + output.

**Scenario: "Prompt too long"**
```csharp
using var client = await LocalChatClient.CreateAsync();

var modelMaxTokens = client.ModelInfo?.ConfigMaxSequenceLength ?? 2048;
var systemPromptTokens = 50;  // Estimate
var userPromptTokens = 4000;   // Count tokens in user input
var maxGenerationTokens = modelMaxTokens - systemPromptTokens - userPromptTokens;

if (maxGenerationTokens < 100)
{
    // Truncate user input or use larger model
    Console.WriteLine($"Model capacity exceeded. Use {modelMaxTokens}-token model.");
}
```

**Solutions:**
1. **Truncate input** — Shorten user message or RAG context
2. **Use larger model** — Phi-4 (14B) has 4K tokens vs Phi-3.5-mini (3.8B) with 2K
3. **Reduce MaxSequenceLength** — Accept shorter responses
4. **Increase model's MaxSequenceLength** — If supported by model architecture

---

## Performance Tips

### Model Selection by Hardware

| Hardware | Recommended Model | RAM | Speed | Quality |
|----------|-------------------|-----|-------|---------|
| RPi/IoT | Qwen2.5-0.5B | 1 GB | 🚀 | ⭐ |
| Laptop (8GB) | Phi-3.5-mini | 6 GB | ⚡⚡ | ⭐⭐⭐ |
| Desktop (16GB) | Phi-4 | 12 GB | ⚡ | ⭐⭐⭐⭐ |
| Workstation (32GB) | Qwen2.5-7B | 8 GB | ⚡ | ⭐⭐⭐⭐ |
| Multi-GPU (CUDA) | Llama-3.3-70B | 40+ GB | 🐢 | ⭐⭐⭐⭐⭐ |

### CPU-Only Optimization

```csharp
// 1. Use smaller model
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct,
    ExecutionProvider = ExecutionProvider.Cpu,
    MaxSequenceLength = 1024  // Reduce context
};

// 2. Set environment variables
Environment.SetEnvironmentVariable("OMP_NUM_THREADS", "4");  // Match CPU cores
Environment.SetEnvironmentVariable("OMP_WAIT_POLICY", "active");

using var client = await LocalChatClient.CreateAsync(options);
```

### Memory-Constrained Environments

```csharp
// Streaming reduces peak memory usage
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    // Process tokens as they arrive, don't buffer full response
    ProcessToken(update.Text);
}

// vs. blocking call (buffers entire response)
var response = await client.GetResponseAsync(messages);  // Higher peak memory
```

### GPU Memory Management

```csharp
// Explicit device selection for multi-GPU setups
var options = new LocalLLMsOptions
{
    ExecutionProvider = ExecutionProvider.Cuda,
    GpuDeviceId = 0  // Use GPU 0
};

// Dispose properly to release GPU memory
using var client = await LocalChatClient.CreateAsync(options);
{
    var response = await client.GetResponseAsync(messages);
}
// GPU memory released here
```

---

## Advanced Diagnostics

### Check ExecutionProvider at Runtime

```csharp
using var client = await LocalChatClient.CreateAsync();

var metadata = client.Metadata;
var modelInfo = client.ModelInfo;

Console.WriteLine($"Model: {modelInfo?.ModelName}");
Console.WriteLine($"Provider: {modelInfo?.ExecutionProvider ?? "Unknown"}");
Console.WriteLine($"Max context: {modelInfo?.ConfigMaxSequenceLength ?? 0} tokens");
```

### Enable Detailed Logging

```csharp
// Use ILogger for diagnostics
var services = new ServiceCollection();
services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddLocalLLMs(options => { /* ... */ });

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatClient>();
```

### Benchmark Model Performance

```csharp
var stopwatch = System.Diagnostics.Stopwatch.StartNew();

var response = await client.GetResponseAsync([
    new(ChatRole.User, "Explain machine learning in one sentence.")
]);

stopwatch.Stop();
Console.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Tokens/sec: {response.Content[0].Text.Split(' ').Length / (stopwatch.ElapsedMilliseconds / 1000.0):F2}");
```

---

## Still Stuck?

1. **Check the logs** — Enable verbose logging to see internal errors
2. **Try Auto provider** — `ExecutionProvider.Auto` falls back safely
3. **Test with tiny model** — Isolate issues: Qwen2.5-0.5B is fastest to download/test
4. **Verify environment** — `nvidia-smi` (CUDA), `dotnet --info` (.NET version)
5. **Open an issue** — [GitHub Issues](https://github.com/elbruno/ElBruno.LocalLLMs/issues) with:
   - .NET version
   - GPU/CPU specs
   - Error message and stack trace
   - Sample code reproducing the issue
