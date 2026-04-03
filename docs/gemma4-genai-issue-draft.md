# Issue Draft: Gemma 4 Support for onnxruntime-genai

> **Target repo:** https://github.com/microsoft/onnxruntime-genai/issues
> **Type:** Feature Request
> **Review before submitting** — this is a draft, not yet posted.

---

## Title

Feature Request: Support for Google Gemma 4 model family (PLE architecture, variable head dims, KV cache sharing)

## Body

### Summary

Google released the [Gemma 4 model family](https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/) (April 2, 2026) with four variants (E2B, E4B, 26B-A4B MoE, 31B). All are Apache 2.0 licensed and use a novel architecture that introduces three features not currently supported by `onnxruntime-genai` v0.12.2.

I'm the author of [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs), a C# library that uses ONNX Runtime GenAI for local LLM inference via `IChatClient`. I've spent significant time attempting to convert Gemma 4 models and have detailed technical findings on what's blocking support.

### What I Tried

1. **Patched `builder.py`** to route `Gemma4ForConditionalGeneration` through the Gemma 3 pipeline — conversion produced a 1.6GB ONNX file (INT4), but the runtime failed at load time:
   ```
   ShapeInferenceError: Incompatible dimensions for matrix multiplication
   at /model/layers.4/attn/o_proj/MatMul_Q4
   ```
   Layer 4 is a full-attention layer with `global_head_dim=512`, but the Gemma 3 model builder created the graph assuming uniform `head_dim=256`.

2. **Changed `genai_config.json` model type** from `gemma4` to `gemma3_text` — same shape error.

3. **Examined `onnx-community/gemma-4-E2B-it-ONNX`** — correct ONNX graph structure, but uses a different I/O contract (separate `embed_tokens.onnx` + `decoder_model_merged.onnx` with `per_layer_inputs` tensor) that's incompatible with GenAI's KV cache management.

4. **Attempted `Gemma4ForCausalLM` loading** — weights stored under `model.language_model.*` prefix (multimodal wrapper), causing weight mismatch.

### Three Architectural Blockers

#### 1. Per-Layer Embeddings (PLE)

Gemma 4's `embed_tokens` produces TWO outputs:
- `inputs_embeds`: `[batch, seq, hidden_size]` (standard)
- `per_layer_inputs`: `[batch, seq, num_hidden_layers, hidden_size_per_layer_input]` — e.g., `[batch, seq, 35, 256]` for E2B

Each transformer layer receives its own slice from `per_layer_inputs`. The GenAI runtime currently expects a single embedding tensor flowing into the decoder stack.

**Config reference (E2B):**
```json
"hidden_size_per_layer_input": 256,
"num_hidden_layers": 35
```

#### 2. Variable Attention Head Dimensions

Gemma 4 uses two different head dimensions depending on the attention type:
- **Sliding attention** (most layers): `head_dim = 256`
- **Full attention** (every 5th layer — indices 4, 9, 14, 19, 24, 29, 34): `global_head_dim = 512`

The `genai_config.json` schema only supports a single `head_size` field. The KV cache buffer allocation uses this single value for all layers, causing shape mismatches at full-attention layers.

**Config reference:**
```json
"head_dim": 256,
"global_head_dim": 512,
"attention_pattern": [0, 0, 0, 0, 1, 0, 0, 0, 0, 1, ...]  // 0=sliding, 1=full
```

#### 3. KV Cache Sharing

Gemma 4 E2B has 35 decoder layers but only 15 unique KV cache pairs. Multiple layers share the same KV cache through a sharing pattern:

```json
"num_kv_shared_layers": 20
```

The runtime expects one KV cache I/O pair per layer (`past_key_values.0` through `past_key_values.N`). With sharing, the ONNX model only produces 15 unique KV outputs for 35 layers.

### Gemma 4 Model Details

| Model | Total Params | Effective Params | Architecture | Context | HuggingFace |
|-------|-------------|-----------------|-------------|---------|-------------|
| E2B IT | 5.1B | 2.3B | Dense + PLE | 128K | `google/gemma-4-E2B-it` |
| E4B IT | 8B | 4.5B | Dense + PLE | 128K | `google/gemma-4-E4B-it` |
| 26B-A4B IT | 25.2B | 3.8B active | MoE + PLE | 256K | `google/gemma-4-26B-A4B-it` |
| 31B IT | 30.7B | 30.7B | Dense | 256K | `google/gemma-4-31B-it` |

All variants use:
- `architectures: ["Gemma4ForConditionalGeneration"]` (multimodal — no text-only `CausalLM` variant exists)
- Same chat template as Gemma 2/3: `<start_of_turn>role\ncontent<end_of_turn>`
- Apache 2.0 license (no gating)
- RoPE with separate theta values for sliding (10000.0) and full attention (1000000.0)

### What Would Need to Change

Based on my investigation, support would require:

1. **Builder (`builder.py`):** Add `Gemma4ForConditionalGeneration` case that handles:
   - Extracting `text_config` from the multimodal config
   - Generating ONNX graph with `per_layer_inputs` as an additional input
   - Creating per-layer attention nodes with correct head dimensions (256 vs 512)
   - Mapping shared KV caches correctly

2. **Runtime (C++):** 
   - Support `per_layer_inputs` tensor as model input alongside `input_ids`/`inputs_embeds`
   - Handle variable-size KV cache buffers per layer (or allocate to max and slice)
   - Support KV cache sharing (N layers → M unique caches where M < N)

3. **Config (`genai_config.json`):**
   - Support per-layer `head_size` (or `head_size` + `global_head_size`)
   - Support `num_kv_shared_layers` or a KV sharing map
   - New model type `gemma4_text` with PLE-aware inference loop

### Environment

- **onnxruntime-genai:** 0.12.2 (latest)
- **transformers:** 5.5.0
- **Python:** 3.13.9
- **OS:** Windows 11
- **GPU:** NVIDIA A10-24Q (24GB VRAM)

### Related

- #2059 — General question about Gemma 4 support (closed, no technical details)
- [onnx-community/gemma-4-E2B-it-ONNX](https://huggingface.co/onnx-community/gemma-4-E2B-it-ONNX) — Transformers.js ONNX export (not GenAI-compatible)

---

## To submit

Run:
```bash
gh issue create --repo microsoft/onnxruntime-genai \
  --title "Feature Request: Support for Google Gemma 4 model family (PLE architecture, variable head dims, KV cache sharing)" \
  --body-file docs/gemma4-genai-issue-draft.md \
  --label "enhancement"
```

> **Note:** May require SAML SSO authorization for the Microsoft org. Visit the URL shown in the error message to authorize.
