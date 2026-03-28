# Decision: MaxSequenceLength reports effective runtime limit

**Author:** Trinity  
**Date:** 2025-07-25  
**Issue:** #5  
**PR:** #6  

## Context

`ModelMetadata.MaxSequenceLength` returned the raw `genai_config.json` value (e.g. 131,072 for Phi-3.5 mini). The ONNX Runtime GenAI Generator enforces `GenerationParameters.MaxLength` (default 2048), so the reported value was ~64x too large.

## Decision

- `MaxSequenceLength` = `min(config_value, options.MaxSequenceLength)` — the effective limit
- `ConfigMaxSequenceLength` (new) = raw config value — for consumers needing theoretical context window
- Version bump to 0.7.0 (new public API property)

## Rationale

Downstream consumers (e.g. PromptDistiller) used `MaxSequenceLength` to calculate safe prompt sizes. The inflated value caused them to send prompts far exceeding the runtime limit. The fix makes the default behavior correct while preserving the raw value for advanced use.
