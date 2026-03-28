# ElBruno.LocalLLMs — Architecture

> **Version:** 1.0 — Initial Architecture  
> **Author:** Morpheus (Lead/Architect)  
> **Date:** 2026-03-17  
> **Status:** Approved for implementation

---

## 1. Design Philosophy

This library exists for one reason: to let any .NET developer drop a local LLM into their app through `IChatClient` — the same interface they'd use for Azure OpenAI, Ollama, or any other provider. If it doesn't plug cleanly into the Microsoft.Extensions.AI ecosystem, it doesn't ship.

**Principles:**
- Composition over inheritance
- Data-driven model registration (models are config, not code)
- Automatic model download — zero friction first-run
- Minimal public API surface — expose what users need, nothing more

---

## 2. Solution Structure Decision

### Decision: Single core package + extension packages by concern (NOT per-model)

After studying both reference repos:

| Pattern | elbruno.localembeddings | ElBruno.QwenTTS |
|---------|------------------------|-----------------|
| Core lib | `ElBruno.LocalEmbeddings` | `ElBruno.QwenTTS.Core` |
| Extensions | `.ImageEmbeddings`, `.Npu`, `.VectorData`, `.KernelMemory` | `.VoiceCloning` |
| Per-model packages? | **No** — model selection via options | **No** — single pipeline |

With 20+ target models, per-model NuGet packages would be noise. The ONNX Runtime GenAI API is model-agnostic — the only model-specific knowledge is configuration data (HuggingFace repo ID, ONNX file paths, chat template format). That's a `ModelDefinition` record, not a separate assembly.

### Projects

```
src/
  ElBruno.LocalLLMs/                    → NuGet: ElBruno.LocalLLMs (core)
  ElBruno.LocalLLMs.SemanticKernel/     → NuGet: ElBruno.LocalLLMs.SemanticKernel (future)
tests/
  ElBruno.LocalLLMs.Tests/              → Unit tests
  ElBruno.LocalLLMs.IntegrationTests/   → Integration tests (require GPU/model)
samples/
  HelloChat/                            → Minimal console chat
  StreamingChat/                        → Streaming token-by-token demo
  MultiModelChat/                       → Switch between models at runtime
  DependencyInjection/                  → ASP.NET Core DI registration
  SemanticKernelChat/                   → SK integration (future)
scripts/
  convert_to_onnx.py                    → HuggingFace → ONNX conversion
  requirements.txt                      → Python dependencies
docs/
  architecture.md                       → This document
```

### Solution File

```xml
<!-- ElBruno.LocalLLMs.slnx -->
<Solution>
  <Folder Name="/src/">
    <Project Path="src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj" />
    <Project Path="tests/ElBruno.LocalLLMs.IntegrationTests/ElBruno.LocalLLMs.IntegrationTests.csproj" />
  </Folder>
  <Folder Name="/samples/">
    <Project Path="samples/HelloChat/HelloChat.csproj" />
    <Project Path="samples/StreamingChat/StreamingChat.csproj" />
    <Project Path="samples/MultiModelChat/MultiModelChat.csproj" />
    <Project Path="samples/DependencyInjection/DependencyInjection.csproj" />
  </Folder>
</Solution>
```

---

## 3. Public API Surface

### 3.1 The IChatClient Implementation

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Local LLM chat client using ONNX Runtime GenAI.
/// Implements IChatClient for seamless integration with Microsoft.Extensions.AI.
/// </summary>
public sealed class LocalChatClient : IChatClient, IAsyncDisposable
{
    // --- Construction ---

    /// <summary>
    /// Creates a LocalChatClient with default options (Phi-3.5-mini-instruct).
    /// Model is downloaded automatically on first use.
    /// </summary>
    public LocalChatClient();

    /// <summary>
    /// Creates a LocalChatClient with the specified options.
    /// </summary>
    public LocalChatClient(LocalLLMsOptions options);

    /// <summary>
    /// Async factory — preferred in async contexts to avoid sync-over-async during model download.
    /// </summary>
    public static Task<LocalChatClient> CreateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Async factory with options and progress reporting.
    /// </summary>
    public static Task<LocalChatClient> CreateAsync(
        LocalLLMsOptions options,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    // --- IChatClient implementation ---

    /// <inheritdoc />
    public ChatClientMetadata Metadata { get; }

    /// <inheritdoc />
    public Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class;

    // --- Lifecycle ---
    public void Dispose();
    public ValueTask DisposeAsync();
}
```

### 3.2 Options

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Configuration options for LocalChatClient.
/// </summary>
public sealed class LocalLLMsOptions
{
    /// <summary>
    /// The model to use. Provides HuggingFace repo, ONNX paths, and chat template.
    /// Default: KnownModel.Phi35MiniInstruct.
    /// </summary>
    public ModelDefinition Model { get; set; } = KnownModels.Phi35MiniInstruct;

    /// <summary>
    /// Path to a local model directory. When set, skips download entirely.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Custom directory for model cache.
    /// Default: %LOCALAPPDATA%/ElBruno/LocalLLMs/models
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Whether to auto-download the model if not cached. Default: true.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Execution provider selection. Default: Auto (CUDA, then DirectML, then CPU).
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; set; } = ExecutionProvider.Auto;

    /// <summary>
    /// GPU device ID for CUDA/DirectML. Default: 0.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;

    /// <summary>
    /// Maximum sequence length for generation. Default: 2048.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 2048;

    /// <summary>
    /// Default temperature for generation. Default: 0.7.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Default top-p for generation. Default: 0.9.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    /// Optional custom session options factory. Overrides ExecutionProvider.
    /// </summary>
    public Func<Microsoft.ML.OnnxRuntime.SessionOptions>? SessionOptionsFactory { get; set; }
}
```

### 3.3 Execution Provider

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Selects the hardware execution provider for ONNX Runtime.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>Auto detection: tries CUDA → DirectML → CPU (default).</summary>
    Auto,

    /// <summary>CPU execution (works everywhere).</summary>
    Cpu,

    /// <summary>NVIDIA CUDA GPU acceleration.</summary>
    Cuda,

    /// <summary>Windows DirectML GPU acceleration (AMD, Intel, NVIDIA).</summary>
    DirectML
}
```

### 3.4 Model Download Progress

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Reports model download progress.
/// </summary>
public readonly record struct ModelDownloadProgress(
    string FileName,
    long BytesDownloaded,
    long TotalBytes,
    double PercentComplete);
```

---

## 4. Model Abstraction

### 4.1 Model Definition (Data, Not Code)

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Describes everything needed to download, load, and use a specific LLM.
/// Models are data — adding a model means adding a record, not a class.
/// </summary>
public sealed record ModelDefinition
{
    /// <summary>Unique identifier for this model (e.g., "phi-3.5-mini-instruct").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>HuggingFace repository ID for download.</summary>
    public required string HuggingFaceRepoId { get; init; }

    /// <summary>
    /// Required files to download from the repo.
    /// Relative paths within the HuggingFace repo (e.g., "onnx/model.onnx").
    /// </summary>
    public required string[] RequiredFiles { get; init; }

    /// <summary>
    /// Optional files to attempt downloading (e.g., tokenizer configs).
    /// </summary>
    public string[] OptionalFiles { get; init; } = [];

    /// <summary>
    /// The ONNX GenAI model type for loading.
    /// </summary>
    public required OnnxModelType ModelType { get; init; }

    /// <summary>
    /// Chat template format (determines how messages are formatted).
    /// </summary>
    public required ChatTemplateFormat ChatTemplate { get; init; }

    /// <summary>Approximate model size category.</summary>
    public ModelTier Tier { get; init; } = ModelTier.Small;

    /// <summary>Whether this model has native ONNX weights on HuggingFace.</summary>
    public bool HasNativeOnnx { get; init; }
}

/// <summary>ONNX model loading strategy.</summary>
public enum OnnxModelType
{
    /// <summary>Standard causal language model (decoder-only).</summary>
    CausalLM,

    /// <summary>ONNX Runtime GenAI model (uses GenAI API directly).</summary>
    GenAI
}

/// <summary>Chat template formatting standard.</summary>
public enum ChatTemplateFormat
{
    ChatML,
    Llama3,
    Phi3,
    Gemma,
    Mistral,
    Qwen,
    DeepSeek,
    Custom
}

/// <summary>Model size tier for documentation/filtering.</summary>
public enum ModelTier
{
    /// <summary>≤2B params — edge, IoT, fast prototyping.</summary>
    Tiny,

    /// <summary>3-4B params — best quality/size ratio, recommended starting point.</summary>
    Small,

    /// <summary>7-24B params — production quality local inference.</summary>
    Medium,

    /// <summary>32B+ params — heavy workloads, multi-GPU.</summary>
    Large
}
```

### 4.2 Known Models Registry

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Pre-defined model definitions for supported LLMs.
/// These are the models the library knows how to download, configure, and run.
/// </summary>
public static class KnownModels
{
    // --- ⚪ Tiny ---
    public static readonly ModelDefinition Qwen25_05BInstruct = new()
    {
        Id = "qwen2.5-0.5b-instruct",
        DisplayName = "Qwen2.5-0.5B-Instruct",
        HuggingFaceRepoId = "Qwen/Qwen2.5-0.5B-Instruct",
        RequiredFiles = ["onnx/model.onnx"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = false
    };

    // --- 🟢 Small (recommended starting point) ---
    public static readonly ModelDefinition Phi35MiniInstruct = new()
    {
        Id = "phi-3.5-mini-instruct",
        DisplayName = "Phi-3.5 mini instruct",
        HuggingFaceRepoId = "microsoft/Phi-3.5-mini-instruct-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Small,
        HasNativeOnnx = true
    };

    // --- 🟡 Medium ---
    public static readonly ModelDefinition Phi4 = new()
    {
        Id = "phi-4",
        DisplayName = "Phi-4",
        HuggingFaceRepoId = "microsoft/phi-4-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>
    /// Returns all known model definitions.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> All { get; } = [
        Qwen25_05BInstruct,
        Phi35MiniInstruct,
        Phi4,
        // Additional models added as converted/validated
    ];

    /// <summary>
    /// Finds a model by its ID string. Returns null if not found.
    /// </summary>
    public static ModelDefinition? FindById(string modelId);
}
```

### 4.3 Model Lifecycle

```
User creates LocalChatClient(options)
       │
       ▼
┌─ Is ModelPath set? ──► YES ──► Load from local path
│       │
│      NO
│       │
│       ▼
│  Is model cached? ──► YES ──► Load from cache
│       │
│      NO
│       │
│       ▼
│  EnsureModelDownloaded? ──► NO ──► throw InvalidOperationException
│       │
│      YES
│       │
│       ▼
│  Download via ElBruno.HuggingFace.Downloader
│  (RequiredFiles + OptionalFiles from ModelDefinition)
│       │
│       ▼
│  Cache to: {CacheDirectory}/{model-id}/
│       │
│       ▼
└──────► Initialize ONNX Runtime GenAI session
         │
         ▼
    Ready for CompleteAsync / CompleteStreamingAsync
```

---

## 5. Internal Architecture

### 5.1 Chat Template Engine

```csharp
namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Formats IList<ChatMessage> into the model's expected prompt format.
/// </summary>
internal interface IChatTemplateFormatter
{
    string FormatMessages(IList<ChatMessage> messages);
}

/// <summary>
/// Resolves the correct formatter based on ChatTemplateFormat.
/// </summary>
internal static class ChatTemplateFactory
{
    internal static IChatTemplateFormatter Create(ChatTemplateFormat format);
}
```

### 5.2 Model Downloader

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Downloads and caches ONNX models from HuggingFace.
/// Uses ElBruno.HuggingFace.Downloader internally.
/// </summary>
public interface IModelDownloader
{
    Task<string> EnsureModelAsync(
        ModelDefinition model,
        string? cacheDirectory = null,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string GetCacheDirectory();
}

internal sealed class ModelDownloader : IModelDownloader
{
    // Uses ElBruno.HuggingFace.HuggingFaceDownloader internally
    // Same cache structure as reference repos:
    //   {cache_root}/{sanitized-model-id}/
}
```

### 5.3 ONNX GenAI Runtime Wrapper

```csharp
namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Thin wrapper around ONNX Runtime GenAI for model loading and inference.
/// </summary>
internal sealed class OnnxGenAIModel : IDisposable
{
    internal OnnxGenAIModel(string modelPath, ExecutionProvider provider, int gpuDeviceId);

    internal string Generate(string prompt, GenerationParameters parameters, CancellationToken ct);

    internal IAsyncEnumerable<string> GenerateStreamingAsync(
        string prompt, GenerationParameters parameters, CancellationToken ct);
}

internal sealed record GenerationParameters(
    int MaxLength = 2048,
    float Temperature = 0.7f,
    float TopP = 0.9f,
    int? TopK = null,
    float RepetitionPenalty = 1.0f);
```

---

## 6. Dependency Injection

```csharp
namespace ElBruno.LocalLLMs;

/// <summary>
/// Extension methods for registering LocalChatClient with DI.
/// </summary>
public static class LocalLLMsServiceExtensions
{
    /// <summary>
    /// Registers IChatClient as a singleton using default options (Phi-3.5-mini).
    /// </summary>
    public static IServiceCollection AddLocalLLMs(this IServiceCollection services);

    /// <summary>
    /// Registers IChatClient as a singleton with configured options.
    /// </summary>
    public static IServiceCollection AddLocalLLMs(
        this IServiceCollection services,
        Action<LocalLLMsOptions> configure);
}
```

**Usage:**
```csharp
// ASP.NET Core
builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.ExecutionProvider = ExecutionProvider.Cuda;
});

// Inject IChatClient anywhere
public class MyService(IChatClient chatClient) { ... }
```

---

## 7. NuGet Packaging Strategy

### 7.1 Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

### 7.2 Core Project NuGet Config

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>ElBruno.LocalLLMs</PackageId>
    <Description>Local LLM chat completions using Microsoft.Extensions.AI and ONNX Runtime GenAI. IChatClient implementation for running LLMs locally.</Description>
    <Authors>Bruno Capuano</Authors>
    <RepositoryUrl>https://github.com/elbruno/ElBruno.LocalLLMs</RepositoryUrl>
    <PackageProjectUrl>https://github.com/elbruno/ElBruno.LocalLLMs</PackageProjectUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>llm;onnx;ai;local;chat;microsoft-extensions-ai;ichatclient;phi;qwen;llama;genai</PackageTags>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ElBruno.HuggingFace.Downloader" Version="0.5.0" />
    <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.3.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
    <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI" Version="0.6.*" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ElBruno.LocalLLMs.Tests" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\..\images\icon.png" Pack="true" PackagePath="\" Link="icon.png" />
  </ItemGroup>
</Project>
```

### 7.3 Versioning

- **Pre-release:** `0.x.y` during initial development
- **First stable:** `1.0.0` when IChatClient contract is proven with ≥3 models
- Version is in the .csproj `<Version>` element, matching the reference repos
- CI publishes preview packages on PR merge, stable on GitHub Release tag

---

## 8. ONNX Conversion Pipeline

### 8.1 Two Paths to ONNX

| Path | Models | Tooling |
|------|--------|---------|
| **Native ONNX** | Phi-3.5, Phi-4 | Direct download from HuggingFace — no conversion needed |
| **Convert** | All others (Qwen, Llama, Mistral, Gemma, etc.) | Python script using `optimum` or ONNX Runtime GenAI builder |

### 8.2 Conversion Script

```
scripts/
  convert_to_onnx.py       # Main conversion script
  requirements.txt          # optimum, onnxruntime, torch, transformers
  README.md                 # Usage instructions
```

**`scripts/convert_to_onnx.py`** will:
1. Accept `--model-id` (HuggingFace ID) and `--output-dir`
2. Download the PyTorch model from HuggingFace
3. Convert to ONNX using `optimum.exporters.onnx` or `onnxruntime-genai` model builder
4. Optionally quantize to INT4/INT8
5. Output the ONNX model files + GenAI config to the output directory

### 8.3 Conversion Strategy for v1.0

**Phase 1 (MVP):** Support only native ONNX models (Phi-3.5, Phi-4)  
**Phase 2:** Add conversion pipeline for Qwen2.5, Llama-3.2  
**Phase 3:** Full model matrix conversion + HuggingFace upload of pre-converted models

Pre-converted models can be uploaded to HuggingFace under the `elbruno/` org for direct download, eliminating conversion at user runtime.

---

## 9. Test Strategy

### 9.1 Unit Tests (`tests/ElBruno.LocalLLMs.Tests/`)

**What to test without models:**
- `LocalLLMsOptions` validation and defaults
- `ModelDefinition` creation and `KnownModels.FindById()`
- `ChatTemplateFormatter` — each template format produces correct prompt strings
- `ModelDownloadProgress` struct
- `ExecutionProvider` enum coverage
- `LocalLLMsServiceExtensions.AddLocalLLMs()` registers correct services
- Constructor argument validation

**What to mock:**
- `IModelDownloader` — mock to avoid actual HuggingFace downloads
- ONNX Runtime GenAI session — wrap behind internal interface for testability

**Framework:** xUnit + Moq (or NSubstitute), matching reference repos

### 9.2 Integration Tests (`tests/ElBruno.LocalLLMs.IntegrationTests/`)

**What to test with real models:**
- End-to-end: create client → send message → receive response
- Streaming: verify tokens arrive incrementally
- Model download: verify auto-download on first use
- Multi-turn conversation
- Cancellation token support
- Memory/resource cleanup (Dispose)

**Guard:** Integration tests gated behind `[Trait("Category", "Integration")]` or environment variable (`RUN_INTEGRATION_TESTS=true`). CI skips them by default; a separate GPU-enabled CI job runs them.

### 9.3 Test Project Configuration

```xml
<!-- tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ElBruno.LocalLLMs\ElBruno.LocalLLMs.csproj" />
  </ItemGroup>
</Project>
```

---

## 10. Sample Applications

### 10.1 HelloChat (Minimal — the README example)

```csharp
using ElBruno.LocalLLMs;

using var client = await LocalChatClient.CreateAsync();

var response = await client.CompleteAsync([
    new(ChatRole.User, "What is the capital of France?")
]);

Console.WriteLine(response.Message.Text);
```

### 10.2 StreamingChat (Token-by-token output)

```csharp
using ElBruno.LocalLLMs;

using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

await foreach (var update in client.CompleteStreamingAsync([
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Explain quantum computing in simple terms.")
]))
{
    Console.Write(update.Text);
}
```

### 10.3 MultiModelChat (Runtime model switching)

Demonstrates creating multiple `LocalChatClient` instances with different models and comparing outputs.

### 10.4 DependencyInjection (ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
    options.ExecutionProvider = ExecutionProvider.DirectML;
});

var app = builder.Build();

app.MapPost("/chat", async (IChatClient client, ChatRequest request) =>
{
    var response = await client.CompleteAsync([
        new(ChatRole.User, request.Message)
    ]);
    return response.Message.Text;
});

app.Run();
```

---

## 11. Complete Directory Layout

```
ElBruno.LocalLLMs/
├── .editorconfig
├── .gitignore
├── .gitattributes
├── .github/
│   └── workflows/
│       ├── ci.yml                    # Build + unit tests on PR
│       ├── integration-tests.yml     # GPU tests on schedule/manual
│       └── publish.yml               # NuGet publish on release tag
├── Directory.Build.props
├── ElBruno.LocalLLMs.slnx
├── LICENSE
├── README.md
├── images/
│   └── icon.png
├── src/
│   └── ElBruno.LocalLLMs/
│       ├── ElBruno.LocalLLMs.csproj
│       │
│       ├── LocalChatClient.cs            # IChatClient implementation
│       ├── LocalLLMsOptions.cs           # Configuration options
│       ├── LocalLLMsServiceExtensions.cs # DI registration
│       │
│       ├── Models/
│       │   ├── ModelDefinition.cs        # Model descriptor record
│       │   ├── KnownModels.cs            # Pre-defined model registry
│       │   ├── ModelTier.cs              # Size tier enum
│       │   ├── OnnxModelType.cs          # Model type enum
│       │   └── ChatTemplateFormat.cs     # Template format enum
│       │
│       ├── Download/
│       │   ├── IModelDownloader.cs       # Download interface
│       │   ├── ModelDownloader.cs        # HuggingFace download impl
│       │   └── ModelDownloadProgress.cs  # Progress reporting
│       │
│       ├── Execution/
│       │   ├── ExecutionProvider.cs       # CPU/CUDA/DirectML enum
│       │   └── OnnxGenAIModel.cs         # ONNX GenAI wrapper (internal)
│       │
│       └── Templates/
│           ├── IChatTemplateFormatter.cs  # Template interface (internal)
│           ├── ChatTemplateFactory.cs     # Factory (internal)
│           ├── ChatMLFormatter.cs         # <|im_start|> format
│           ├── Phi3Formatter.cs           # <|user|> format
│           ├── Llama3Formatter.cs         # <|begin_of_text|> format
│           ├── QwenFormatter.cs           # Qwen format
│           └── MistralFormatter.cs        # [INST] format
│
├── tests/
│   ├── ElBruno.LocalLLMs.Tests/
│   │   ├── ElBruno.LocalLLMs.Tests.csproj
│   │   ├── LocalChatClientTests.cs
│   │   ├── LocalLLMsOptionsTests.cs
│   │   ├── KnownModelsTests.cs
│   │   ├── ModelDefinitionTests.cs
│   │   └── Templates/
│   │       ├── ChatMLFormatterTests.cs
│   │       ├── Phi3FormatterTests.cs
│   │       └── Llama3FormatterTests.cs
│   │
│   └── ElBruno.LocalLLMs.IntegrationTests/
│       ├── ElBruno.LocalLLMs.IntegrationTests.csproj
│       ├── ChatCompletionTests.cs
│       ├── StreamingTests.cs
│       └── ModelDownloadTests.cs
│
├── samples/
│   ├── HelloChat/
│   │   ├── HelloChat.csproj
│   │   └── Program.cs
│   ├── StreamingChat/
│   │   ├── StreamingChat.csproj
│   │   └── Program.cs
│   ├── MultiModelChat/
│   │   ├── MultiModelChat.csproj
│   │   └── Program.cs
│   └── DependencyInjection/
│       ├── DependencyInjection.csproj
│       └── Program.cs
│
├── scripts/
│   ├── convert_to_onnx.py
│   ├── requirements.txt
│   └── README.md
│
├── docs/
│   └── architecture.md               # This document
│
└── .squad/                            # Squad team configuration
```

---

## 12. Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI.Abstractions` | 10.3.0 | `IChatClient`, `ChatMessage`, `ChatCompletion` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.3 | DI extension methods |
| `Microsoft.ML.OnnxRuntimeGenAI` | 0.6.* | ONNX Runtime GenAI for LLM inference |
| `ElBruno.HuggingFace.Downloader` | 0.5.0 | Model download from HuggingFace Hub |

---

## 13. Implementation Phases

### Phase 1 — MVP (Trinity builds, Tank tests)
- [ ] Solution scaffolding: .slnx, Directory.Build.props, .csproj files
- [ ] `LocalLLMsOptions`, `ModelDefinition`, `KnownModels` (Phi-3.5 only)
- [ ] `ModelDownloader` using `ElBruno.HuggingFace.Downloader`
- [ ] `Phi3Formatter` chat template
- [ ] `OnnxGenAIModel` wrapper
- [ ] `LocalChatClient` implementing `IChatClient` (Complete + Streaming)
- [ ] Unit tests for all public API
- [ ] `HelloChat` sample
- [ ] Integration test with real Phi-3.5 model

### Phase 2 — Multi-Model Support
- [ ] Additional chat templates (ChatML, Llama3, Qwen, Mistral)
- [ ] `KnownModels` expanded to Phi-4, Qwen2.5-3B, Llama-3.2-3B
- [ ] Python conversion scripts
- [ ] `StreamingChat` and `MultiModelChat` samples
- [ ] DI integration + `DependencyInjection` sample

### Phase 3 — Production Hardening
- [ ] GPU execution providers (CUDA, DirectML)
- [ ] Performance benchmarks
- [ ] Pre-converted model hosting on HuggingFace
- [ ] Full model matrix validation
- [ ] NuGet publish workflow

---

## 14. Architecture Decision Records

| Decision | Rationale |
|----------|-----------|
| **Single package, not per-model** | Models are data (config records), not code. 20+ NuGet packages would be unmaintainable. |
| **`IChatClient`, not custom interface** | Non-negotiable. The library exists to plug into MEAI. |
| **ONNX Runtime GenAI, not raw ONNX Runtime** | GenAI handles tokenization, KV cache, sampling — we don't reinvent these. |
| **Sync constructor + async factory** | Matches `LocalEmbeddingGenerator` pattern. Sync for tools/tests, async for production. |
| **`ModelDefinition` as record** | Immutable, data-first. Adding a model = adding a record to `KnownModels`. |
| **Chat templates as internal strategy pattern** | Users pick a model; the template is resolved automatically. No template API in public surface. |
| **`ElBruno.HuggingFace.Downloader` for downloads** | Proven in both reference repos. Consistent download/cache behavior. |
| **`net8.0;net10.0` multi-targeting** | Matches reference repos. .NET 8 LTS + .NET 10 current. |
