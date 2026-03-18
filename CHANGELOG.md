# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.0] - 2026-03-18

### Added

**Core Library**
- `LocalChatClient` class implementing `Microsoft.Extensions.AI.IChatClient`
- Async factory pattern: `LocalChatClient.CreateAsync()` with progress reporting
- Sync constructor: `new LocalChatClient(options)` for tools/tests/console apps
- Full streaming support via `GetStreamingResponseAsync()`

**Models & Execution**
- Support for Phi-3.5-mini-instruct (native ONNX, Microsoft-published)
- Support for Phi-4 (native ONNX, Microsoft-published)
- Support for Qwen2.5-0.5B-Instruct (requires ONNX conversion)
- 29 models in roadmap (Tiny, Small, Medium, Large, Next-Gen tiers)
- Automatic model download from HuggingFace via `ElBruno.HuggingFace.Downloader`
- Configurable model cache directory (default: `%LOCALAPPDATA%/ElBruno/LocalLLMs/models`)
- Custom `ModelPath` option to use pre-downloaded models without re-downloading

**GPU Acceleration**
- CUDA execution provider (NVIDIA GPUs)
- DirectML execution provider (Windows AMD/Intel Arc GPUs)
- CPU execution provider (default, works everywhere)
- Configurable GPU device ID for multi-GPU systems

**Chat Templates**
- 5 built-in chat template formatters (internal):
  - ChatML (for Qwen, Gemma, Mistral)
  - Phi3 (for Phi-3, Phi-3.5, Phi-4)
  - Llama3 (for Llama-3.x, Llama-4)
  - Qwen (for Qwen series)
  - Mistral (for Mistral 7B+)
- Automatic template selection based on model definition
- Support for multi-turn conversations with system/user messages

**Generation Parameters**
- Configurable temperature (default: 0.7)
- Configurable top-p / nucleus sampling (default: 0.9)
- Configurable max sequence length (default: 2048)
- Sensible defaults for both quality and speed

**Dependency Injection**
- `AddLocalLLMs()` extension method for ASP.NET Core
- Singleton registration of `IChatClient`
- Options configuration callback pattern

**Build & Infrastructure**
- Multi-target: `net8.0` and `net10.0`
- `Directory.Build.props` for shared build settings
- Language version 12.0 with implicit usings and nullable reference types
- Treat warnings as errors
- `.editorconfig` for code style enforcement

**Testing**
- 210 unit tests covering:
  - Chat template formatting (exact string matching)
  - Model registry and discovery
  - Generation parameter building
  - Options validation
  - Dependency injection registration
- 17 integration tests covering:
  - Model download from HuggingFace
  - ONNX model loading and initialization
  - E2E inference (single/multi-turn)
  - Streaming token generation
  - Memory cleanup and disposal
- Integration tests gated by `RUN_INTEGRATION_TESTS` environment variable
- xUnit + NSubstitute test framework

**Sample Applications**
- `HelloChat` — minimal 12-line example with single message
- `StreamingChat` — streaming response with token-by-token output
- `MultiModelChat` — switching between multiple models at runtime
- `DependencyInjection` — ASP.NET Core web app with DI registration

**Documentation**
- `docs/architecture.md` — 17 design decisions documented
- `docs/getting-started.md` — comprehensive user guide
- `docs/supported-models.md` — complete model reference (29 models)
- `CONTRIBUTING.md` — contributor guide (build, test, add models)
- `README.md` — quick start and feature overview
- `LICENSE` — MIT license

**ONNX Conversion Scripts**
- Python script for converting HuggingFace models to ONNX format
- Support for int4 quantization
- Batch file processing for efficient conversion

**CI/CD**
- GitHub Actions workflow for build, test, and NuGet publish
- Automated testing on every commit
- NuGet package versioning and release automation

### Technical Details

**Dependencies**
- `Microsoft.Extensions.AI.Abstractions` — 10.4.0 (MEAI API)
- `Microsoft.Extensions.DependencyInjection.Abstractions` — 10.0.5 (DI support)
- `Microsoft.ML.OnnxRuntimeGenAI` — 0.8.3 (ONNX inference with tokenization and streaming)
- `ElBruno.HuggingFace.Downloader` — 0.6.0 (model download)
- `xunit` — 2.9.0 (unit testing)
- `NSubstitute` — 5.3.0 (test mocking)

**Design Patterns**
- Single core package (not per-model)
- Lazy initialization with `SemaphoreSlim` for thread safety
- Internal strategy pattern for chat template formatting
- Data-driven model definitions (models are `record` instances, not classes)
- Sync constructor + async factory for flexible usage

**API Surface (MEAI 10.4.0)**
- `LocalChatClient.GetResponseAsync(messages, options?, cancellation)` → `ChatResponse`
- `LocalChatClient.GetStreamingResponseAsync(messages, options?, cancellation)` → `IAsyncEnumerable<ChatResponseUpdate>`
- `LocalChatClient.CreateAsync(options?, progress?, cancellation)` → async factory
- `LocalLLMsServiceExtensions.AddLocalLLMs(services, configure)` → DI registration

### Known Limitations

- Only Phi-3.5-mini and Phi-4 have native ONNX (others require conversion)
- Integration tests require manual `RUN_INTEGRATION_TESTS=true` to avoid unexpected downloads
- First run is slow (model download + ONNX load); subsequent runs are cached
- Streaming not yet available in all contexts (working on this)
- No built-in caching of inference results
- No prompt caching or KV cache persistence between requests

### Breaking Changes

None — this is the initial release.

---

## [Unreleased]

### Planned

- [ ] More native ONNX models (Llama-3.2-3B, Qwen2.5-3B, etc.)
- [ ] Semantic Kernel integration package
- [ ] Prompt caching and result caching
- [ ] Vision model support
- [ ] Function calling / tool use
- [ ] Extended context window support
- [ ] Fine-tuning support via LoRA adapters
- [ ] Performance benchmarking suite
- [ ] WebAssembly (WASM) target

---

## Legend

- **Added** — New features
- **Changed** — Changes in existing functionality
- **Deprecated** — Soon-to-be removed features
- **Removed** — Removed features
- **Fixed** — Bug fixes
- **Security** — Security vulnerabilities fixed
