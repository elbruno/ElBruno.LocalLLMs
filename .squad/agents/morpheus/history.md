# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-17 — Architecture Design Complete

**Architecture decisions made:**
- Single `ElBruno.LocalLLMs` core package (not per-model). Models are data (`ModelDefinition` records in `KnownModels`), not code.
- `LocalChatClient` implements `IChatClient` from `Microsoft.Extensions.AI.Abstractions`.
- Uses `Microsoft.ML.OnnxRuntimeGenAI` (not raw ORT) — GenAI handles tokenization, KV cache, sampling.
- Sync constructor + async `CreateAsync()` factory — proven pattern from LocalEmbeddings.
- Chat templates are internal strategy pattern (`IChatTemplateFormatter`), resolved automatically from `ChatTemplateFormat` enum.
- Multi-target `net8.0;net10.0`, matching reference repos.
- Phase 1 MVP targets Phi-3.5-mini-instruct only (native ONNX, zero conversion friction).

**Reference repo patterns discovered:**
- `elbruno.localembeddings`: `LocalEmbeddingGenerator` implements `IEmbeddingGenerator<string, Embedding<float>>`. Options class + `CreateAsync()` factory. `ModelDownloader` wraps `ElBruno.HuggingFace.Downloader`. DI via `AddLocalEmbeddings()`. `.slnx` solution format. `Directory.Build.props` with warnings-as-errors.
- `ElBruno.QwenTTS`: `.Core` package has pipeline + options. `TtsPipeline.CreateAsync()` factory. `QwenTtsServiceExtensions.AddQwenTts()` for DI. Python scripts in `/python` for ONNX conversion. E2E GPU integration tests in `/tests`.

**Key file paths:**
- Architecture doc: `docs/architecture.md`
- Decisions: `.squad/decisions/inbox/morpheus-architecture.md`
- Core source will be: `src/ElBruno.LocalLLMs/`
- Main class: `src/ElBruno.LocalLLMs/LocalChatClient.cs`
- Options: `src/ElBruno.LocalLLMs/LocalLLMsOptions.cs`
- Model registry: `src/ElBruno.LocalLLMs/Models/KnownModels.cs`

**User preferences:**
- Bruno uses `ElBruno.HuggingFace.Downloader` v0.5.0 for all model downloads
- Both reference repos use `.slnx` (new solution format), not `.sln`
- Both reference repos target `net8.0;net10.0`
- NuGet metadata includes icon, README, MIT license in all projects
- `InternalsVisibleTo` test projects is standard practice
