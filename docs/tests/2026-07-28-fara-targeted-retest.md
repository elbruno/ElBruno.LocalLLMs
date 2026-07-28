# Fara1.5-9B Targeted Retest

**Date:** 2026-07-28 UTC  
**Scope:** Fara-only validation after uploading missing processor config files to `elbruno/Fara1.5-9B-onnx`

## Results

- **Unit tests:** PASS — 39/39 Fara-targeted tests
- **Vision lifecycle test:** FAIL — `VisionLifecycleTests.VisionModel_FullLifecycle_DownloadInferenceCacheHitDelete`

## Failure Summary

The retest progressed past the missing `processor_config.json` problem, but it still failed during model load:

```text
Microsoft.ML.OnnxRuntimeGenAI.OnnxRuntimeGenAIException
Load model <temp>\fara1.5-9b\ failed. File doesn't exist
```

The exception occurs in `new Model(modelPath)` inside `OnnxVisionModel`, before `MultiModalProcessor` initialization.

## Root Cause

The current Hugging Face package is still the wrong ORT-GenAI export shape.

- Current repo shape: single-file `qwen3_5` export (`model.onnx` + `model.onnx.data`)
- Required repo shape: `qwen_vl` three-stage VLM export
  - `vision_encoder.onnx`
  - `embedding_injector.onnx`
  - `text_decoder.onnx`
- `genai_config.json` must report `model.type = "qwen_vl"`

This matches the repository ADR notes in `.squad/decisions.md` and `OnnxVisionModel`'s own warning path.

## Action Taken

- Re-added `KnownModels.Fara15_9B` to `KnownExportIssueModelIds`
- Updated `scripts/convert_fara.py` to force `--model_type qwen_vl`
- Updated Fara docs/comments to reflect the real republish requirement

## Next Step

Regenerate `elbruno/Fara1.5-9B-onnx` with the corrected `qwen_vl` export, upload the new artifact set, and rerun the focused Fara lifecycle test.
