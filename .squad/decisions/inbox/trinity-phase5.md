# Decision: Phase 5 — Fine-Tuned Model Integration

**Author:** Trinity (Core Dev)  
**Date:** 2026-03-29  
**Status:** Implemented

## Context

Phase 5 of the fine-tuning plan (`docs/plan-finetune-qwen.md`) calls for integrating fine-tuned Qwen2.5 models into the library so .NET developers can discover and use them easily.

## Decisions Made

### 1. Three fine-tuned model definitions added to KnownModels

Added `Qwen25_05B_ToolCalling`, `Qwen25_05B_RAG`, and `Qwen25_05B_Instruct_FineTuned` to `KnownModels.cs` in the Tiny tier section, immediately after the base `Qwen25_05BInstruct`. All three are included in the `All` collection.

### 2. No `Description` property on ModelDefinition

The plan specified a `Description` field on the new model entries, but `ModelDefinition` doesn't have this property. Adding it would be a public API change requiring consideration across all existing models. Omitted for now — can be added as a separate enhancement.

### 3. Sample follows ToolCallingAgent pattern exactly

`FineTunedToolCalling` uses the same structure: agent loop, 3 tools (GetCurrentTime, Calculate, GetWeather), multi-turn demos with the same formatting conventions (emoji markers, section separators, FormatArgs helper).

### 4. Fine-tuning guide targets two audiences

`docs/fine-tuning-guide.md` has a clear split: "Using Pre-Fine-Tuned Models" (just download and use, C# only) and "Fine-Tuning Your Own Model" (advanced, requires Python). This matches Bruno's directive that most .NET developers just want to download and run.

## Files Changed

- `src/ElBruno.LocalLLMs/Models/KnownModels.cs` — 3 new model definitions
- `src/samples/FineTunedToolCalling/` — Program.cs, .csproj, README.md
- `docs/fine-tuning-guide.md` — new user-facing guide
- `ElBruno.LocalLLMs.slnx` — added FineTunedToolCalling project
- `docs/CHANGELOG.md` — Phase 5 entries
