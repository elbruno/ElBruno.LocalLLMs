# Decision: Colab Notebook Uses Unsloth Merge Instead of merge_lora.py

**Author:** Dozer (ML Engineer)
**Date:** 2025-07-26
**Status:** Implemented

## Context

The Colab notebook needed a LoRA merge step. We have `merge_lora.py` which uses PEFT's `PeftModel.merge_and_unload()`, but in the notebook context the model is already loaded in Unsloth's `FastLanguageModel`.

## Decision

Use Unsloth's `model.save_pretrained_merged(dir, tokenizer, save_method="merged_16bit")` instead of reimplementing the PEFT merge flow.

## Rationale

- Model is already in GPU memory from training — no need to reload from disk
- Unsloth's merge handles the adapter-to-dense conversion internally
- Simpler code in the notebook (1 call vs. loading base model + PEFT + merge + save)
- Output is identical: a standard HuggingFace checkpoint in FP16

## Impact

- Only affects the Colab notebook. The standalone `merge_lora.py` script remains unchanged for local workflows.
