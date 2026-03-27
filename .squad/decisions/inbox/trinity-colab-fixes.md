# Decision: Colab Notebook Dependency & Compatibility Fixes

**Author:** Trinity (Core Developer)
**Date:** 2025-07-25
**Status:** Applied

## Context

The `scripts/finetune/train_and_publish.ipynb` notebook had 4 execution errors on Google Colab:

1. `--no-deps` flag prevented transitive dependencies from installing, breaking `from datasets import load_dataset`
2. Outdated Unsloth install URL (`unsloth[colab-new] @ git+...`) replaced with `pip install unsloth`
3. `evaluation_strategy` deprecated in newer transformers — changed to `eval_strategy`
4. `onnxruntime-genai` import crashes without graceful fallback on environments where it's unavailable

## Decision

- Simplified install cell to use `pip install unsloth` (their current recommended approach) which pulls in all transitive deps
- Added `|| echo` fallback for onnxruntime-genai install
- Added try/except guards around onnxruntime-genai imports in both the conversion and validation cells
- Wrapped entire validation body in `if og is not None:` guard so the notebook completes even without onnxruntime-genai

## Rationale

Colab environments are ephemeral and dependency availability varies. Defensive imports with clear warning messages are preferable to hard crashes, especially for .NET developers who may not be familiar with Python debugging.
