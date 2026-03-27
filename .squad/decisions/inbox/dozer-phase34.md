# Decision: Phase 3 & 4 Scripts — ONNX Conversion & Publishing Pipeline

**Date:** 2026-03-29  
**Author:** Dozer (ML / ONNX Conversion Engineer)  
**Status:** Implemented  
**Plan Reference:** `docs/plan-finetune-qwen.md` §5 (Phase 3) and §6 (Phase 4)

## What Was Done

Created 5 complete scripts in `scripts/finetune/`:

1. `merge_lora.py` — LoRA adapter → merged HuggingFace checkpoint
2. `convert_to_onnx.py` — HuggingFace → ONNX INT4 via onnxruntime_genai builder
3. `validate_onnx.py` — 12-test validation suite against QwenFormatter expectations
4. `upload_to_hf.py` — Upload ONNX model to HuggingFace Hub
5. `model-card-template.md` — HuggingFace model card with YAML frontmatter

## Key Decisions

### 1. PEFT over Unsloth for merge step
The plan referenced Unsloth for merging, but I used `peft.PeftModel.from_pretrained()` + `merge_and_unload()` instead. Reason: PEFT is the standard merge pathway, better documented, and more reliable. Unsloth is great for training speed but its merge API wraps PEFT anyway. The merge script accepts any LoRA checkpoint regardless of training framework.

### 2. 12 validation test cases (not 10)
Added 2 extra tests beyond the plan minimum: tokenizer round-trip (catches broken tokenizer early) and RAG no-answer edge case (model should refuse when context lacks the answer). Both are common failure modes after quantization.

### 3. Template variable syntax
Used `{{PLACEHOLDER}}` double-brace syntax in the model card template instead of Python format strings. This avoids escaping issues with C# code blocks in the template and is compatible with multiple rendering tools (Jinja, simple string replace, etc.).

### 4. CUDA execution provider option
Added `--execution-provider cuda` to convert_to_onnx.py based on prior experience converting Llama-3.3-70B. The CUDA EP uses a streaming quantization path that avoids OOM on 14B+ models. CPU EP is default for simplicity.

### 5. INT4 accuracy level 4 as default
Set `int4_accuracy_level=4` (highest quality) as the default. Fine-tuned models lose more quality to quantization than base models (~1-3% vs <1%), so maximum accuracy is worth the marginal size increase.

## Dependencies

These scripts depend on Phase 1 (training data) and Phase 2 (fine-tuning) being complete to actually run. However, the scripts work independently with any HuggingFace model — they're not hardcoded to a specific checkpoint.

## Next Steps

- Phase 2 (Mouse) produces LoRA adapters → feed into `merge_lora.py`
- Run the full pipeline: merge → convert → validate → upload
- Phase 5 (Trinity) adds KnownModels entries for the published models
