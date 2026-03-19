# Decision: GPU Support via Additive NuGet Packages

**Date:** 2026-03-19
**Author:** Trinity (Core Dev)
**Status:** Implemented

## Context

The library ships `Microsoft.ML.OnnxRuntimeGenAI` (CPU-only) as its base dependency. The runtime fallback code in `OnnxGenAIModel.cs` already tries DirectML→CUDA→CPU on Windows and CUDA→CPU on Linux, but GPU providers were never available because only the CPU package was referenced.

## Decision

GPU support is **additive at the app level**, not baked into the library package:

1. **Library** (`ElBruno.LocalLLMs`) keeps CPU-only NuGet ref — works everywhere
2. **Consumers** add `Microsoft.ML.OnnxRuntimeGenAI.Cuda` or `.DirectML` to their app project
3. Runtime auto-detects available providers — zero code changes required

## Rationale

- Bundling CUDA (~800MB) or DirectML in the library NuGet would bloat it and break users without compatible hardware
- The additive pattern mirrors how Microsoft ships ONNX Runtime itself (base + provider packages)
- v0.12.2 confirmed: QNN has no .NET NuGet, WinML is a system component — no new enum values needed

## Consequences

- README documents the pattern clearly with `dotnet add` commands
- ConsoleAppDemo shows commented-out GPU refs as a template
- `ExecutionProvider` enum stays at 4 values: Auto, Cpu, Cuda, DirectML
- Error pattern matching in `ShouldFallbackToNextProvider` expanded for robustness
