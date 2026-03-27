# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Architecture Status & RAG Plan

**2026-03-17:** Morpheus completed full solution architecture. Blueprint in `docs/architecture.md`. 9 decisions merged to `.squad/decisions.md`. Tank should write unit test stubs mapping to interfaces defined in architecture (IModelDefinition, IChatTemplateFormatter, etc.).

**2026-03-27:** RAG tool routing plan approved (`docs/plan-rag-tool-routing.md`). Tank is owner for **Phase 1** (benchmark framework). Deliverable: `benchmarks/ElBruno.LocalLLMs.ToolRoutingBenchmarks/` project with benchmark scenarios for 6 models × 3 catalog sizes × 5 prompt categories. Measures accuracy, latency, memory. Data drives Phase 3 optimization decisions.

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-03-19:** `scripts/manage-models.ps1` is now the primary operator workflow (list/locations/report/delete with parameter sets); QA should keep safety assertions aligned across both `manage-models.ps1` and legacy `delete-models.ps1` paths.
- **2026-03-19:** `scripts/delete-models.ps1` should use native `SupportsShouldProcess` (`-WhatIf`/`-Confirm`) instead of a custom `-WhatIf` switch; in list mode, use `Format-Table | Out-Host` before returning `-PassThru` objects so automation receives clean JSON/object output.
- **2026-03-19:** Post-initialization fallback state (`Auto` resolving to `Cpu` after GPU provider failures) is not unit-testable without a model creation seam; a future model-factory/provider-probe abstraction would enable deterministic fallback-state tests without downloading models.
- **2026-03-19:** Provider selection defaults changed to `ExecutionProvider.Auto` with deterministic runtime fallback `Cuda -> DirectML -> Cpu`. Tests should assert both requested provider and resolved active provider behavior, including explicit-provider no-fallback paths.
- **2026-03-19:** Console sample progress output now uses single-line carriage-return rendering (`\r`) with one trailing newline; output assertions or snapshot-style checks should avoid expecting multi-line incremental logs.
- **2026-03-17:** MEAI v10.4.0 uses renamed API: `GetResponseAsync`/`GetStreamingResponseAsync` (not `CompleteAsync`), `ChatResponse` (not `ChatCompletion`), `ChatResponseUpdate` (not `StreamingChatCompletionUpdate`), `DefaultModelId` (not `ModelId`), `GetService(Type, object?)` (not generic). Tests must align with these names.
- **2026-03-17:** Created 14 test files (11 unit + 3 integration) covering 210 passing unit tests. All template formatters tested with exact output strings. LocalChatClient tested with NSubstitute-mocked IModelDownloader via internal constructor. Integration tests gated by `RUN_INTEGRATION_TESTS=true` env var.
- **2026-03-17:** QwenFormatter output is identical to ChatMLFormatter (both use `<|im_start|>/<|im_end|>`). MistralFormatter folds system prompt into first `[INST]` block with `\n\n` separator.
- **2026-03-17:** `InternalsVisibleTo` on core project allows tests to access internal types: `IChatTemplateFormatter`, `ChatTemplateFactory`, `ChatMLFormatter`, `Phi3Formatter`, `Llama3Formatter`, `QwenFormatter`, `MistralFormatter`, `OnnxGenAIModel`, and the internal `LocalChatClient(options, downloader)` constructor.
- **2026-03-17:** `KnownModels.FindById()` is case-insensitive (`StringComparison.OrdinalIgnoreCase`). Tests verify both exact and uppercase lookups succeed.
- **2026-03-19:** `ProviderSelectionTests.cs` expanded from 7 to 62 tests covering all 8 fallback message patterns for CUDA/DirectML, case sensitivity, null/missing-context edge cases, inner exceptions, CPU provider behavior, and `BuildProviderFailureReason` formatting/truncation/newline tests. `BuildProviderFailureReason` changed from `private` to `internal static` to enable direct testing. Total suite: 318 tests, all passing.
- **2026-03-19:** `ShouldFallbackToNextProvider` uses `ex.ToString()` (not `ex.Message`), so inner exception text is included in the match. This means a provider keyword in an inner exception can trigger fallback — tested and verified.
- **2026-03-19:** `LocalChatClientTests.cs` extended with `ProviderSelectionDetails_BeforeInitialization_IsNull` and parameterized `ActiveExecutionProvider_BeforeInitialization_MatchesConfigured` covering all explicit provider values.
