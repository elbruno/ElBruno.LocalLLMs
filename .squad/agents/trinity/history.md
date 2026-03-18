# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Architecture Status

**2026-03-17:** Morpheus completed full solution architecture. Blueprint in `docs/architecture.md`. 9 decisions merged to `.squad/decisions.md`. Trinity should implement core library using architecture.md as canonical reference.

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-17: Samples implemented and README fixed for MEAI 10.4.0
- MEAI 10.4.0 uses `GetResponseAsync`/`GetStreamingResponseAsync` (NOT `CompleteAsync`/`CompleteStreamingAsync`)
- Returns `ChatResponse` with `.Text` property (NOT `ChatCompletion` with `.Message.Text`)
- Streaming returns `ChatResponseUpdate` with `.Text`
- DI sample csproj already had `Microsoft.NET.Sdk.Web` — no change needed
- All 4 samples (HelloChat, StreamingChat, MultiModelChat, DependencyInjection) now have real implementations
- Full solution builds clean (8 projects, 0 warnings)

### 2026-03-17: Core library implementation complete
- Implemented all 21 .cs files across Models/, Download/, Execution/, Templates/, and root
- ElBruno.HuggingFace.Downloader API: `HuggingFaceDownloader` class, `DownloadRequest` with `RepoId`/`LocalDirectory`/`RequiredFiles`/`OptionalFiles`/`Progress`, `DownloadFilesAsync()` method, `AreFilesAvailable()` for cache checks
- Microsoft.ML.OnnxRuntimeGenAI API: `Model(path)` → `Tokenizer(model)` → `GeneratorParams(model)` with `SetSearchOption()` → `Generator(model, params)` with `AppendTokenSequences()` / `GenerateNextToken()` / `GetNextTokens()` loop. `TokenizerStream` for incremental decoding.
- Config class needed for non-CPU providers: `ClearProviders()` → `AppendProvider("cuda"/"dml")` → `SetProviderOption(provider, "device_id", id)` → `new Model(config)`
- Lazy init pattern with SemaphoreSlim for thread-safe model loading on first CompleteAsync call
- ChatOptions from MEAI maps: MaxOutputTokens→MaxLength, Temperature, TopP, TopK, FrequencyPenalty→RepetitionPenalty

### 2026-03-18: Comprehensive docs overhaul
- README now shows all 23 models in a tier-organized table with ONNX status (✅ Native vs 🔄 Convert)
- CONTRIBUTING.md and CHANGELOG.md moved from repo root to docs/ per Bruno's directive (only README.md + LICENSE at root)
- Created docs/samples.md — walkthroughs for all 4 samples (HelloChat, StreamingChat, MultiModelChat, DependencyInjection) with code snippets and expected output
- Created docs/benchmarks.md — guide for ChatTemplateBenchmarks and ModelDefinitionBenchmarks (BenchmarkDotNet, `[MemoryDiagnoser]`)
- Created docs/onnx-conversion.md — conversion pipeline docs covering prerequisites, INT4/INT8/none quantization, troubleshooting, disk space planning
- CI workflows (squad-preview, squad-release, squad-promote) updated to reference `docs/CHANGELOG.md` instead of root `CHANGELOG.md`
- Cross-references in getting-started.md, supported-models.md, and publishing.md updated after file moves
- Key docs paths: docs/samples.md, docs/benchmarks.md, docs/onnx-conversion.md, docs/CONTRIBUTING.md, docs/CHANGELOG.md
