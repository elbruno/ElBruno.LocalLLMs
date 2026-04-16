# Architecture Decision: ElBruno.LocalLLMs.BitNet

**Date:** 2026-04-17  
**Author:** Morpheus (Lead/Architect)  
**Status:** Proposed  
**Requested by:** Bruno Capuano

---

## Context

Bruno has decided that BitNet support belongs under the `ElBruno.LocalLLMs` umbrella as `ElBruno.LocalLLMs.BitNet`. This package must implement `IChatClient` from Microsoft.Extensions.AI, but uses bitnet.cpp (a fork of llama.cpp with custom ternary kernels) for inference — NOT ONNX Runtime GenAI.

This document records the five architectural decisions required to ship this package.

---

## Decision 1: Native Interop Strategy — Option C (Direct P/Invoke to llama.cpp C API via bitnet.cpp)

### Options Evaluated

| Option | Verdict | Rationale |
|--------|---------|-----------|
| **A: Direct P/Invoke to bitnet.cpp** | Viable but higher-effort | Would require defining a custom C wrapper around bitnet.cpp's C++ internals. bitnet.cpp doesn't expose its own `bitnet.h` — it extends llama.cpp's existing C API. |
| **B: LLamaSharp NuGet** | **Rejected** | LLamaSharp wraps mainline llama.cpp, which does NOT support ternary 1.58-bit weights or BitNet kernel types (TL1/TL2/I2_S). The two ecosystems are incompatible as of 2025. bitnet.cpp's fork introduces custom kernels that LLamaSharp cannot load. |
| **C: P/Invoke to llama.cpp C API (as extended by bitnet.cpp)** | **Selected** | bitnet.cpp IS a llama.cpp fork. It builds a shared library (`libllama.so` / `llama.dll`) that exposes the same llama.h entry points — `llama_load_model_from_file`, `llama_new_context_with_model`, `llama_decode`, `llama_token_to_piece`, etc. The difference is that the compiled binary contains ternary kernel implementations. This means we P/Invoke to the standard llama.h C API, but link against a bitnet.cpp-compiled native binary. |

### Selected: Option C — P/Invoke to llama.h C API (bitnet.cpp-compiled binary)

**Key insight:** bitnet.cpp doesn't change the API surface. It changes the implementation behind the same entry points. The user compiles bitnet.cpp (or downloads a prebuilt binary) and we P/Invoke to the same `llama_model_load_from_file`, `llama_new_context_with_model`, `llama_decode` functions.

### Native Library Distribution

**Phase 1 (MVP): User-provided native library.**

The user is responsible for:
1. Building bitnet.cpp from source (`cmake --build` produces `llama.dll` / `libllama.so` / `libllama.dylib`)
2. OR downloading a prebuilt binary from the BitNet GitHub releases
3. Placing it on a known path or setting an environment variable

The `BitNetOptions.NativeLibraryPath` property tells our library where to find it.

**Why not bundle in NuGet?**
- bitnet.cpp requires platform-specific builds with custom kernel selection (TL1 for ARM, TL2 for x86, I2_S for both)
- The build matrix is large: {win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64} × {kernel-type}
- bitnet.cpp is still actively evolving (research-stage) — pinning a binary creates maintenance burden
- ONNX Runtime solved this with a massive engineering team shipping runtime packages. We don't have that luxury.

**Phase 2 (Future): Separate native NuGet packages.**

If demand warrants it, we can publish platform-specific packages following the ONNX Runtime pattern:

```
ElBruno.LocalLLMs.BitNet.Native.win-x64
ElBruno.LocalLLMs.BitNet.Native.linux-x64
ElBruno.LocalLLMs.BitNet.Native.osx-arm64
```

Each would contain:
```
runtimes/{rid}/native/llama.dll  (or libllama.so / libllama.dylib)
```

This is the same pattern used by `Microsoft.ML.OnnxRuntime` (managed wrapper + separate `Microsoft.ML.OnnxRuntime.Gpu` / `.DirectML` native packages). But this is NOT Phase 1 scope.

### Cross-Platform Story

| Platform | Native Binary | Kernel | Notes |
|----------|---------------|--------|-------|
| win-x64 | `llama.dll` | TL2 (x86) or I2_S | Most common dev environment |
| linux-x64 | `libllama.so` | TL2 (x86) or I2_S | Server/cloud deployments |
| osx-arm64 | `libllama.dylib` | TL1 (ARM) or I2_S | Apple Silicon MacBooks |
| win-arm64 | `llama.dll` | TL1 (ARM) or I2_S | Surface/Snapdragon devices |
| linux-arm64 | `libllama.so` | TL1 (ARM) or I2_S | Raspberry Pi, embedded |

The P/Invoke declarations use `[DllImport("llama")]` — .NET's native library loader resolves this per-platform.

### User Installation Flow

```bash
# 1. Install the managed NuGet package
dotnet add package ElBruno.LocalLLMs.BitNet

# 2. Build bitnet.cpp native library (one-time)
git clone https://github.com/microsoft/BitNet.git
cd BitNet
pip install -r requirements.txt
python setup_env.py --hf-repo microsoft/BitNet-b1.58-2B-4T-gguf -q i2_s

# 3. Configure path in code
var options = new BitNetOptions
{
    NativeLibraryPath = @"C:\path\to\bitnet\build\bin\Release",
    Model = BitNetKnownModels.BitNet2B4T,
    ModelPath = @"C:\models\BitNet-b1.58-2B-4T\ggml-model-i2_s.gguf"
};
```

---

## Decision 2: API Surface Design

### 2.1 `BitNetChatClient : IChatClient, IAsyncDisposable`

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Local BitNet chat client using bitnet.cpp (llama.cpp fork).
/// Implements IChatClient for seamless MEAI integration.
/// </summary>
public sealed class BitNetChatClient : IChatClient, IAsyncDisposable
{
    // --- Construction ---

    /// <summary>
    /// Creates a BitNetChatClient with the specified options.
    /// Loads the native library and model immediately.
    /// </summary>
    public BitNetChatClient(BitNetOptions options);

    /// <summary>
    /// Creates a BitNetChatClient with options and a logger factory.
    /// </summary>
    public BitNetChatClient(BitNetOptions options, ILoggerFactory? loggerFactory);

    /// <summary>
    /// Async factory for DI/hosted scenarios. Validates native lib
    /// and loads model without blocking.
    /// </summary>
    public static Task<BitNetChatClient> CreateAsync(
        BitNetOptions options,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default);

    // --- IChatClient ---

    /// <summary>
    /// Non-streaming chat completion.
    /// Formats messages via chat template, runs inference via bitnet.cpp,
    /// returns the full response.
    /// </summary>
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming chat completion.
    /// Tokens are yielded as they are generated by bitnet.cpp.
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns metadata about the loaded model and runtime.
    /// </summary>
    public ChatClientMetadata Metadata { get; }

    // --- IAsyncDisposable ---
    public ValueTask DisposeAsync();
}
```

**How it loads the native library:**
1. If `BitNetOptions.NativeLibraryPath` is set, prepend it to `PATH` (Windows) or `LD_LIBRARY_PATH` (Linux) / `DYLD_LIBRARY_PATH` (macOS) before first P/Invoke call
2. Use `NativeLibrary.SetDllImportResolver` for the `"llama"` library name to probe custom paths
3. First P/Invoke call validates the library is reachable; throws `BitNetNativeLibraryException` if not

**How it maps to IChatClient:**
- `GetResponseAsync`: Format messages → tokenize → run full generation loop → detokenize → return `ChatResponse`
- `GetStreamingResponseAsync`: Same, but yield each token as a `ChatResponseUpdate` in a decode loop

### 2.2 `BitNetOptions`

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Configuration options for BitNetChatClient.
/// </summary>
public sealed class BitNetOptions
{
    /// <summary>
    /// The model definition from the BitNet catalog.
    /// Default: BitNetKnownModels.BitNet2B4T.
    /// </summary>
    public BitNetModelDefinition Model { get; set; } = BitNetKnownModels.BitNet2B4T;

    /// <summary>
    /// Path to the GGUF model file.
    /// When set, skips any automatic resolution.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Path to the directory containing the bitnet.cpp native library
    /// (llama.dll / libllama.so / libllama.dylib).
    /// If null, searches PATH / LD_LIBRARY_PATH / default locations.
    /// </summary>
    public string? NativeLibraryPath { get; set; }

    /// <summary>
    /// Maximum tokens to generate. Default: 2048.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for sampling. Default: 0.7.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Top-p nucleus sampling. Default: 0.9.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    /// Top-k sampling. Default: 40.
    /// </summary>
    public int TopK { get; set; } = 40;

    /// <summary>
    /// Repetition penalty. Default: 1.1.
    /// </summary>
    public float RepetitionPenalty { get; set; } = 1.1f;

    /// <summary>
    /// Number of CPU threads for inference.
    /// Default: Environment.ProcessorCount.
    /// </summary>
    public int ThreadCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Context window size in tokens. Default: 4096.
    /// </summary>
    public int ContextSize { get; set; } = 4096;

    /// <summary>
    /// Optional system prompt prepended to conversations.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Chat template format for prompt formatting.
    /// Default is resolved from the model definition.
    /// </summary>
    public ChatTemplateFormat? ChatTemplateOverride { get; set; }
}
```

**Parallel to `LocalLLMsOptions`:**

| LocalLLMsOptions | BitNetOptions | Notes |
|------------------|---------------|-------|
| `Model` (ModelDefinition) | `Model` (BitNetModelDefinition) | Different record type (GGUF vs ONNX) |
| `ModelPath` | `ModelPath` | Same semantics |
| `CacheDirectory` | _(not in MVP)_ | Phase 2: auto-download GGUF from HF |
| `EnsureModelDownloaded` | _(not in MVP)_ | Phase 2 |
| `ExecutionProvider` | `ThreadCount` | BitNet is CPU-focused; no GPU provider enum |
| `MaxSequenceLength` | `MaxTokens` + `ContextSize` | Separate concerns for BitNet |
| `Temperature` | `Temperature` | Same |
| `TopP` | `TopP` | Same |
| `SystemPrompt` | `SystemPrompt` | Same |

### 2.3 `BitNetModelDefinition`

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Describes a BitNet model — its source, format, and prompt template.
/// Parallel to ModelDefinition but for GGUF/bitnet.cpp models.
/// </summary>
public sealed record BitNetModelDefinition
{
    /// <summary>Unique identifier (e.g., "bitnet-b1.58-2b-4t").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>HuggingFace repository ID for GGUF download.</summary>
    public required string HuggingFaceRepoId { get; init; }

    /// <summary>
    /// Default GGUF filename within the repo (e.g., "ggml-model-i2_s.gguf").
    /// </summary>
    public required string GgufFileName { get; init; }

    /// <summary>
    /// Chat template format (ChatML, Llama3, etc.).
    /// Reuses the shared ChatTemplateFormat enum.
    /// </summary>
    public required ChatTemplateFormat ChatTemplate { get; init; }

    /// <summary>
    /// Parameter count in billions (e.g., 0.7, 2.4, 3.3, 8.0).
    /// </summary>
    public required double ParametersBillions { get; init; }

    /// <summary>
    /// Context window size supported by this model.
    /// </summary>
    public int ContextLength { get; init; } = 4096;

    /// <summary>
    /// Approximate model file size in MB.
    /// </summary>
    public int ApproximateSizeMB { get; init; }

    /// <summary>
    /// Recommended BitNet kernel type for optimal performance.
    /// </summary>
    public BitNetKernelType RecommendedKernel { get; init; } = BitNetKernelType.I2_S;
}
```

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// BitNet kernel types for ternary weight computation.
/// </summary>
public enum BitNetKernelType
{
    /// <summary>Integer 2-bit signed — universal, works on all platforms.</summary>
    I2_S,
    /// <summary>Table Lookup 1 — optimized for ARM (Apple Silicon, Snapdragon).</summary>
    TL1,
    /// <summary>Table Lookup 2 — optimized for x86 (Intel, AMD).</summary>
    TL2
}
```

### 2.4 `BitNetKnownModels`

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Pre-defined BitNet model catalog.
/// </summary>
public static class BitNetKnownModels
{
    /// <summary>
    /// Microsoft BitNet b1.58 2B-4T — the official flagship model.
    /// 2.4B params, trained on 4T tokens, MIT license.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet2B4T = new()
    {
        Id = "bitnet-b1.58-2b-4t",
        DisplayName = "BitNet b1.58 2B-4T",
        HuggingFaceRepoId = "microsoft/BitNet-b1.58-2B-4T-gguf",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 2.4,
        ContextLength = 4096,
        ApproximateSizeMB = 400
    };

    /// <summary>
    /// 1BitLLM bitnet_b1_58-large — community 0.7B model.
    /// Smallest BitNet model, good for testing/prototyping.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet07B = new()
    {
        Id = "bitnet-b1.58-0.7b",
        DisplayName = "BitNet b1.58 0.7B",
        HuggingFaceRepoId = "1bitLLM/bitnet_b1_58-large",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 0.7,
        ContextLength = 2048,
        ApproximateSizeMB = 150
    };

    /// <summary>
    /// 1BitLLM bitnet_b1_58-3B — community 3.3B model.
    /// Larger community model for better quality.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet3B = new()
    {
        Id = "bitnet-b1.58-3b",
        DisplayName = "BitNet b1.58 3B",
        HuggingFaceRepoId = "1bitLLM/bitnet_b1_58-3B",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 3.3,
        ContextLength = 4096,
        ApproximateSizeMB = 650
    };

    /// <summary>
    /// Falcon3 1B Instruct 1.58-bit — instruction-tuned, smallest Falcon.
    /// </summary>
    public static readonly BitNetModelDefinition Falcon3_1B = new()
    {
        Id = "falcon3-1b-instruct-1.58bit",
        DisplayName = "Falcon3 1B Instruct 1.58-bit",
        HuggingFaceRepoId = "tiiuae/Falcon3-1B-Instruct-1.58bit",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.ChatML,
        ParametersBillions = 1.0,
        ContextLength = 8192,
        ApproximateSizeMB = 200
    };

    /// <summary>
    /// Falcon3 3B Instruct 1.58-bit — instruction-tuned, mid-tier Falcon.
    /// </summary>
    public static readonly BitNetModelDefinition Falcon3_3B = new()
    {
        Id = "falcon3-3b-instruct-1.58bit",
        DisplayName = "Falcon3 3B Instruct 1.58-bit",
        HuggingFaceRepoId = "tiiuae/Falcon3-3B-Instruct-1.58bit",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.ChatML,
        ParametersBillions = 3.0,
        ContextLength = 8192,
        ApproximateSizeMB = 600
    };

    /// <summary>Returns all known BitNet model definitions.</summary>
    public static IReadOnlyList<BitNetModelDefinition> All { get; } =
    [
        BitNet2B4T,
        BitNet07B,
        BitNet3B,
        Falcon3_1B,
        Falcon3_3B
    ];

    /// <summary>Finds a model by its ID string. Returns null if not found.</summary>
    public static BitNetModelDefinition? FindById(string modelId) =>
        All.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
}
```

### 2.5 `BitNetServiceExtensions`

```csharp
namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// DI registration for BitNetChatClient.
/// </summary>
public static class BitNetServiceExtensions
{
    /// <summary>
    /// Registers IChatClient backed by BitNetChatClient with default options.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(this IServiceCollection services)
        => services.AddBitNetChatClient(_ => { });

    /// <summary>
    /// Registers IChatClient backed by BitNetChatClient with configured options.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(
        this IServiceCollection services,
        Action<BitNetOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BitNetOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<BitNetOptions>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new BitNetChatClient(opts, loggerFactory);
        });

        return services;
    }
}
```

**DI usage:**
```csharp
builder.Services.AddBitNetChatClient(options =>
{
    options.Model = BitNetKnownModels.BitNet2B4T;
    options.ModelPath = @"C:\models\bitnet-2b\ggml-model-i2_s.gguf";
    options.NativeLibraryPath = @"C:\bitnet\build\bin\Release";
    options.ThreadCount = 8;
});
```

---

## Decision 3: Chat Template Integration — Shared Source Package (Internal)

### The Problem

BitNet models use standard chat templates (Llama3, ChatML, etc.) — the same ones our core library already implements. We have two options:

| Option | Pros | Cons |
|--------|------|------|
| **A: ProjectReference to core library** | Zero duplication | BitNet package drags in ONNX Runtime GenAI dependency. Users who only want BitNet now need ONNX Runtime. Types like `ModelDefinition`, `OnnxModelType`, `ExecutionProvider` leak into the public surface. |
| **B: Duplicate template code** | Full independence | Maintenance burden. Bug fixes must be applied twice. |
| **C: Extract shared types into `ElBruno.LocalLLMs.Shared`** | Clean separation, reuse | New package to maintain. Over-engineering for ~6 files. |
| **D: Shared source via internal package or source files** | No runtime dependency. Code reuse. | Slightly unusual pattern. |

### Selected: Option A (ProjectReference) with PrivateAssets

**Rationale:**

The core library (`ElBruno.LocalLLMs`) already lists `Microsoft.ML.OnnxRuntimeGenAI` with `PrivateAssets="native"`, meaning it only needs the managed assembly for compilation — native binaries are excluded. More importantly, the chat template types (`ChatTemplateFormat` enum, `IChatTemplateFormatter` interface, and the 7 formatter implementations) are ALL internal. They're not part of the public API surface.

The `BitNet` project references the core library and gets access to:
- `ChatTemplateFormat` enum (public) — ✅ this is the only type we need publicly
- `IChatTemplateFormatter` + formatters (internal) — accessed via `InternalsVisibleTo`

**What does NOT leak:**
- `ModelDefinition` — BitNet uses its own `BitNetModelDefinition`
- `OnnxModelType`, `ExecutionProvider` — these are public in the core but users of the BitNet package don't need to reference them directly
- ONNX Runtime GenAI native binaries — already excluded via `PrivateAssets="native"`

**The trade-off:** BitNet consumers will transitively reference `Microsoft.ML.OnnxRuntimeGenAI` managed assembly. This is a ~2MB managed DLL with no native binaries (those are excluded). This is acceptable because:
1. The managed assembly is tiny
2. No native ONNX Runtime DLLs are pulled
3. We avoid duplicating 7 template formatters and their test suites

**If this becomes unacceptable** in the future (e.g., users complain about the transitive reference), we extract a `ElBruno.LocalLLMs.Abstractions` package containing `ChatTemplateFormat`, formatters, and shared types. But not now — YAGNI.

### Project File Snippet

```xml
<!-- ElBruno.LocalLLMs.BitNet.csproj -->
<ItemGroup>
  <ProjectReference Include="..\ElBruno.LocalLLMs\ElBruno.LocalLLMs.csproj"
                    PrivateAssets="all" />
</ItemGroup>
```

Wait — `PrivateAssets="all"` would prevent the core library's types from flowing to BitNet consumers. We need the `ChatTemplateFormat` enum to be visible. Revised:

```xml
<!-- ElBruno.LocalLLMs.BitNet.csproj -->
<ItemGroup>
  <ProjectReference Include="..\ElBruno.LocalLLMs\ElBruno.LocalLLMs.csproj" />
</ItemGroup>
```

The standard ProjectReference is correct. The ONNX native binaries are already excluded by the core project's own `PrivateAssets="native"` on its ORT reference.

### InternalsVisibleTo

Add to `ElBruno.LocalLLMs.csproj`:
```xml
<InternalsVisibleTo Include="ElBruno.LocalLLMs.BitNet" />
```

This gives BitNet access to `ChatTemplateFactory.Create()` and all formatter implementations.

---

## Decision 4: Model Catalog

### Available BitNet Models (2025)

Research identified these models suitable for local inference, ordered by size:

| Model | Params | HuggingFace ID | Size (GGUF) | Chat Template | License | Notes |
|-------|--------|----------------|-------------|---------------|---------|-------|
| **bitnet_b1_58-large** | 0.7B | `1bitLLM/bitnet_b1_58-large` | ~150 MB | Llama3 | MIT | Smallest. Good for testing. |
| **Falcon3-1B-Instruct-1.58bit** | 1.0B | `tiiuae/Falcon3-1B-Instruct-1.58bit` | ~200 MB | ChatML | Apache 2.0 | Instruction-tuned, 8K context |
| **BitNet b1.58 2B-4T** | 2.4B | `microsoft/BitNet-b1.58-2B-4T-gguf` | ~400 MB | Llama3 | MIT | Official Microsoft model. Best tested. **Recommended default.** |
| **bitnet_b1_58-3B** | 3.3B | `1bitLLM/bitnet_b1_58-3B` | ~650 MB | Llama3 | MIT | Community model, larger capacity |
| **Falcon3-3B-Instruct-1.58bit** | 3.0B | `tiiuae/Falcon3-3B-Instruct-1.58bit` | ~600 MB | ChatML | Apache 2.0 | Instruction-tuned |
| Falcon3-7B-Instruct-1.58bit | 7.0B | `tiiuae/Falcon3-7B-Instruct-1.58bit` | ~1.4 GB | ChatML | Apache 2.0 | Larger, may not fit edge |
| Falcon3-10B-Instruct-1.58bit | 10.0B | `tiiuae/Falcon3-10B-Instruct-1.58bit` | ~2 GB | ChatML | Apache 2.0 | Largest, desktop only |
| Llama3-8B-1.58-100B-tokens | 8.0B | `HF1BitLLM/Llama3-8B-1.58-100B-tokens` | ~1.5 GB | Llama3 | Meta | Research model |

### Phase 1 Catalog (3 models)

For the MVP, include the **smallest 3 models** suitable for local inference plus the flagship:

1. **BitNet b1.58 0.7B** — smallest, testing/prototyping
2. **Falcon3 1B Instruct 1.58-bit** — smallest instruction-tuned
3. **BitNet b1.58 2B-4T** — official Microsoft flagship (**default**)
4. **BitNet b1.58 3B** — best community quality
5. **Falcon3 3B Instruct 1.58-bit** — largest instruction-tuned in MVP

Phase 2 can add Falcon3-7B, Falcon3-10B, and the Llama3-8B models.

### Why BitNet 2B-4T as Default

- Official Microsoft model (aligned with our MEAI focus)
- MIT license (no friction)
- Best tested with bitnet.cpp (it's literally the reference model)
- 400 MB is small enough for most developer machines
- 4096 context is sufficient for most chat scenarios

---

## Decision 5: NuGet Package Structure

### Package Layout

```
ElBruno.LocalLLMs.BitNet.nupkg
├── lib/
│   ├── net8.0/
│   │   └── ElBruno.LocalLLMs.BitNet.dll
│   └── net10.0/
│       └── ElBruno.LocalLLMs.BitNet.dll
├── nuget_logo.png
└── README.md
```

**No native libraries bundled.** Phase 1 is managed-only. The user provides the bitnet.cpp native binary.

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>ElBruno.LocalLLMs.BitNet</PackageId>
    <Description>BitNet 1-bit LLM chat client for Microsoft.Extensions.AI. IChatClient implementation using bitnet.cpp for local 1.58-bit model inference.</Description>
    <PackageProjectUrl>https://github.com/elbruno/ElBruno.LocalLLMs</PackageProjectUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>bitnet;1bit;llm;ai;local;chat;microsoft-extensions-ai;ichatclient;ternary;edge;cpu</PackageTags>
    <Version>0.1.0</Version>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ElBruno.LocalLLMs\ElBruno.LocalLLMs.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.4.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ElBruno.LocalLLMs.BitNet.Tests" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\..\images\nuget_logo.png" Pack="true" PackagePath="" />
  </ItemGroup>
</Project>
```

### Dependency Graph

```
ElBruno.LocalLLMs.BitNet
  ├── ElBruno.LocalLLMs (ProjectReference — for ChatTemplateFormat, formatters)
  │     ├── Microsoft.Extensions.AI.Abstractions 10.4.0
  │     ├── Microsoft.ML.OnnxRuntimeGenAI 0.12.2 (managed only, native excluded)
  │     └── ElBruno.HuggingFace.Downloader 0.6.0
  ├── Microsoft.Extensions.AI.Abstractions 10.4.0
  ├── Microsoft.Extensions.DependencyInjection.Abstractions 10.0.5
  └── Microsoft.Extensions.Logging.Abstractions 9.0.0
```

### Future: Native Package Split

When ready for Phase 2 native distribution:

```
ElBruno.LocalLLMs.BitNet              (managed + P/Invoke declarations)
ElBruno.LocalLLMs.BitNet.Native.win-x64    (runtimes/win-x64/native/llama.dll)
ElBruno.LocalLLMs.BitNet.Native.linux-x64  (runtimes/linux-x64/native/libllama.so)
ElBruno.LocalLLMs.BitNet.Native.osx-arm64  (runtimes/osx-arm64/native/libllama.dylib)
```

This mirrors `Microsoft.ML.OnnxRuntime` (managed) + `Microsoft.ML.OnnxRuntime.Gpu` (native).

---

## Internal Architecture: P/Invoke Layer

### Native Interop Classes (internal)

```csharp
namespace ElBruno.LocalLLMs.BitNet.Native;

/// <summary>
/// P/Invoke declarations for the llama.h C API (bitnet.cpp build).
/// </summary>
internal static partial class LlamaNative
{
    private const string LibraryName = "llama";

    [LibraryImport(LibraryName)]
    internal static partial void llama_backend_init();

    [LibraryImport(LibraryName)]
    internal static partial void llama_backend_free();

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr llama_load_model_from_file(
        string path_model,
        LlamaModelParams @params);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr llama_new_context_with_model(
        IntPtr model,
        LlamaContextParams @params);

    [LibraryImport(LibraryName)]
    internal static partial void llama_free(IntPtr ctx);

    [LibraryImport(LibraryName)]
    internal static partial void llama_free_model(IntPtr model);

    // Tokenization, decode, sampling — additional P/Invoke declarations
    // follow the llama.h API surface
}
```

**Key design choice:** Use `LibraryImport` (source-generated P/Invoke, .NET 7+) instead of `DllImport` for better performance and AOT compatibility.

### SafeHandle Wrappers

```csharp
internal sealed class LlamaModelHandle : SafeHandle { ... }
internal sealed class LlamaContextHandle : SafeHandle { ... }
```

These ensure native resources are freed even if the user forgets to dispose.

---

## Project Structure

```
src/
├── ElBruno.LocalLLMs/                          (existing core)
├── ElBruno.LocalLLMs.BitNet/                   (NEW)
│   ├── ElBruno.LocalLLMs.BitNet.csproj
│   ├── BitNetChatClient.cs
│   ├── BitNetOptions.cs
│   ├── BitNetServiceExtensions.cs
│   ├── Models/
│   │   ├── BitNetModelDefinition.cs
│   │   ├── BitNetKnownModels.cs
│   │   └── BitNetKernelType.cs
│   ├── Native/
│   │   ├── LlamaNative.cs                      (P/Invoke declarations)
│   │   ├── LlamaModelHandle.cs                 (SafeHandle)
│   │   ├── LlamaContextHandle.cs               (SafeHandle)
│   │   ├── LlamaModelParams.cs                 (struct)
│   │   ├── LlamaContextParams.cs               (struct)
│   │   └── NativeLibraryResolver.cs            (DllImportResolver setup)
│   └── Internal/
│       └── BitNetInferenceEngine.cs            (orchestrates tokenize→decode→detokenize)
├── tests/
│   └── ElBruno.LocalLLMs.BitNet.Tests/         (NEW)
│       ├── ElBruno.LocalLLMs.BitNet.Tests.csproj
│       ├── BitNetOptionsTests.cs
│       ├── BitNetModelDefinitionTests.cs
│       └── BitNetKnownModelsTests.cs
```

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| bitnet.cpp API changes (still research-stage) | **High** | Pin to a specific commit/release. Wrap P/Invoke behind internal abstraction layer (`BitNetInferenceEngine`). |
| Native lib build complexity for users | **Medium** | Provide clear docs + scripts. Phase 2: ship prebuilt binaries. |
| ONNX Runtime managed assembly leaks via transitive dependency | **Low** | No native binaries leak. Managed DLL is ~2MB. Extract to `.Abstractions` if users complain. |
| Chat template drift between core and BitNet | **Low** | Single source of truth via ProjectReference + InternalsVisibleTo. |
| Thread safety of bitnet.cpp | **Medium** | llama.cpp's C API is NOT thread-safe per context. Use `SemaphoreSlim` for serialized inference (same pattern as `LocalChatClient`). |

---

## Implementation Phases

### Phase 1: MVP (2-3 weeks)
- [ ] Project scaffolding (`ElBruno.LocalLLMs.BitNet.csproj`, test project)
- [ ] P/Invoke declarations for core llama.h functions
- [ ] SafeHandle wrappers
- [ ] `BitNetOptions`, `BitNetModelDefinition`, `BitNetKnownModels`
- [ ] `BitNetChatClient` with `GetResponseAsync` (non-streaming)
- [ ] `GetStreamingResponseAsync`
- [ ] DI extension methods
- [ ] Unit tests (options validation, model catalog, template integration)
- [ ] Integration test (requires bitnet.cpp binary + model, env-gated)
- [ ] Documentation in `docs/bitnet.md`

### Phase 2: Polish (2 weeks)
- [ ] Auto-download GGUF from HuggingFace (reuse `IModelDownloader`)
- [ ] Native library auto-discovery (search well-known paths)
- [ ] Health check (`BitNetHealthCheck : IHealthCheck`)
- [ ] Benchmarks

### Phase 3: Native Distribution (4 weeks)
- [ ] CI pipeline to build bitnet.cpp for 6 platforms
- [ ] Publish `ElBruno.LocalLLMs.BitNet.Native.{rid}` packages
- [ ] Zero-config experience: `dotnet add package ElBruno.LocalLLMs.BitNet.Native.win-x64`

---

## Summary of Decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Native interop | **Option C**: P/Invoke to llama.h C API via bitnet.cpp-compiled binary |
| 2 | API surface | `BitNetChatClient : IChatClient`, `BitNetOptions`, `BitNetModelDefinition`, DI extensions |
| 3 | Chat templates | **ProjectReference** to core library + `InternalsVisibleTo` for template reuse |
| 4 | Model catalog | 5 models: BitNet 0.7B, Falcon3 1B, **BitNet 2B-4T (default)**, BitNet 3B, Falcon3 3B |
| 5 | Package structure | **Managed-only NuGet** (Phase 1). User provides native lib. Future: separate native packages per RID. |

---

*Morpheus — "I can only show you the door. You're the one that has to walk through it."*
