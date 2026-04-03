# Decision: Gemma 4 Dedicated Conversion Script

**Date:** 2025-03-17  
**Author:** Dozer (ML/ONNX Conversion Engineer)  
**Status:** Implemented  

## Context

Google released Gemma 4 with four model sizes featuring diverse architectures:
- E2B/E4B: Dense with Per-Layer Embeddings (PLE)
- 26B: Mixture of Experts (MoE, 8 active / 128 total + 1 shared)
- 31B: Pure dense

No native ONNX weights exist yet, requiring custom conversion for use with ElBruno.LocalLLMs.

## Decision

Created a dedicated `convert_gemma4.py` conversion script instead of extending the generic `convert_to_onnx.py`.

### Key Design Choices

1. **Use onnxruntime_genai.models.builder** instead of optimum
   - Generates `genai_config.json` required for C# library compatibility
   - Properly embeds chat templates in the config
   - Handles tokenizer setup correctly for ONNX Runtime GenAI

2. **Bake in `trust_remote_code=True`**
   - All Gemma 4 models require remote code execution
   - Passing via `--extra_options` to the builder
   - User doesn't need to remember this flag

3. **Pre-flight checks**
   - RAM check (8-80 GB depending on model size)
   - Disk space check (30-180 GB depending on model size)
   - Dependency validation before starting conversion
   - Prevents wasted time from failed conversions

4. **Model-specific validation**
   - Checks for `genai_config.json`, `tokenizer_config.json`, `.onnx` files
   - Warns if required files are missing
   - Clear error messages for common Gemma 4 issues

5. **PowerShell wrapper**
   - Windows-first environment
   - Auto-installs missing Python dependencies
   - Makes conversion accessible to C# developers

## Files Created

- `scripts/convert_gemma4.py` — Python conversion script (350 lines)
- `scripts/convert_gemma4.ps1` — PowerShell wrapper (130 lines)
- `docs/onnx-conversion.md` — Added comprehensive Gemma 4 section (~200 lines)
- `scripts/requirements.txt` — Updated with new dependencies
- `.squad/team.md` — Added 4 Gemma 4 models to Target Models table

## Rationale

### Why not extend convert_to_onnx.py?

1. **Different tooling** — GenAI builder vs. optimum requires fundamentally different code paths
2. **Model-specific complexity** — MoE routing, PLE architecture need dedicated handling
3. **User experience** — Dedicated script provides clearer errors, better validation, model-specific guidance
4. **Maintainability** — Separate scripts are easier to update as Gemma 4 evolves

### Why GenAI builder over optimum?

- **C# library compatibility** — Requires `genai_config.json` with embedded chat template
- **Tokenizer setup** — GenAI builder configures tokenizer correctly for streaming
- **Future-proof** — GenAI builder is the recommended path for ONNX Runtime GenAI workloads

## Alternatives Considered

1. **Extend convert_to_onnx.py with --model-family flag**
   - Rejected: Would create a complex monolithic script with branching logic
   - Better to have focused, single-purpose tools

2. **Use optimum with post-processing**
   - Rejected: Would require manually creating genai_config.json
   - Error-prone and fragile as config format evolves

3. **Document manual conversion steps**
   - Rejected: Too complex for users, error-prone, not reproducible
   - Automation provides better UX

## Dependencies Added

```
onnxruntime-genai>=0.4.0    # GenAI model builder (required)
huggingface-hub>=0.20.0      # Model downloading
psutil>=5.9.0                # RAM checks (optional but recommended)
```

## Future Work

- Test conversions once Gemma 4 is officially released
- Add GPU support (`-e cuda`) for faster conversion
- Consider automated testing of converted models
- May need architecture-specific handling as more variants emerge

## Team Impact

- **Trinity (Core Dev):** Can integrate Gemma 4 models into library once converted
- **Tank (Tester):** Can test conversions and validate output quality
- **Users:** Clear conversion path for cutting-edge Gemma 4 models
- **Bruno:** Self-service conversion without needing ML expertise

## References

- Gemma 4 announcement: https://huggingface.co/collections/google/gemma-4-6774ae48e9c9ff6e55b60deb
- ONNX Runtime GenAI docs: https://onnxruntime.ai/docs/genai/
- Model IDs:
  - `google/gemma-4-E2B-it`
  - `google/gemma-4-E4B-it`
  - `google/gemma-4-26B-A4B-it`
  - `google/gemma-4-31B-it`
