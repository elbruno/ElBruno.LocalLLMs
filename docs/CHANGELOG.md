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

### Added

**Phase 4a: Tool Calling**
- **Tool/Function Calling** — `LocalChatClient` now supports tool calling via `IChatClient`. Define tools with `AIFunctionFactory.Create`, pass them in `ChatOptions.Tools`, and get `FunctionCallContent` in responses. Works with Phi-3.5, Phi-4, and all Qwen2.5 models.
- `SupportsToolCalling` property on `ModelDefinition` to indicate which models support tool calling
- `JsonToolCallParser` for parsing tool calls from model output (supports `<tool_call>` tags, raw JSON, and array formats)
- Tool-aware `FormatMessages` overload on all chat template formatters
- `ToolCallingAgent` sample demonstrating multi-turn agent loop with local tools
- 41 new unit tests for tool calling (parser, formatter, integration)
- `docs/tool-calling-guide.md` — comprehensive tool calling guide with model support matrix, quick start, defining tools, agent loop patterns, and error handling
- Architecture plan at `docs/plan-rag-tool-routing.md` for Phase 4 (RAG + Tool Routing)

**Phase 4b: RAG Pipeline**
- **RAG Pipeline Support** — new `ElBruno.LocalLLMs.Rag` extension package for Retrieval-Augmented Generation

**Phase 5: Fine-Tuned Model Integration**
- **Fine-tuned Qwen2.5 models** — three new `KnownModels` entries for fine-tuned variants: `Qwen25_05B_ToolCalling`, `Qwen25_05B_RAG`, `Qwen25_05B_Instruct_FineTuned`
- `FineTunedToolCalling` sample demonstrating improved tool calling accuracy with fine-tuned Qwen2.5-0.5B
- `docs/fine-tuning-guide.md` — user-facing guide for using pre-fine-tuned models and fine-tuning your own
- `IDocumentChunker` interface for splitting documents into chunks
- `SlidingWindowChunker` implementation with configurable chunk size and overlap
- `IDocumentStore` interface for vector storage and similarity search
- `InMemoryDocumentStore` implementation with cosine similarity search (fast, in-memory)
- `SqliteDocumentStore` implementation for persistent vector storage across runs
- `IRagPipeline` interface orchestrating chunking, embedding, and retrieval workflows
- `LocalRagPipeline` implementation integrating with `ElBruno.LocalEmbeddings` for vector embeddings
- `RagContext` record containing retrieved chunks and formatted context for injection into chat
- `RagIndexProgress` for progress reporting during document indexing
- `RagServiceExtensions.AddLocalRag()` for dependency injection in ASP.NET Core
- `RagChatbot` sample demonstrating end-to-end RAG pipeline (index documents, retrieve context, chat with grounding)
- `docs/rag-guide.md` — comprehensive RAG guide with architecture overview, quick start, chunking strategies, vector stores, best practices, and limitations

**Documentation**
- Updated `docs/supported-models.md` with tool calling support matrix for Qwen2.5-0.5B, Phi-3.5-mini, Qwen-7B, Phi-4, Llama-3.2-3B, DeepSeek-R1
- Updated `docs/getting-started.md` with tool calling section and reference to samples/ToolCallingAgent
- `docs/tool-calling-guide.md` — 23KB guide covering model support, quick start, tool definitions, agent loops, multi-turn conversations, error handling, and limitations
- `docs/rag-guide.md` — 24KB guide covering RAG architecture, chunking, vector stores, dependency injection, RAG + tool calling patterns, and best practices

### Planned

- [ ] More native ONNX models (Llama-3.2-3B, Qwen2.5-3B, etc.)
- [ ] Semantic Kernel integration package
- [ ] Prompt caching and result caching
- [ ] Vision model support
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
