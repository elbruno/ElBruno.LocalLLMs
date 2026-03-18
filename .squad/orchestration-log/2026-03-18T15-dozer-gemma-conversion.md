# Orchestration Log: Dozer — Gemma & Llama ONNX Conversion

**Timestamp:** 2026-03-18T15:00Z  
**Agent:** Dozer (ML Engineer)  
**Mode:** background  
**Task:** Convert 5 Gemma + Llama models to ONNX GenAI INT4 format

## Outcome

**Result:** 3 of 5 models converted successfully

### Converted Models
- ✅ **Gemma-2B-IT** (google/gemma-2b-it) → [elbruno/Gemma-2B-IT-onnx](https://huggingface.co/elbruno/Gemma-2B-IT-onnx) | 3.5 GB INT4
- ✅ **Gemma-2-2B-IT** (google/gemma-2-2b-it) → [elbruno/Gemma-2-2B-IT-onnx](https://huggingface.co/elbruno/Gemma-2-2B-IT-onnx) | 3.8 GB INT4
- ✅ **Gemma-2-9B-IT** (google/gemma-2-9b-it) → [elbruno/Gemma-2-9B-IT-onnx](https://huggingface.co/elbruno/Gemma-2-9B-IT-onnx) | 9.0 GB INT4

### Blocked Models
- ❌ **Llama-3.2-3B-Instruct** (meta-llama/Llama-3.2-3B-Instruct) | 403 Forbidden — "awaiting review from repo authors" (separate gated license from Llama 3.1)
- ❌ **Llama-3.3-70B-Instruct** (meta-llama/Llama-3.3-70B-Instruct) | 403 Forbidden — "not in the authorized list" (separate gated license from Llama 3.1)

## Files Produced

- `elbruno/Gemma-2B-IT-onnx` on HuggingFace
- `elbruno/Gemma-2-2B-IT-onnx` on HuggingFace
- `elbruno/Gemma-2-9B-IT-onnx` on HuggingFace
- `dozer/history.md` updated with Gemma architecture support finding

## Key Finding

**Gemma architecture IS SUPPORTED** by `onnxruntime_genai` builder v0.12.1. Both Gemma v1 (2B) and Gemma v2 (2B, 9B) convert cleanly to ONNX GenAI INT4 CPU format. This was previously unknown and represents a capability expansion for the library.

| Architecture | Support Status | Models Tested |
|-------------|---|---|
| Gemma v1 | ✅ Supported | Gemma-2B-IT |
| Gemma v2 | ✅ Supported | Gemma-2-2B-IT, Gemma-2-9B-IT |

## Technical Notes

- **Conversion speed:** Gemma-2B ~2 min, Gemma-2-2B ~3 min, Gemma-2-9B ~8 min
- **Vocabulary size:** Gemma uses 256K vocab (256000 tokens), making embed_tokens weights large (~2-3 GB) even for small param counts
- **INT4 quantization:** CPU INT4 format for all models (compatible with library's current deployment target)

## Action Items for Next Phases

1. Monitor Llama-3.2-3B access request status with Meta
2. Request access to Llama-3.3-70B at https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct
3. Once Llama access is approved, retry conversions (70B models may hit memory limits)
4. Consider model registry updates to reflect Gemma support

## Cross-Agent Impact

- **Trinity (Core Dev):** Needs to update `KnownModels.cs` with 3 Gemma model entries
- **Morpheus (Lead):** New decision: Gemma architecture is supported; consider adding Gemma models to recommended tier list
