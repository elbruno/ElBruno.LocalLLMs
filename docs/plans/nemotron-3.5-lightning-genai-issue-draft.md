# Issue Draft: Nemotron 3.5 Lightning 30B-A3B Support for onnxruntime-genai

> **Target repo:** https://github.com/microsoft/onnxruntime-genai/issues
> **Type:** Feature Request
> **Review before submitting** — this is a draft, not yet posted.
> **Companion doc:** `docs/blocked-models.md` § "NVIDIA Nemotron 3.5 Lightning 30B-A3B"
> **Modelled on:** `docs/plans/gemma4-genai-issue-draft.md` (issue #2062 in this repo's history)

---

## Title

Feature Request: Support for NVIDIA Nemotron 3.5 Lightning 30B-A3B (`nemotron_h`: Mamba-2 + MoE + MTP hybrid)

## Body

### Summary

NVIDIA released **Nemotron 3.5 Lightning 30B-A3B** (`nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16`, announced 2026-08-11), the smallest member of the Nemotron 3 family: 30B total parameters, 3B active per token, hybrid Mamba-2 SSM + MoE + attention layers, with Multi-Token Prediction (MTP) heads baked into the checkpoint. It ships day-0 on vLLM, SGLang, TensorRT-LLM, llama.cpp, Ollama, LM Studio, and Unsloth. **No ONNX artifacts exist.**

I'm the author of [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs), a C# library that uses ONNX Runtime GenAI for local LLM inference via `IChatClient`. Before assuming "Nemotron" being in the builder's README implied any precedent, I verified the model's actual `config.json` against the installed `onnxruntime_genai` builder (v0.14.1 locally; this project pins v0.15.1) and against `main`. **The precedent does not exist — the two "Nemotron" names refer to unrelated architectures.**

### What I Found

**1. This is a different architecture from the "Nemotron" the builder already dispatches.**

Verified `config.json` (https://huggingface.co/nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16/resolve/main/config.json):

```json
"architectures": ["NemotronHForCausalLM"],
"model_type": "nemotron_h",
```

`src/python/py/models/builder.py` — on **both** the `v0.15.1` tag (this project's pin) and `main` (checked 2026-08-12) — dispatches only:

```python
elif config.architectures[0] == "NemotronForCausalLM":
    onnx_model = NemotronModel(config, io_dtype, onnx_dtype, execution_provider, cache_dir, extra_options)
```

`NemotronModel` (`builders/nemotron.py`) is a thin `LlamaModel` subclass — it sets `layernorm_attrs["add_offset"] = 1` and overrides the MLP projection naming, nothing else. It is dense, uniform-attention, and has no Mamba, no MoE, no per-layer block-type awareness. `"NemotronHForCausalLM"` / `"nemotron_h"` do not appear anywhere in the builder's dispatch table or class list in either version. **The two "Nemotron" names are unrelated architectures that happen to share a vendor prefix; the README listing "Nemotron" provides no actual precedent for this model.**

**2. Three distinct architectural features have no builder support at all.**

Verified `config.json` fields:

```json
"mamba_head_dim": 64, "mamba_num_heads": 64, "ssm_state_size": 128,
"conv_kernel": 4, "use_mamba_kernels": true, "mamba_ssm_cache_dtype": "float32",

"n_routed_experts": 128, "num_experts_per_tok": 6, "n_shared_experts": 1,
"moe_shared_expert_intermediate_size": 3712, "moe_shared_expert_overlap": true,

"num_nextn_predict_layers": 1, "mtp_layers_block_type": ["attention", "moe"],

"layers_block_type": [
  "mamba","moe","mamba","moe","mamba","attention","moe","mamba","moe","mamba",
  "moe","mamba","attention", /* ... 52 entries total, only 6 are "attention" ... */
]
```

- **Mamba-2 SSM layers.** I grepped the installed v0.14.1 build and `main`'s `src/python/py/models/builders/base.py` plus every class registered in `builders/__init__.py` (checked 2026-08-12) for `mamba`, `ssm`, and `conv1d`: **zero matches anywhere**. GenAI's cache manager only implements transformer KV cache; there is no SSM recurrent-state cache path to attach `ssm_state_size`/`conv_kernel`/`mamba_ssm_cache_dtype` to.
- **Sparse MoE routing.** Real QMoE routing exists in the builder (`GPTOSSModel`, via `moe_attrs` and a QMoE op in `base.py`), but it is CUDA-only and forces `--precision int4`.
- **`layers_block_type`.** `genai_config.json` has no per-layer block-type field; the builder assumes a single uniform layer kind for the whole model. This model has three interleaved kinds (`mamba`/`moe`/`attention`) across 52 layers, with only 6 being plain attention.
- **MTP heads** (`num_nextn_predict_layers`, `mtp_layers_block_type`) have no export path in the builder and would need to be stripped and documented before any conversion attempt, per this project's standing conversion practice.

**3. The `main`-branch "Granite MoE Hybrid" precedent does not transfer, either.**

`main` (not present in `v0.15.1`) adds a case for `GraniteMoeHybridForCausalLM` → `GraniteMoeHybridModel` (`builders/granite.py`). I inspected this class: it overrides `make_layer` to route through `layer.shared_mlp` — Granite Hybrid's **always-on dense MLP** — and does not touch the conditionally-routed experts or any Mamba/SSM state. It inherits `make_attention` unmodified from `GraniteModel`/`MistralModel`, i.e. every layer is assumed to be standard attention. **This is MoE support in name only** — it provides no Mamba-2 state-cache precedent and no genuine per-token expert-routing precedent that would transfer to `nemotron_h`.

### Nemotron 3.5 Lightning 30B-A3B Model Details

| Property | Value |
|---|---|
| HF repos | `nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16` (reference), `-NVFP4` (inference), `-Base-BF16`, plus DFlash/DSpark drafter variants |
| Architecture | `NemotronHForCausalLM`, `model_type: nemotron_h` — hybrid Mamba-2 + MoE + attention |
| Params | 30B total, 3B active |
| Layer schedule | 52 layers per `layers_block_type`: interleaved `mamba` / `moe` / `attention` (only 6 of 52 are plain attention) |
| Mamba-2 | `mamba_head_dim=64`, `mamba_num_heads=64`, `ssm_state_size=128`, `conv_kernel=4` |
| MoE | `n_routed_experts=128`, `num_experts_per_tok=6`, `n_shared_experts=1` (shared + routed experts) |
| MTP | `num_nextn_predict_layers=1`, baked into the checkpoint via continued pretraining |
| Context | Up to 1M tokens (256K recommended for single-H100 deployment) |
| Numerics | Reference weights BF16; `-NVFP4` release uses NVIDIA-specific 4-bit kernels, not an ONNX quantization format |
| License | **OpenMDW-1.1** — see below |
| Day-0 support | vLLM, SGLang, TensorRT-LLM, llama.cpp, Ollama, LM Studio, Unsloth. **No ONNX.** |

### License: OpenMDW-1.1

Verified from https://raw.githubusercontent.com/OpenMDW/OpenMDW/main/1.1/LICENSE.OpenMDW-1.1 (fetched 2026-08-12). Permissive (no copyleft, no output-use restriction), but distinct from every other license currently in this project's catalog (Apache 2.0 / MIT / Llama gated) in requiring an explicit redistribution notice:

> "If you distribute any portion of the Model Materials, you shall retain in your distribution (1) a copy of this agreement, and (2) all copyright notices and other notices of origin included in the Model Materials that are applicable to your distribution."

This does not affect the ONNX Runtime GenAI feature request itself, but it is a hard prerequisite this project must satisfy (a `LICENSE` + `NOTICE` file pair) before publishing any converted `elbruno/*-onnx` artifact for this model in the future.

### What Would Need to Change

1. **Builder:** A `NemotronHForCausalLM` (`nemotron_h`) dispatch case that:
   - Reads `layers_block_type` and constructs the correct block (Mamba-2 SSM / MoE / attention) per layer index, instead of assuming a single uniform layer kind.
   - Implements true per-token MoE routing for `n_routed_experts=128` / `num_experts_per_tok=6` with shared-expert overlap (`moe_shared_expert_overlap`) — not the Granite MoE Hybrid dense-only shortcut.
   - Adds Mamba-2 SSM weight export (`conv1d`, in-projection/out-projection, discretization parameters) and a runtime-compatible cache slot for `ssm_state_size=128`.
2. **Runtime (C++):** Mamba-2 recurrent-state caching alongside the existing transformer KV cache, since layers of both kinds are interleaved in the same model.
3. **Config (`genai_config.json`):** A `layers_block_type`-aware model type, since single-valued fields cannot represent this interleave (the same failure class documented for Gemma 4 in this project's history, applied to a three-way interleave instead of a two-way one).
4. **MTP handling:** A documented path for stripping or (eventually) using the `num_nextn_predict_layers` / `mtp_layers_block_type` heads.

### Environment

- **onnxruntime-genai:** 0.14.1 (installed locally); this project pins 0.15.1
- **Python:** verified against `builder.py` on the `v0.15.1` tag and `main` (both fetched 2026-08-12)
- **OS:** Windows 11

### Related

- `docs/blocked-models.md` § "Mixtral-8x7B-Instruct-v0.1" — the original MoE-routing blocker this project has tracked; `nemotron_h` compounds it with Mamba-2 and MTP.
- `docs/blocked-models.md` § "NVIDIA Nemotron 3.5 Lightning 30B-A3B" — full findings, license notes, and recommended alternatives for this project.

---

## To submit

Run:
```bash
gh issue create --repo microsoft/onnxruntime-genai \
  --title "Feature Request: Support for NVIDIA Nemotron 3.5 Lightning 30B-A3B (\`nemotron_h\`: Mamba-2 + MoE + MTP hybrid)" \
  --body-file docs/plans/nemotron-3.5-lightning-genai-issue-draft.md \
  --label "enhancement"
```

> **Note:** May require SAML SSO authorization for the Microsoft org. Visit the URL shown in the error message to authorize.
> **Do not** set up a scheduled/polling monitor workflow for this issue. Track it as a single terminal-state issue, following the retirement lesson from the Gemma 4 monitor (`docs/blocked-models.md` § "Retirement note").
