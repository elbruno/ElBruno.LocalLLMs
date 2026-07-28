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
- Verified builder behavior in ORT-GenAI 0.14.1:
  - `builder.py` maps `Qwen3_5ForConditionalGeneration` to `Qwen35TextModel`
  - `Qwen35TextModel` forces `exclude_embeds=True` by default
  - the builder therefore emits only the decoder/text path for Fara
- Result: the published ONNX artifact is incomplete for `LocalVisionChatClient`

This moved the diagnosis from "missing processor files" to a confirmed upstream builder limitation.

## Action Taken

- Re-added `KnownModels.Fara15_9B` to `KnownExportIssueModelIds`
- Updated `scripts/convert_fara.py` to detect and block the known decoder-only builder path
- Updated Fara docs/comments to reflect the confirmed ORT-GenAI limitation

## Next Step

Wait for upstream ORT-GenAI support for a full Fara/Qwen3.5-VL export, or implement a custom multimodal export pipeline, then regenerate the Hugging Face artifact and rerun the focused Fara lifecycle test.
