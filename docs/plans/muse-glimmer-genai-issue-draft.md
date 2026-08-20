# Issue Draft: Muse Glimmer 30B Support for onnxruntime-genai

> **Target repo:** https://github.com/microsoft/onnxruntime-genai/issues
> **Type:** Feature Request
> **Review before submitting** — this is a draft, not yet posted.
> **Companion doc:** `docs/blocked-models.md` § "Meta Muse Glimmer 30B"
> **Modelled on:** `docs/plans/gemma4-genai-issue-draft.md` (issue #2062 in this repo's history)

---

## Title

Feature Request: Support for Meta Muse Glimmer 30B (gated GQA attention, multimodal wrapper prefix)

## Body

### Summary

Meta released **Muse Glimmer 30B** (`meta-models/Muse-Glimmer-30B`, Apache 2.0, announced 2026-08-10) as part of an open-weight push alongside Muse Spark 1.2. It is a 30B dense model — a 2B ViT "Perception Encoder" plus a 28B text decoder — and ships day-0 on transformers, llama.cpp, vLLM (transformers backend), MLX, and ExecuTorch. **No ONNX artifacts exist, official or community.**

I'm the author of [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs), a C# library that uses ONNX Runtime GenAI for local LLM inference via `IChatClient`. Before attempting conversion, I checked the model's verified `config.json` and `model.safetensors.index.json` against the installed `onnxruntime_genai` model builder (v0.14.1 locally; this project pins v0.15.1) and against the `main` branch source. All three architectural novelties turn out to have some precedent in the builder; the blocker is that the gated-attention precedent doesn't match Muse Glimmer's tensor layout and the model isn't dispatched at all.

### What I Found

**1. The SWA/NoPE attention *pattern* already has a precedent — `SmolLM3Model`.**

Verified `text_config` (https://huggingface.co/meta-models/Muse-Glimmer-30B/resolve/main/config.json):

```json
"num_hidden_layers": 52,
"num_attention_heads": 32,
"num_key_value_heads": 2,
"head_dim": 128,
"layer_types": ["sliding_attention","sliding_attention","sliding_attention","full_attention", /* × 13 */],
"layer_rope_theta": [500000.0, 500000.0, 500000.0, 0, /* × 13, 0 = NoPE on full-attention layers */],
"sliding_window": 2048
```

This is 52 layers in a repeating `(SWA, SWA, SWA, Full) × 13` pattern, with every 4th (full-attention) layer running with **no positional embedding** (`layer_rope_theta = 0`). `head_dim` is a **uniform 128** across all layers — this is *not* the Gemma 4 variable-head-dim failure class (`docs/blocked-models.md` documents that one from this project's history).

`src/python/py/models/builders/smollm.py`'s `SmolLM3Model.make_attention` already implements exactly this shape: it reads `config.layer_types` and `config.no_rope_layers` per layer, temporarily toggles `attention_attrs["use_rope_in_attn"]` and `window_size`, calls the base `make_attention`, then restores the originals. Muse Glimmer's `layer_types`/`layer_rope_theta` arrays map onto this mechanism directly — the SWA/NoPE alternation is not, by itself, a blocker.

**2. Gated attention has a builder precedent, but not one that consumes Muse Glimmer's tensor layout — this is the actual blocker.**

`model.safetensors.index.json` (https://huggingface.co/meta-models/Muse-Glimmer-30B/resolve/main/model.safetensors.index.json) shows every decoder layer's attention block as:

```
model.language_model.layers.{i}.self_attn.q_proj.weight
model.language_model.layers.{i}.self_attn.k_proj.weight
model.language_model.layers.{i}.self_attn.v_proj.weight
model.language_model.layers.{i}.self_attn.o_proj.weight
model.language_model.layers.{i}.self_attn.gate_proj.weight   <-- separate tensor, not folded into q_proj
```

`self_attn.gate_proj` is a fourth learned projection, but output-gated attention itself is not new to the builder: `src/python/py/models/builders/qwen.py`'s `Qwen35TextModel._make_full_attention` (v0.15.1/`main`) already builds a gated-attention subgraph — it splits a doubled Q projection into a `Q` path and a `gate` path per head, runs `GroupQueryAttention`, then multiplies the attention output by `Sigmoid(gate)` before `o_proj`. `GPTOSSModel.attention_attrs["sinks"]` is unrelated (an attention-sink bias term, not an output gate). The gap for Muse Glimmer is that its gate arrives as its own `gate_proj` weight rather than a doubled Q output, so the existing `Sigmoid`/`Mul` gating subgraph needs to be adapted to read `gate_proj` directly — an implementation gap, not the invention of a new attention mechanism.

**3. Weights sit behind the same multimodal prefix that blocked Gemma 4 in this project.**

Every decoder tensor above is namespaced under `model.language_model.*` — identical in shape to the `Gemma4ForConditionalGeneration` wrapper prefix documented in this project's prior issue (see "Related" below). Whether a text-only extraction is viable (ignoring `vision_config`'s separate 50-layer ViT tower) has not been validated.

**4. Dispatch findings — v0.15.1 and `main` (checked 2026-08-12).**

Neither the `v0.15.1` tag nor `main`'s `src/python/py/models/builder.py` contains a case for `config.architectures[0] == "MuseGlimmerForConditionalGeneration"` or any `model_type == "muse_glimmer*"` branch, and no `MuseGlimmer*` class exists under `src/python/py/models/builders/`. The only difference between `v0.15.1` and `main` relevant to hybrid/gated architectures is `main`'s addition of `GraniteMoeHybridForCausalLM` — an unrelated architecture string that would not match Muse Glimmer's config regardless.

### Muse Glimmer 30B Model Details

| Property | Value |
|---|---|
| HF repo (BF16) | `meta-models/Muse-Glimmer-30B` |
| Architecture | `MuseGlimmerForConditionalGeneration` / `AutoModelForMultimodalLM` |
| Params | 30B dense — 2B ViT "Perception Encoder" + 28B text decoder |
| Text decoder | 52 layers, `(SWA, SWA, SWA, Full) × 13`, sliding window 2048, NoPE on full-attention layers, uniform `head_dim=128`, GQA 16:1 (`num_attention_heads=32`, `num_key_value_heads=2`) |
| Attention | Gated GQA via `self_attn.gate_proj`, plus Q-K RMSNorm and `qk_scale_factor: 3.87` |
| Vision tower | `muse_glimmer_vision`, 50 layers (`window_attention × 3, full_attention × 1` repeating), `patch_size=14`, `patch_temporal=2`, `merge_size=2` (2×2 pixel shuffle), interpolated absolute position embeddings (`pos_emb_height/width=32`), 2D RoPE (`rope_theta=10000`) |
| Weight prefix | `model.language_model.*` for the decoder (multimodal wrapper, same class of issue as Gemma 4) |
| License | Apache 2.0 |
| Day-0 support | transformers, llama.cpp (incl. DFlash drafter), vLLM (transformers backend), MLX, ExecuTorch. **No ONNX.** |

### What Would Need to Change

1. **Builder (`builder.py` / `builders/`):**
   - Add a `MuseGlimmerForConditionalGeneration` (or `muse_glimmer_text`) dispatch case.
   - Adapt `Qwen35TextModel._make_full_attention`'s existing `Sigmoid`/`Mul` output-gating subgraph (or add a sibling attention builder class) to read `self_attn.gate_proj` directly instead of splitting a doubled Q projection, alongside the existing per-layer conditional-RoPE/window mechanism already proven by `SmolLM3Model`.
   - Handle the `model.language_model.*` prefix for text-only extraction, or add the vision tower (`muse_glimmer_vision`) as a fourth multimodal builder alongside the existing Phi3V/Phi4MM/Qwen-VL family.

2. **Runtime (C++):** No new execution path is required — Qwen3.5's gated attention already runs as a `GroupQueryAttention` node followed by `Sigmoid`/`Mul` ONNX ops, which the runtime already executes. The work is graph-construction (builder-side), not a new C++ op.

3. **Config (`genai_config.json`):** Likely no new schema — Qwen3.5's gating is inferred from the model class rather than a config flag, and the SWA/NoPE per-layer fields are already representable via the `SmolLM3Model` precedent.

### Environment

- **onnxruntime-genai:** 0.14.1 (installed locally); this project pins 0.15.1
- **Python:** verified against `builder.py` on the `v0.15.1` tag and `main` (both fetched 2026-08-12)
- **OS:** Windows 11

### Related

- This project's prior issue draft for Gemma 4 (`docs/plans/gemma4-genai-issue-draft.md`) — same class of multimodal-wrapper-prefix and single-valued-config concerns, different specific blocker (PLE/variable head dims there vs. gated attention here).
- `docs/blocked-models.md` § "Meta Muse Glimmer 30B" — full findings and recommended alternatives for this project.

---

## To submit

Run:
```bash
gh issue create --repo microsoft/onnxruntime-genai \
  --title "Feature Request: Support for Meta Muse Glimmer 30B (gated GQA attention, multimodal wrapper prefix)" \
  --body-file docs/plans/muse-glimmer-genai-issue-draft.md \
  --label "enhancement"
```

> **Note:** May require SAML SSO authorization for the Microsoft org. Visit the URL shown in the error message to authorize.
> **Do not** set up a scheduled/polling monitor workflow for this issue. Track it as a single terminal-state issue, following the retirement lesson from the Gemma 4 monitor (`docs/blocked-models.md` § "Retirement note").
