# SKILL: MEAI + ONNX Library Architecture Pattern

> Reusable architecture pattern for building .NET libraries that expose Microsoft.Extensions.AI
> interfaces backed by local ONNX Runtime inference with HuggingFace model downloads.

## When to Use

- Building a .NET library that wraps ONNX models behind MEAI interfaces (`IChatClient`, `IEmbeddingGenerator`)
- Need auto-download from HuggingFace with local caching
- Want DI integration with `IServiceCollection` extensions
- Supporting multiple models through a single package

## Pattern Structure

```
Root/
├── Directory.Build.props          # Shared: LangVersion, Nullable, TreatWarningsAsErrors
├── {LibName}.slnx                 # New .NET solution format
├── src/{LibName}/
│   ├── {LibName}.csproj           # Multi-target net8.0;net10.0
│   ├── {MainClass}.cs             # Implements MEAI interface (IChatClient, IEmbeddingGenerator)
│   ├── {Options}.cs               # Options class with model name, path, cache dir
│   ├── {ServiceExtensions}.cs     # AddXxx() IServiceCollection extension
│   ├── Models/
│   │   ├── ModelDefinition.cs     # Record describing a model (HF repo, files, type)
│   │   └── KnownModels.cs        # Static registry of pre-defined models
│   └── Download/
│       ├── IModelDownloader.cs    # Interface for testability
│       └── ModelDownloader.cs     # Uses ElBruno.HuggingFace.Downloader
├── tests/{LibName}.Tests/         # Unit tests (mock downloader, no real models)
├── tests/{LibName}.IntegrationTests/  # Needs real models, gated by trait/env var
├── samples/                       # Console apps demonstrating usage
└── scripts/                       # Python ONNX conversion scripts
```

## Key Patterns

1. **Sync Constructor + Async Factory:** `new MainClass(options)` for tools, `MainClass.CreateAsync(options)` for ASP.NET
2. **Model-as-Data:** `ModelDefinition` record with HF repo ID + required files. Adding model = adding record instance.
3. **Auto-Download on First Use:** Check cache → download if missing → load ONNX session
4. **Internal Abstractions:** Template formatters, ONNX wrappers are internal. Public surface is minimal.
5. **DI Extension:** `services.AddXxx(options => { ... })` registers the MEAI interface as singleton.

## Dependencies (Bruno's Stack)

- `ElBruno.HuggingFace.Downloader` 0.5.0 — model download
- `Microsoft.Extensions.AI.Abstractions` — MEAI interfaces
- `Microsoft.Extensions.DependencyInjection.Abstractions` — DI
- `Microsoft.ML.OnnxRuntime` or `Microsoft.ML.OnnxRuntimeGenAI` — inference

## Proven In

- `elbruno/elbruno.localembeddings` — IEmbeddingGenerator
- `elbruno/ElBruno.QwenTTS` — ITtsPipeline (custom interface)
- `elbruno/ElBruno.LocalLLMs` — IChatClient (designed, not yet built)
