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

### 2026-03-19: GPU/CPU NuGet package strategy
- Main library keeps CPU-only `Microsoft.ML.OnnxRuntimeGenAI` — GPU is additive via app-level package refs
- GPU NuGet packages for v0.12.2: `Microsoft.ML.OnnxRuntimeGenAI.Cuda` and `.DirectML` (no .NET packages for QNN or WinML)
- QNN is Qualcomm/ARM-only (Android native), WinML is a system component (no separate NuGet) — neither relevant for .NET desktop/server
- ExecutionProvider enum (Auto/Cpu/Cuda/DirectML) covers all available .NET providers — no new entries needed
- `ShouldFallbackToNextProvider` expanded with 4 additional error patterns: "no available provider", "unable to find", "cannot load", "not available"
- ConsoleAppDemo.csproj shows GPU enablement via commented-out package refs — clean pattern for samples
- README GPU section placed after Quick Start, documents fallback order per OS

### 2026-03-19: Progress rendering + provider fallback hardening
- `ConsoleDownloadProgressRenderer` centralizes console progress behavior with two modes: interactive single-line updates and redirected concise periodic lines.
- Interactive rendering should throttle and state-filter updates, then always emit one final newline after completion to avoid prompt overlap.
- `ExecutionProvider.Auto` fallback should only continue when failure text indicates provider unavailability; non-provider/model errors should fail fast instead of silently dropping to CPU.
- Surface fallback reasoning (`LocalChatClient.ProviderSelectionDetails`) so samples can explain why CPU was selected.
- Guard model-selection regressions for GPU-preferred defaults with tests that assert Phi model required paths remain under `gpu/`.

### 2026-03-19: GPU-first defaults + stable progress output
- Added `ExecutionProvider.Auto` as default option value; runtime fallback order is CUDA -> DirectML -> CPU
- Exposed resolved runtime provider via `LocalChatClient.ActiveExecutionProvider` so samples can show when CPU fallback occurred
- Updated Phi defaults to GPU subpaths in `KnownModels` (`gpu/gpu-int4-awq-block-128` and `gpu/gpu-int4-rtn-block-32`) to avoid CPU-pinned variant selection
- Console progress rendering is most reliable with ASCII-only carriage-return updates (`\r`) plus one explicit final newline after completion
- Net10 validation can be blocked by external file locks on `obj/Debug/net10.0/ElBruno.LocalLLMs.dll`; net8 build is a practical fallback for local verification

### 2026-03-18: Gemma ONNX conversions and Llama gating
- Gemma v1 (2B) and v2 (2B, 9B) architectures are confirmed supported by ONNX Runtime GenAI builder — conversions succeeded cleanly
- Meta Llama 3.2 and 3.3 have separate license gates from Llama 3.1 — each requires its own HuggingFace access request
- Llama-3.2-3B access is "awaiting review"; Llama-3.3-70B needs a request at https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct
- Three new elbruno ONNX repos: Gemma-2B-IT-onnx, Gemma-2-2B-IT-onnx, Gemma-2-9B-IT-onnx

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

### 2026-03-18: Llama-3.2-3B ONNX repo + README badges
- Dozer converted Llama-3.2-3B-Instruct to ONNX; updated KnownModels.cs to point at `elbruno/Llama-3.2-3B-Instruct-onnx` with `RequiredFiles = ["*"]` and `HasNativeOnnx = true`
- Updated ONNX status from 🔄 Convert → ✅ Native in both README.md model table and docs/supported-models.md
- Expanded README badge block to 8 badges matching ElBruno.VibeVoiceTTS style: NuGet, NuGet Downloads, Build Status, MIT License, HuggingFace, .NET 8/10, GitHub Stars, Twitter Follow
- Beware: `git add -A` can pick up a local `cache_dir/` with multi-GB model blobs — always stage specific files instead

### 2026-03-18: ConsoleAppDemo sample created
- Created `samples/ConsoleAppDemo/` — comprehensive demo with 4 examples: download progress, simple Q&A, streaming, multi-turn conversation
- Follows the LocalEmbeddings ConsoleApp pattern: box-drawn banners (╔═══╗), section separators (━━━), emoji markers (⬇️ ✓ 🗣️ 🤖)
- `ModelDownloadProgress.PercentComplete` is 0.0–1.0 range (use `:P0` format specifier for display)
- `_resolvedModelPath` is private — use `Path.Combine(SpecialFolder.LocalApplicationData, "ElBruno", "LocalLLMs", "models")` to show expected cache path
- Multi-turn conversation demo adds assistant response to `List<ChatMessage>` history between turns
- Added to `ElBruno.LocalLLMs.slnx` under `/samples/` folder — builds clean (0 warnings)

### 2026-03-19: Model manager script safety conventions
- Added `scripts/manage-models.ps1` with advanced script parameter sets for list, locations, report, delete-one, and delete-all flows.
- Matched library default cache root exactly: `%LOCALAPPDATA%\ElBruno\LocalLLMs\models`.
- Destructive actions require explicit switches and interactive confirmation unless `-Force` is supplied.
- `-DryRun` is implemented via PowerShell WhatIf semantics so delete flows can be previewed safely.
- Report/list output uses table formatting with per-model size, file count, and total size summary.
- Optional `-CleanupEmptyFolders` removes empty directories after delete operations.
- QA hardening of `scripts/delete-models.ps1` should preserve native `SupportsShouldProcess` behavior so both scripts share consistent dry-run semantics.

### 2026-03-27: Tool/Function calling implementation using prompt-based approach
- Implemented full tool calling support for `LocalChatClient` via prompt-based approach (ONNX GenAI has no native function calling)
- Created `ToolCalling/` namespace with `IToolCallParser`, `ParsedToolCall` record, `JsonToolCallParser`, and `ToolCallParserFactory`
- `JsonToolCallParser` handles multiple formats: `<tool_call>` tags, raw JSON objects, arrays, with auto-generated CallId if not present
- Extended `IChatTemplateFormatter` with `FormatMessages(messages, tools)` overload — backward compatible
- Implemented full tool support in `ChatMLFormatter` — injects tool schemas into system message, formats `FunctionCallContent` in assistant messages, formats `FunctionResultContent` in user messages
- Other formatters (Phi3, Qwen, Llama3, Gemma, Mistral, DeepSeek) have stub implementations that delegate to non-tool version (TODOs for future work)
- Updated `LocalChatClient.GetResponseAsync` to parse tool calls from LLM output and build `FunctionCallContent` items
- Updated `LocalChatClient.GetStreamingResponseAsync` to accumulate text, then parse tool calls and emit as separate updates at end
- Added `ModelDefinition.SupportsToolCalling` property — enabled for Phi-3.5, Phi-4, and all Qwen2.5 models (0.5B, 1.5B, 3B, 7B)
- Tool calling works via JSON schemas in prompt + JSON parsing from text output — no ONNX Runtime modifications needed
- Key file paths: `src/ElBruno.LocalLLMs/ToolCalling/`, `Templates/IChatTemplateFormatter.cs`, `Templates/ChatMLFormatter.cs`, `LocalChatClient.cs`, `Models/ModelDefinition.cs`, `Models/KnownModels.cs`


### 2026-03-27: Phase 4a Tool Calling Implementation Complete

**All components delivered and tested:**
- ToolCalling namespace fully integrated: IToolCallParser, ParsedToolCall, JsonToolCallParser, ToolCallParserFactory
- Parser handles 3 output formats (Qwen tags, raw JSON, arrays) with auto-generated stable CallIds
- Extended IChatTemplateFormatter with FormatMessages(messages, tools) — backward compatible
- ChatMLFormatter fully implemented (tool injection, result formatting); other formatters stubbed
- LocalChatClient routes tools through formatter → parser → FunctionCallContent
- Streaming and non-streaming modes both work (parses at end, emits tool calls as final update)
- 41 comprehensive tests (Tank): parser (29), formatter (12), integration (20) — all passing
- 359/359 total tests passing (24 existing + 41 new)
- Backward compatibility verified (non-tool code unaffected)
- All 11 Phase 4 architectural decisions merged to canonical decisions.md

**Ready for Phase 4b:** RAG pipeline architecture specified; Trinity ready to implement ElBruno.LocalLLMs.Rag extension package
