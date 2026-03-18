# Decision: Gemma Models Now Use Native ONNX Repos

**Date:** 2026-03-18
**Author:** Trinity (Core Dev)
**Status:** Implemented

## Context

Dozer converted 3 Gemma models to ONNX GenAI format and uploaded them to elbruno HuggingFace repos. KnownModels.cs previously pointed at Google's original repos with `HasNativeOnnx = false`.

## Decision

Updated all 3 Gemma entries in KnownModels to point at elbruno ONNX repos with `RequiredFiles = ["*"]` and `HasNativeOnnx = true`:
- `elbruno/Gemma-2B-IT-onnx` (Tiny tier)
- `elbruno/Gemma-2-2B-IT-onnx` (Small tier)
- `elbruno/Gemma-2-9B-IT-onnx` (Medium tier)

## Rationale

- GenAI format repos contain all necessary files (model, tokenizer, genai_config.json), so `["*"]` is the correct RequiredFiles pattern
- Consistent with how all other converted models (TinyLlama, Qwen, Llama-3.1, Mistral, etc.) are configured
- Users no longer need to run ONNX conversion for any Gemma v1/v2 model

## Impact

- Gemma models now work out of the box (no conversion step)
- 16 of 23 KnownModels now have native ONNX — only 7 still need conversion
- Remaining blocked Llama models (3.2-3B, 3.3-70B) are gated by Meta license, not architecture
