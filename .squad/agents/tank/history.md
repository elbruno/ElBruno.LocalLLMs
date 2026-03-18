# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Architecture Status

**2026-03-17:** Morpheus completed full solution architecture. Blueprint in `docs/architecture.md`. 9 decisions merged to `.squad/decisions.md`. Tank should write unit test stubs mapping to interfaces defined in architecture (IModelDefinition, IChatTemplateFormatter, etc.).

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-03-17:** MEAI v10.4.0 uses renamed API: `GetResponseAsync`/`GetStreamingResponseAsync` (not `CompleteAsync`), `ChatResponse` (not `ChatCompletion`), `ChatResponseUpdate` (not `StreamingChatCompletionUpdate`), `DefaultModelId` (not `ModelId`), `GetService(Type, object?)` (not generic). Tests must align with these names.
- **2026-03-17:** Created 14 test files (11 unit + 3 integration) covering 210 passing unit tests. All template formatters tested with exact output strings. LocalChatClient tested with NSubstitute-mocked IModelDownloader via internal constructor. Integration tests gated by `RUN_INTEGRATION_TESTS=true` env var.
- **2026-03-17:** QwenFormatter output is identical to ChatMLFormatter (both use `<|im_start|>/<|im_end|>`). MistralFormatter folds system prompt into first `[INST]` block with `\n\n` separator.
- **2026-03-17:** `InternalsVisibleTo` on core project allows tests to access internal types: `IChatTemplateFormatter`, `ChatTemplateFactory`, `ChatMLFormatter`, `Phi3Formatter`, `Llama3Formatter`, `QwenFormatter`, `MistralFormatter`, `OnnxGenAIModel`, and the internal `LocalChatClient(options, downloader)` constructor.
- **2026-03-17:** `KnownModels.FindById()` is case-insensitive (`StringComparison.OrdinalIgnoreCase`). Tests verify both exact and uppercase lookups succeed.
