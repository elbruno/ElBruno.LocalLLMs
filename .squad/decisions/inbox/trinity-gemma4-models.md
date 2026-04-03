# Decision: Gemma 4 Model Definitions & Tier Placement

**Date:** 2026-03-29  
**Owner:** Trinity  
**Status:** Implemented

## Context

Google released Gemma 4 with 4 model sizes:
- **Gemma 4 E2B IT** — 5.1B total params (2B effective/active), 128K context, edge/mobile focus
- **Gemma 4 E4B IT** — 8B total params (4B effective/active), 128K context, edge/laptop focus  
- **Gemma 4 26B A4B IT** — 25.2B total params (3.8B active), MoE architecture, 256K context
- **Gemma 4 31B IT** — 30.7B dense params, 256K context, flagship quality

All 4 support native function calling and use the same chat template as Gemma 1/2 (`<start_of_turn>` format). None have native ONNX weights yet on HuggingFace.

## Decision

Added all 4 models to `KnownModels.cs` with the following tier placement:

### Tier Assignments
- **E2B** → **Tiny tier** — 2B active params make it an edge/mobile model despite 5.1B total
- **E4B** → **Small tier** — 4B active params, edge/laptop deployment sweet spot
- **26B A4B** → **Large tier** — MoE with fast inference (3.8B active), but 25B total still requires Large-tier RAM
- **31B** → **Large tier** — Dense flagship, max quality, standard Large tier placement

### Rationale

1. **Active params drive tier placement for MoE models** — E2B/E4B are effectively 2B/4B models at inference time
2. **Total params drive memory requirements** — 26B A4B needs ~20-28 GB despite fast inference (3.8B active)
3. **Existing Gemma formatter works as-is** — no template changes needed, already uses `ChatTemplateFormat.Gemma`
4. **ONNX conversion required** — all 4 set `HasNativeOnnx: false`, users must convert from `google/gemma-4-*` repos
5. **Tool calling enabled** — all 4 set `SupportsToolCalling: true` per Google's release notes

## Files Modified

- `src/ElBruno.LocalLLMs/Models/KnownModels.cs` — added 4 new static fields + added to `All` list
- `README.md` — added 4 rows to Supported Models table (status: 🔄 Convert)
- `docs/supported-models.md` — added full specs (HuggingFace IDs, chat template, RAM, tool calling)

## Implementation Notes

- Model IDs follow existing pattern: `gemma-4-e2b-it`, `gemma-4-e4b-it`, `gemma-4-26b-a4b-it`, `gemma-4-31b-it`
- C# field names: `Gemma4E2BIT`, `Gemma4E4BIT`, `Gemma4_26BA4BIT`, `Gemma4_31BIT` (underscores for numeric suffixes)
- HuggingFace repo IDs: `google/gemma-4-E2B-it`, `google/gemma-4-E4B-it`, `google/gemma-4-26B-A4B-it`, `google/gemma-4-31B-it`
- Fixed chat template documentation: separated Gemma from ChatML in supported-models.md table (was incorrectly grouped)

## Next Steps

When ONNX weights become available:
1. Update `HasNativeOnnx: true` for models with published ONNX
2. Update `HuggingFaceRepoId` to point to ONNX repos (likely `elbruno/Gemma-4-*-onnx` following existing pattern)
3. Change README status from 🔄 Convert to ✅ Native
