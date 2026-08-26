# Blocked Models Reference

This document details models that cannot be converted to ONNX yet, along with the specific blockers, workarounds, and when they might become available.

---

## ⚠️ Llama Gated Model Status

Meta's Llama models use **per-model license gates** on HuggingFace. Having access to one Llama model does **not** grant access to others:

- **Llama-3.2-3B-Instruct** — ✅ **DONE** — License accepted, converted, and uploaded to `elbruno/Llama-3.2-3B-Instruct-onnx`.
- **Llama-3.3-70B-Instruct** — ✅ **DONE** — License accepted, converted to INT4 ONNX using CUDA execution provider (CPU OOM'd at ~440GB, CUDA succeeded), and uploaded to `elbruno/Llama-3.3-70B-Instruct-onnx` (39.3 GB).

Use `Llama-3.1-8B-Instruct` (already converted, native ONNX) or `Llama-3.2-3B-Instruct` as smaller alternatives.

---

## Quick Summary

| Model | Params | Blocker | Status | Next Step |
|-------|--------|---------|--------|-----------|
| **Gemma-4 Family** | 5.1B–30.7B | ~~PLE architecture not supported~~ | ✅ Conversion path only | No active monitor — convert locally with `onnxruntime-genai` v0.15.1+ |
| **StableLM-2-1.6B-Chat** | 1.6B | Unsupported architecture | ⛔ Blocked | Wait for builder support or use standard ONNX |
| **Mixtral-8x7B-Instruct-v0.1** | 46.7B (MoE) | MoE routing not supported | ⛔ Blocked | Wait for builder MoE support or use Mistral-7B |
| **DeepSeek-R1-Distill-Llama-70B** | 70B | RAM: ~450GB needed for INT4 | ⛔ Blocked | Use 512GB+ machine, cloud GPU, or smaller DeepSeek-R1-Distill-Qwen-14B |
| **Command-R (35B)** | 35B | Gated model / license issue | ⛔ Blocked | Verify HuggingFace license or use CohereForAI/c4ai-command-r-plus |
| **Llama-3.3-70B-Instruct** | 70B | ~~RAM: ~450GB needed for INT4~~ | ✅ Resolved | CUDA conversion succeeded; uploaded to elbruno/Llama-3.3-70B-Instruct-onnx |
| **Codestral-22B-v0.1** | 22B | MNPL license (non-production only) | ⛔ Blocked | Use Devstral Small 2 (Apache 2.0) or Qwen2.5-Coder-7B-Instruct instead |
| **Devstral-Small-2-24B** | 24B | No ONNX conversion path exists | ⛔ Blocked | Wait for onnxruntime-genai support or use GGUF via llama.cpp |
| **Inkling** | 975B (MoE, multimodal) | MoE + massive size + multimodal (text/image/audio) | 🔴 Not Viable | Use a hosted API (Tinker / 3rd-party inference) or a small local model |
| **Muse Glimmer 30B** | 30B (2B ViT + 28B text) | Gated GQA (`self_attn.gate_proj`) + multimodal wrapper prefix not dispatched by builder | ⛔ Blocked | Use GGUF via llama.cpp/DFlash or Gemma-2-9B-IT for local ONNX |
| **Nemotron 3.5 Lightning 30B-A3B** | 30B total / 3B active | `nemotron_h` (Mamba-2 + MoE + MTP hybrid) not dispatched; OpenMDW-1.1 license | ⛔ Blocked | Use GGUF via llama.cpp/vLLM or Qwen2.5-32B-Instruct for local ONNX |
| **Qwen3.8-Flash-Next** | 125B total / 6B active (+51B n-gram embed, +4B MTP) | `qwen4_exp` (novel hybrid: linear attention + QSA + MoE + N-gram embedding + Gated Residual) not dispatched — verified via a live builder run, see below; Qwen Community License 1.0 | ⛔ Blocked | Use GGUF via llama.cpp/Unsloth or Qwen2.5-32B-Instruct / Qwen3-14B-Instruct for local ONNX |

---

## Architecture Limitations (No Current Builder Support)

### Gemma 4 Family (E2B, E4B, 12B, 26B, 31B) — Monitor Retired, Public Artifacts Pending

**Models:**
- google/gemma-4-E2B-it (5.1B total, 2.3B effective)
- google/gemma-4-E4B-it (8B total, 4.5B effective)
- google/gemma-4-12B-it (12B unified dense, June 2026 release)
- google/gemma-4-26B-A4B-it (25.2B total, 3.8B active MoE)
- google/gemma-4-31B-it (30.7B dense)

**HuggingFace:** https://huggingface.co/google/gemma-4-E2B-it  
**License:** Apache 2.0 (open, no gating)  
**Status:** ✅ Manual conversion path only — use `onnxruntime-genai` v0.15.1+ and a local `ModelPath`; validated public `elbruno/*-onnx` repos are still pending.

#### Historical blocker (conversion path unlocked, publication still pending)

Gemma 4 introduced three architectural features that were not handled in earlier runtime versions:

| Feature | What It Does | Why It Breaks GenAI |
|---------|-------------|-------------------|
| **Per-Layer Embeddings (PLE)** | Each layer receives a separate `per_layer_inputs` [batch, seq, 35, 256] tensor | GenAI runtime expects single embedding output, no `per_layer_inputs` input |
| **Variable Head Dimensions** | Sliding attention: head_dim=256, Full attention (every 5th layer): global_head_dim=512 | `genai_config.json` has single `head_size` field — can't represent variable dims |
| **KV Cache Sharing** | 35 layers share only 15 unique KV cache pairs | Runtime allocates one KV cache per layer — can't handle shared caches |

All three were runtime-level limitations and required runtime-level support.

#### Validation path used

1. **Patched GenAI builder** to route Gemma 4 through Gemma 3 pipeline → produced 1.6GB ONNX file, but runtime failed with `ShapeInferenceError` at full attention layers (head dim mismatch)
2. **Examined onnx-community models** → correct ONNX structure but incompatible with GenAI's external KV cache management
3. **Attempted `Gemma4ForCausalLM` loading** → weights stored under multimodal prefix, mismatch
4. **Validated with newer runtime planning** → keep Gemma 4 on Python `onnxruntime-genai` v0.15.1+ for future conversion attempts; a local `v0.14.1` builder run still failed with `NotImplementedError`.

#### What's available now

- ✅ Model definitions in `KnownModels.cs` (all 5 variants)
- ✅ Chat template (GemmaFormatter works — same as Gemma 2/3)
- ✅ Conversion scripts (`scripts/convert_gemma4.py`, `scripts/convert_gemma4.ps1`)
- ✅ Unit tests (6 model + 9 tool-calling + 195 multilingual)
- ✅ Documentation (supported-models, onnx-conversion, this page)

#### Recommended Alternatives

- **Gemma-2-2B-IT** (2.6B) — ✅ converted, smallest Gemma in ONNX
- **Gemma-2-9B-IT** (9B) — ✅ converted, production Gemma quality
- **Phi-3.5-mini-instruct** (3.8B) — ✅ native ONNX, excellent for edge

#### Retirement note

- The daily monitor was retired on 2026-08-06 after the canonical tracker issue (#39) was closed.
- Keep this section as historical context only; do not re-enable schedule-based polling unless Gemma 4 becomes blocked again.
- If a future regression appears, open one canonical issue and keep it terminal-state only — no duplicate daily comments.

---

### StableLM-2-1.6B-Chat

**Model:** stabilityai/stablelm-2-zephyr-1_6b  
**Parameters:** 1.6B  
**HuggingFace:** https://huggingface.co/stabilityai/stablelm-2-zephyr-1_6b

#### Why It's Blocked

StabilityAI's custom transformer architecture is not in the list of supported architectures for `onnxruntime-genai` model builder v0.12.1. The builder only supports standard architectures (Llama, Qwen, Phi, Gemma, Mistral, etc.). StableLM's modifications to the attention mechanism and layer design fall outside this scope.

#### What You Can Do

**Option 1: Wait for Builder Update**
- Future releases of `onnxruntime-genai` may add StableLM support
- Monitor: https://github.com/microsoft/onnxruntime-genai/releases

**Option 2: Standard ONNX Export (Limited)**
- Use Hugging Face's `optimum-cli` to export to standard ONNX format:
  ```bash
  python -m optimum.exporters.onnx \
    --model_name_or_path stabilityai/stablelm-2-zephyr-1_6b \
    --output ./stablelm-onnx
  ```
- **Limitation:** Will not be compatible with ONNX Runtime GenAI (no KV cache, slower inference)
- Not recommended for this library's use case

#### Recommended Alternatives

- **Phi-3.5-mini-instruct** (3.8B) — native ONNX, better performance, architecture fully supported
- **Qwen2.5-1.5B-Instruct** (1.5B) — similar size, fully supported
- **TinyLlama-1.1B** (1.1B) — if you need something even smaller

---

## Mixture of Experts (MoE) — Not Yet Supported

### Mixtral-8x7B-Instruct-v0.1

**Model:** mistralai/Mixtral-8x7B-Instruct-v0.1  
**Parameters:** 46.7B (effective ~12.7B active per token due to MoE routing)  
**HuggingFace:** https://huggingface.co/mistralai/Mixtral-8x7B-Instruct-v0.1

#### Why It's Blocked

Mixtral uses **Mixture of Experts (MoE)** architecture: instead of a single feed-forward network per layer, it has 8 expert sub-networks with a learned router that selects 2 experts per token. This routing mechanism is fundamentally different from dense transformer models.

**The builder cannot handle:**
- Expert routing logic (which 2 experts to activate for each token)
- Dynamic computation graphs (number of active parameters varies per token)
- Proper KV cache management with expert switching

While MoE is more efficient than dense models (only ~2/8 experts active), the ONNX Runtime GenAI builder (v0.12.1) lacks the primitives to represent this.

#### What You Can Do

**Option 1: Wait for MoE Support in Builder**
- MoE is increasingly popular (Mixtral, Qwen-MoE, DeepSeek-V2, Llama-4-Scout)
- Microsoft is likely working on MoE support
- Monitor: https://github.com/microsoft/onnxruntime-genai/issues

**Option 2: Use Dense Alternative**
- **Mistral-7B-Instruct-v0.3** (7B) — already in KnownModels, native ONNX
  - Slightly smaller, but excellent quality
  - Faster on CPU (no routing overhead)
  - Trade-off: ~0.5B more parameters than Mixtral's active experts, but in practice performs very similarly

#### Recommended Alternatives

- **Mistral-7B-Instruct-v0.3** (7B) — native ONNX, excellent instruction-following, same Mistral quality
- **Qwen2.5-7B-Instruct** (7B) — native ONNX, superior coding and reasoning
- **Phi-4** (14B) — native ONNX, strongest reasoning

---

## Memory-Limited Models (Conversion Requires Massive RAM)

### DeepSeek-R1-Distill-Llama-70B

**Model:** deepseek-ai/DeepSeek-R1-Distill-Llama-70B  
**Parameters:** 70B  
**HuggingFace:** https://huggingface.co/deepseek-ai/DeepSeek-R1-Distill-Llama-70B

#### Why It's Blocked

During ONNX conversion (especially with INT4 quantization), the entire model weights must be loaded into memory for processing. A 70B parameter model in FP32 requires approximately **280 GB of RAM**. With overhead and intermediate tensors during quantization, **~450 GB total RAM is needed**.

Most consumer and even enterprise machines have only 64–256 GB of RAM. Even if you have 512 GB, operating at the limit causes severe performance degradation.

#### Disk Space Also Matters

| Stage | Space |
|-------|-------|
| Raw download | ~140 GB |
| During conversion | ~450 GB |
| Final INT4 | ~40 GB |

You need ~500 GB free disk space during conversion.

#### What You Can Do

**Option 1: High-Memory Machine**
- Machines with 512+ GB RAM (very rare):
  - High-end workstations ($50k+)
  - Data center systems (AWS, Azure, GCP)
- Conversion time: ~2–4 hours on CPU, ~30–60 min on GPU

**Option 2: Cloud GPU Instances**
- **Azure ML Studio** with A100 GPU (40–80 GB VRAM) — GPU memory bypasses the CPU RAM issue
  - Cost: ~$4–8/hour
  - Conversion: ~20–30 minutes
- **Runpod or Lambda Labs** — rent GPU instances by the hour
  - A100 with 80 GB VRAM recommended
  - Cost: ~$1.50–3/hour

**Option 3: Use Smaller Alternative**
- **DeepSeek-R1-Distill-Qwen-14B** (14B) — already converted and in KnownModels
  - Exceptional reasoning ability (better than Phi-4)
  - Only 14B, RAM requirement: ~12–16 GB
  - Performance: comparable to the 70B on most tasks

#### Recommended Alternatives

- **DeepSeek-R1-Distill-Qwen-14B** (14B) — ✅ already converted, incredible reasoning
- **Qwen2.5-32B-Instruct** (32B) — native ONNX, excellent quality, needs 24–32 GB RAM
- **Phi-4** (14B) — native ONNX, strong reasoning, production-ready

---

### Llama-3.3-70B-Instruct — ✅ RESOLVED

**Model:** meta-llama/Llama-3.3-70B-Instruct  
**Parameters:** 70B  
**HuggingFace:** https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct  
**Converted ONNX:** https://huggingface.co/elbruno/Llama-3.3-70B-Instruct-onnx (39.3 GB, INT4)

#### Resolution

Converted to INT4 ONNX using **CUDA execution provider**, bypassing the CPU RAM limitation. CPU conversion OOM'd at ~440GB; CUDA succeeded.

#### Historical Details (for reference)

Confirmed details from initial conversion attempt (2026-03-18):
- ✅ License accepted — model downloads successfully (no more 403)
- ✅ All 80 decoder layers load correctly — Llama architecture fully supported by builder v0.12.1
- ❌ INT4 quantization phase exhausts ~440GB RAM → OS kill (CPU only)
- ✅ CUDA conversion succeeded — GPU memory bypasses CPU RAM bottleneck

---

## Gated / License Models

### Command-R (35B)

**Model:** CohereForAI/c4ai-command-r-v01  
**Parameters:** 35B  
**HuggingFace:** https://huggingface.co/CohereForAI/c4ai-command-r-v01

#### Why It's Blocked

This model requires accepting Cohere's specific license agreement on HuggingFace before access is granted. The license page may have:
- Changed URL/location
- Been updated with new terms
- Become unavailable for certain regions

Without explicit license acceptance from your HuggingFace account, the model cannot be downloaded.

#### What You Can Do

**Option 1: Accept License and Try Again**
1. Visit: https://huggingface.co/CohereForAI/c4ai-command-r-v01
2. Log in with your HuggingFace account
3. Accept the Cohere license terms in the UI
4. Run conversion script:
   ```bash
   python scripts/convert_to_onnx.py \
       --model-id CohereForAI/c4ai-command-r-v01 \
       --output-dir ./models/command-r-35b
   ```

**Option 2: Check Cohere's Current Offerings**
- Visit: https://huggingface.co/CohereForAI
- Browse available models and their license status
- Command-R may have been superseded or relicensed

**Option 3: Use Alternative Cohere Model**
- **CohereForAI/c4ai-command-r-plus** (40B) — check if this has better license status
  - More recent, may have clearer licensing
  - Similar performance and use cases

#### Recommended Alternatives

- **Qwen2.5-32B-Instruct** (32B) — native ONNX, excellent instruction-following
- **Phi-4** (14B) — native ONNX, strongest reasoning for its size
- **Llama-3.1-8B-Instruct** (8B) — native ONNX, balanced performance

---

## License Restrictions

### Codestral 22B v0.1

**Model:** mistralai/Codestral-22B-v0.1
**HuggingFace:** https://huggingface.co/mistralai/Codestral-22B-v0.1
**License:** MNPL-0.1 (Mistral AI Non-Production License)
**Status:** ⛔ Blocked — license incompatible with production use

#### Why It's Blocked

Codestral 22B is distributed under Mistral's **Non-Production License (MNPL-0.1)**, which prohibits:
- Production deployment (serving outputs to end users)
- Commercial activity of any kind (including free services in a business context)
- Distribution in commercial software, cloud services, or hosted platforms

This makes it incompatible with a NuGet library where users would deploy applications using the model. Including it in `KnownModels` would mislead users into thinking it's freely usable.

#### Workaround

Users who accept the MNPL license for research purposes can still use the model manually:

```csharp
var codestral = new ModelDefinition
{
    Id = "codestral-22b-v0.1",
    DisplayName = "Codestral-22B-v0.1",
    HuggingFaceRepoId = "mistralai/Codestral-22B-v0.1",
    RequiredFiles = ["*"],
    ModelType = OnnxModelType.GenAI,
    ChatTemplate = ChatTemplateFormat.Mistral,
    Tier = ModelTier.Large,
    HasNativeOnnx = false
};
```

**Alternatives:** Use `Qwen2.5-Coder-7B-Instruct` (Apache 2.0, code-specialized, 7B) or `Devstral-Small-2` via llama.cpp.

---

## ONNX Conversion Not Available

### Devstral Small 2 (24B)

**Model:** mistralai/Devstral-Small-2-24B-Instruct-2512
**HuggingFace:** https://huggingface.co/mistralai/Devstral-Small-2-24B-Instruct-2512
**License:** Apache 2.0 ✅
**Status:** ⛔ Blocked — no ONNX conversion path

#### Why It's Blocked

Devstral Small 2 is an excellent code-focused model with an open license, but ONNX conversion is not currently feasible:

| Blocker | Details |
|---------|---------|
| **No ONNX exports exist** | No community or official ONNX versions on HuggingFace |
| **Custom architecture** | Uses Mistral's "Tekken" tokenizer (~131k vocab), FP8 quantization, and custom attention mechanisms |
| **Multimodal components** | Vision+text hybrid architecture adds ONNX conversion complexity |
| **onnxruntime-genai builder** | Not tested with Devstral architecture; likely needs explicit support |

#### What Would Unblock It

1. `onnxruntime-genai` adding explicit Devstral/Mistral-v7 architecture support in the model builder
2. Community ONNX export appearing on HuggingFace
3. Mistral publishing an official ONNX variant

#### Alternative

Use Devstral Small 2 via **llama.cpp** (GGUF format) or **vLLM** (safetensors) for local code development. These are well-supported deployment paths.

For ONNX-based local code assistance, use **Qwen2.5-Coder-7B-Instruct** instead — same Qwen2.5 architecture already supported by the library.

---

## Next-Gen Models (Not in KnownModels Yet)

These models are in the `team.md` roadmap but haven't been added to the library yet. They have architecture or tooling challenges.

### Llama-4 Series

#### Llama-4-Scout (17B MoE)

**Model:** meta-llama/Llama-4-Scout-17B-16E-Instruct  
**Parameters:** ~17B (16-expert MoE)  
**HuggingFace:** https://huggingface.co/meta-llama/Llama-4-Scout-17B-16E-Instruct  
**Status:** 🔄 In Progress (MoE blocker, same as Mixtral-8x7B)

**Why Blocked:**
- Llama-4-Scout uses a 16-expert MoE architecture (more complex than Mixtral-8x7B)
- ONNX Runtime GenAI builder v0.12.1 doesn't support MoE
- Effective parameter count is ~4–5B active per token (fast, but builder can't express it)

**Workaround:**
- Wait for MoE support in ONNX Runtime GenAI (likely in 2025)
- Use Llama-3.1-8B-Instruct instead (dense, native ONNX)

---

#### Llama-4-Maverick (17B + 128-expert MoE)

**Model:** meta-llama/Llama-4-Maverick-17B-128E-Instruct  
**Parameters:** ~17B (128-expert MoE)  
**HuggingFace:** https://huggingface.co/meta-llama/Llama-4-Maverick-17B-128E-Instruct  
**Status:** 🔄 Experimental (heavy MoE blocker, 64+ GB RAM)

**Why Blocked:**
- 128-expert MoE — even more complex than Llama-4-Scout
- Requires massive memory during conversion (~64+ GB)
- MoE support not available in builder

**Assessment:**
- Unlikely to be viable for local inference without significant builder improvements
- Extremely compute-intensive even for inference

**Workaround:**
- Use Qwen2.5-32B or Phi-4 instead (simpler architectures, similar performance)

---

### Qwen3 Series

#### Qwen3-8B

**Model:** Qwen/Qwen3-8B  
**Parameters:** 8B  
**HuggingFace:** https://huggingface.co/Qwen/Qwen3-8B  
**Status:** 🔄 Conversion Pending (architecture compatibility)

**Why Pending:**
- Qwen3 is brand-new; ONNX builder support may not be fully optimized
- May require `--trust-remote-code` due to custom modeling code
- Performance on ONNX Runtime GenAI not yet validated

**Likelihood:** ✅ High — Qwen2.5 works well, Qwen3 should be similar

**Timeline:**
- Once converted, should work with KnownModels similar to Qwen2.5-7B

---

#### Qwen3-32B

**Model:** Qwen/Qwen3-32B  
**Parameters:** 32B  
**HuggingFace:** https://huggingface.co/Qwen/Qwen3-32B  
**Status:** 🔄 Conversion Pending (RAM: ~30 GB for INT4)

**Why Pending:**
- Same architecture considerations as Qwen3-8B, plus RAM overhead for conversion
- Conversion needs ~100+ GB disk, ~32 GB RAM

**Likelihood:** ✅ High — if Qwen3-8B works, this should too

**Timeline:**
- Once Qwen3-8B is validated, proceed with Qwen3-32B

**Recommended Alternative (if blocked):**
- **Qwen2.5-32B-Instruct** (32B) — native ONNX, proven, equivalent performance

---

### Gemma-3

#### Gemma-3-12B-IT

**Model:** google/gemma-3-12b-it  
**Parameters:** 12B  
**HuggingFace:** https://huggingface.co/google/gemma-3-12b-it  
**Status:** 🔄 Conversion Pending (new architecture version)

**Why Pending:**
- Gemma-3 is a new version with potential architecture changes vs. Gemma-2
- May require updated builder or tooling
- Conversion not yet tested in this project

**Likelihood:** ✅ Medium-High — Gemma-2-9B works, Gemma-3 should too

**Workaround:**
- **Gemma-2-9B-IT** (9B) — currently being converted, mature architecture
- **Phi-4** (14B) — native ONNX, better reasoning

---

### DeepSeek-V3

#### DeepSeek-V3 (671B MoE)

**Model:** deepseek-ai/DeepSeek-V3  
**Parameters:** 671B (very large MoE, 37B active per token)  
**HuggingFace:** https://huggingface.co/deepseek-ai/DeepSeek-V3  
**Status:** 🔴 Not Viable (multiple blockers)

**Why Not Viable:**

| Blocker | Reason |
|---------|--------|
| MoE Architecture | ONNX Runtime GenAI doesn't support MoE; can't express routing |
| RAM (Conversion) | 671B model needs ~2700 GB RAM for INT4 quantization |
| RAM (Inference) | At least 256+ GB VRAM (multiple H100s) for practical use |
| Disk Space | ~500 GB final size (even INT4 quantized) |

**Assessment:**
- **Not intended for local inference on consumer/enterprise hardware**
- Designed for cloud APIs and data centers only
- Even with future MoE support, memory constraints remain insurmountable

**Recommendation:**
- Use smaller alternatives:
  - **Qwen2.5-32B** (32B, native ONNX)
  - **Phi-4** (14B, native ONNX)
  - **DeepSeek-R1-Distill-Qwen-14B** (14B, native ONNX, exceptional reasoning)

---

### Inkling

#### Inkling (975B MoE, Multimodal)

**Model:** thinkingmachines/Inkling  
**Parameters:** 975B total, 41B active (sparse MoE — 6 of 256 experts routed + 2 shared per token)  
**Architecture:** 66-layer decoder-only transformer, hybrid local/global attention, **natively multimodal** (text + image + audio in → text out) via a hierarchical patch vision encoder and discrete audio token encoder  
**Numerics:** BF16 and NVFP4 only  
**HuggingFace:** https://huggingface.co/thinkingmachines/Inkling  
**Status:** 🔴 Not Viable (multiple blockers)

**Why Not Viable:**

| Blocker | Reason |
|---------|--------|
| MoE Architecture | ONNX Runtime GenAI doesn't support MoE; can't express 256-expert routing (same class of blocker as Mixtral / DeepSeek-V3) |
| Multimodal I/O | Vision (patch encoder) + audio (discrete token) inputs have no path through the text-only `optimum` / GenAI text-generation-with-past pipeline |
| RAM (Conversion) | 975B params ≈ ~2 TB in BF16; INT4 quantization needs multiple TB of RAM |
| RAM (Inference) | Even INT4 ≈ ~490 GB+ of weights — requires data-center multi-GPU, not local/consumer hardware |
| Numerics | NVFP4 is an NVIDIA 4-bit format, not ONNX; no export path exists for this custom multimodal MoE architecture |

**Assessment:**
- **Not intended for local inference on consumer/enterprise hardware** — a larger, multimodal sibling of DeepSeek-V3 (671B MoE).
- Designed for cloud/data-center serving. Vendor guidance points to the Tinker platform and third-party inference providers.
- Even with future MoE support in the builder, the size and multimodal constraints remain insurmountable for this library.

**Recommendation:**
- Access Inkling via a hosted API (Tinker cookbook / third-party inference providers) — outside this library's local-ONNX scope.
- For local inference, use smaller alternatives:
  - **Phi-4** (14B, native ONNX)
  - **Qwen2.5-32B** (32B, native ONNX)
  - **DeepSeek-R1-Distill-Qwen-14B** (14B, native ONNX, exceptional reasoning)

---

### Meta Muse Glimmer 30B

#### Muse Glimmer 30B (Dense 30B, Gated-Attention Multimodal)

**Model:** meta-models/Muse-Glimmer-30B
**Parameters:** 30B total — 2B ViT "Perception Encoder" + 28B text decoder
**Config:** https://huggingface.co/meta-models/Muse-Glimmer-30B/resolve/main/config.json (verified 2026-08-12)
**Architecture:** `architectures: ["MuseGlimmerForConditionalGeneration"]`, `model_type: "muse_glimmer"`, text `model_type: "muse_glimmer_text"`, vision `model_type: "muse_glimmer_vision"`
**License:** Apache 2.0 (open, no gating)
**Status:** ⛔ Blocked — architecture not dispatched by the ONNX Runtime GenAI model builder

#### Why It's Blocked

Muse Glimmer splits cleanly into a part the builder's precedent already covers and a part it does not.

**1. The attention *pattern* is SmolLM3-compatible (not the blocker).** The verified `text_config` shows 52 layers alternating `layer_types: ["sliding_attention","sliding_attention","sliding_attention","full_attention"] × 13`, with `layer_rope_theta` set to `500000.0` on sliding layers and `0` on every 4th (full-attention) layer — i.e. NoPE on full attention, exactly the per-layer conditional-RoPE + sliding/full window pattern that `onnxruntime_genai/models/builders/smollm.py`'s `SmolLM3Model.make_attention` already implements via `config.layer_types` and `config.no_rope_layers` (temporarily toggling `attention_attrs["use_rope_in_attn"]` and `window_size` per layer, then restoring). `text_config.head_dim` is a **uniform 128** across all 52 layers — unlike Gemma 4's variable 256/512 split, so this alone would **not** trigger the single-valued-`head_size` failure class that blocked Gemma 4. This part of the model has a real precedent to extend.

**2. Gated attention has a builder precedent, but not one that fits Muse Glimmer's tensor layout (the actual blocker).** `model.safetensors.index.json` (https://huggingface.co/meta-models/Muse-Glimmer-30B/resolve/main/model.safetensors.index.json, verified 2026-08-12) shows each decoder layer's attention block has `self_attn.gate_proj.weight` alongside the usual `q_proj`/`k_proj`/`v_proj`/`o_proj` — a fourth, separate learned projection. This is *not* a novel operation for the builder: `src/python/py/models/builders/qwen.py`'s `Qwen35TextModel._make_full_attention` (`onnxruntime-genai` v0.15.1/`main`) already implements output gating for full-attention layers — it splits a doubled Q projection into `Q` and a `gate` signal per head, runs `GroupQueryAttention`, then multiplies the attention output by `Sigmoid(gate)` before `o_proj`. The gap is that Qwen3.5's gate is folded into the existing Q projection (no separate weight tensor), whereas Muse Glimmer exposes gating as its own `self_attn.gate_proj` tensor. Adapting the existing gated-attention subgraph — reading `gate_proj` directly instead of splitting a doubled Q output, then reusing the same `Sigmoid`/`Mul` pattern — is an implementation gap requiring new `make_attention` wiring and a `genai_config.json` attribute, not the invention of a new operation.

**3. Weights sit behind the same multimodal prefix that blocked Gemma 4.** The same safetensors index shows every decoder tensor under `model.language_model.*` (e.g. `model.language_model.layers.0.self_attn.gate_proj.weight`), identical in shape to the Gemma 4 blocker documented above. A text-only extraction is possible in principle (the vision tower is a separate `vision_config` sub-tree) but has not been attempted or validated here.

**4. Dispatch findings — v0.15.1 (pinned by this repo) and `main` (checked 2026-08-12).** Neither `src/python/py/models/builder.py` on the `v0.15.1` tag nor on `main` contains a case for `"MuseGlimmerForConditionalGeneration"` or a `model_type == "muse_glimmer*"` branch, and no `MuseGlimmer*` class exists anywhere under `src/python/py/models/builders/`. `main` differs from `v0.15.1` only in adding `GraniteMoeHybridForCausalLM` (see the Nemotron section below) — irrelevant to Muse Glimmer's architecture string.

**5. Video and vision are out of scope regardless.** The `-assistant` DFlash drafter, the 2 fps / 96-frame video path, and the 2D-RoPE + pixel-shuffle vision tower are all downstream of the two blockers above; none were evaluated further because the text decoder itself does not dispatch.

#### What Would Unblock It

1. `onnxruntime-genai` adding a `MuseGlimmerForConditionalGeneration` (or `muse_glimmer_text`) builder case with a gated-attention op, following the `SmolLM3Model` per-layer RoPE/window pattern for the SWA/NoPE alternation.
2. A community ONNX export of the text decoder validated against GenAI's external KV-cache contract (not just an `optimum`/transformers.js export — see the Gemma 4 lesson above).
3. Confirmation that `model.language_model.*` weights can be extracted text-only, the same open question Gemma 4 left unresolved.

#### Recommended Alternatives

- **GGUF via llama.cpp** — Muse Glimmer ships day-0 GGUF (Meta-calibrated k-quants) with full DFlash drafter support; see `docs/plans/gguf-sibling-package-proposal.md` for the costed sibling-package proposal.
- **Gemma-2-9B-IT** (9B) — ✅ converted, closest same-family text-only alternative already in ONNX.
- **Phi-4** (14B) — ✅ native ONNX, strong general-purpose reasoning.

---

### NVIDIA Nemotron 3.5 Lightning 30B-A3B

#### Nemotron 3.5 Lightning 30B-A3B (Mamba-2 + MoE + MTP Hybrid)

**Model:** nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16
**Parameters:** 30B total, 3B active per token
**Config:** https://huggingface.co/nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16/resolve/main/config.json (verified 2026-08-12)
**Architecture:** `architectures: ["NemotronHForCausalLM"]`, `model_type: "nemotron_h"`
**License:** **OpenMDW-1.1** (not Apache/MIT — a new license class for this catalog; see below)
**Status:** ⛔ Blocked — architecture not dispatched by the ONNX Runtime GenAI model builder

#### Why It's Blocked

The verified `config.json` confirms this is a fundamentally different architecture from anything the builder currently maps, and from the plain "Nemotron" name already in its dispatch table:

| Feature | Verified config field | Why It Breaks GenAI |
|---------|-----------------------|----------------------|
| **Mamba-2 SSM layers** | `mamba_head_dim: 64`, `mamba_num_heads: 64`, `ssm_state_size: 128`, `conv_kernel: 4`, `use_mamba_kernels: true`, `mamba_ssm_cache_dtype: "float32"` | No Mamba/SSM state-cache handling exists anywhere in the builder. A search of the installed v0.14.1 build and the `main` branch's `src/python/py/models/builders/base.py` plus every model in `builders/__init__.py` (checked 2026-08-12) found **zero** occurrences of `mamba`, `ssm`, or `conv1d`. GenAI's cache manager only knows transformer KV cache, not SSM recurrent state. |
| **Sparse MoE routing** | `n_routed_experts: 128`, `num_experts_per_tok: 6`, `n_shared_experts: 1`, `moe_shared_expert_intermediate_size: 3712`, `moe_shared_expert_overlap: true` | Real QMoE routing exists in the builder (`GPTOSSModel`, via `moe_attrs`/QMoE op in `base.py`), but it is CUDA-only and forces `--precision int4`. More importantly, the closest-*named* MoE-Hybrid precedent doesn't apply — see the Granite MoE Hybrid finding below. |
| **Mixed layer schedule** | `layers_block_type`: an explicit 52-entry array interleaving `"mamba"`, `"moe"`, and `"attention"` (only 6 of 52 layers are plain attention) | `genai_config.json` has no per-layer block-type field; the builder assumes every layer is the same kind of block. |
| **Multi-Token Prediction (MTP)** | `num_nextn_predict_layers: 1`, `mtp_layers_block_type: ["attention", "moe"]` | No MTP head export path exists in the builder; would need to be stripped before any conversion attempt (per the repo's standing rule to document exactly which tensors are dropped). |

**Dispatch findings — v0.15.1 (pinned by this repo) and `main` (checked 2026-08-12).** Both `src/python/py/models/builder.py` versions dispatch **only** `config.architectures[0] == "NemotronForCausalLM"` to `NemotronModel` (a thin `LlamaModel` subclass in `builders/nemotron.py` that just sets `layernorm_attrs["add_offset"] = 1` and a custom MLP — dense, uniform-attention, no Mamba, no MoE). This model's real architecture string, `"NemotronHForCausalLM"`, and its `model_type`, `"nemotron_h"`, do not appear anywhere in either version's dispatch table or class list. The two "Nemotron" names are unrelated architectures that happen to share a vendor prefix — the plan's assumption that "Nemotron" being in the README implies partial precedent for Nemotron 3.5 Lightning **does not hold** and should be treated as disproven.

**The `main`-branch "Granite MoE Hybrid" precedent does not transfer either.** `main` (not yet in a released version; absent from `v0.15.1`) adds `GraniteMoeHybridForCausalLM` → `GraniteMoeHybridModel` (`builders/granite.py`). Inspecting that class shows it overrides `make_layer` to route through `layer.shared_mlp` — Granite Hybrid's **always-on dense MLP** — and does not touch the actual conditionally-routed experts or any Mamba/SSM state; it inherits `make_attention` unmodified from `GraniteModel`/`MistralModel`, i.e. it assumes every layer is standard attention. It is a MoE model in name only, from the builder's point of view. It provides no Mamba-2 or genuine per-token-routing precedent for Nemotron 3.5 Lightning.

#### License: OpenMDW-1.1 — new obligations for this catalog

Verified from https://raw.githubusercontent.com/OpenMDW/OpenMDW/main/1.1/LICENSE.OpenMDW-1.1 (fetched 2026-08-12). OpenMDW-1.1 is permissive (no copyleft, no field-of-use restriction on outputs) but, unlike every other license currently in this catalog (Apache 2.0 / MIT / Llama gated), it imposes an explicit **redistribution notice requirement**:

> "If you distribute any portion of the Model Materials, you shall retain in your distribution (1) a copy of this agreement, and (2) all copyright notices and other notices of origin included in the Model Materials that are applicable to your distribution."

**Practical effect:** if a converted `elbruno/*-onnx` repo for this model is ever published, it must ship a copy of the OpenMDW-1.1 license text plus NVIDIA's original copyright/origin notices alongside the weights — a `LICENSE` + `NOTICE` file pair this repo has not previously needed. No upload should happen before this is scoped, and it does not change the ONNX dispatch blocker above, which is the binding constraint today.

#### What Would Unblock It

1. `onnxruntime-genai` adding a `NemotronHForCausalLM` (`nemotron_h`) builder case with Mamba-2 SSM state-cache support and real per-token MoE routing (not the Granite MoE Hybrid dense-only shortcut).
2. A per-layer `layers_block_type`-aware model type in `genai_config.json`, mirroring the verified `layers_block_type` array above.
3. A documented MTP-head stripping step, since no MTP export path exists.
4. Legal sign-off on the OpenMDW-1.1 notice requirements before any `elbruno/*-onnx` publication.

#### Recommended Alternatives

- **GGUF via llama.cpp/vLLM/SGLang/TensorRT-LLM** — Nemotron 3.5 Lightning ships day-0 on all of these; see `docs/plans/gguf-sibling-package-proposal.md` for the costed sibling-package proposal.
- **Qwen2.5-32B-Instruct** (32B) — ✅ native ONNX, closest dense equivalent in the catalog today.
- **DeepSeek-R1-Distill-Qwen-14B** (14B) — ✅ native ONNX, if long-context Mamba-2 scaling isn't required.

---

### Qwen3.8-Flash-Next

#### Qwen3.8-Flash-Next (125B total / 6B active, Multimodal Hybrid MoE)

**Model:** Qwen/Qwen3.8-Flash-Next (canonical safetensors repo; `unsloth/Qwen3.8-Flash-Next-GGUF` is a GGUF-only repackaging of the same weights)
**Parameters:** 125B total, 6B activated per token, plus 51B dedicated N-gram embedding parameters and a 4B Multi-Token-Prediction (MTP) head
**Config:** https://huggingface.co/Qwen/Qwen3.8-Flash-Next/raw/main/config.json (verified 2026-08-26)
**Architecture:** `architectures: ["Qwen4ExpForConditionalGeneration"]`, `model_type: "qwen4_exp"` (text: `"qwen4_exp_text"`, vision: `"qwen4_exp"` sub-config) — an **experimental preview of the Qwen4 architecture**, not a Qwen3 variant despite the "Qwen3.8" name
**License:** **Qwen Community License 1.0** — permissive but with revenue/MAU-gated commercial notice obligations and a separate "Model as a Service" / "AI Work Assistant" licensing carve-out (verified from the repo's `LICENSE` file, 2026-08-26) — a license class not previously in this catalog (closest precedent: the OpenMDW-1.1 note-requirement class documented for Nemotron 3.5 Lightning above)
**Status:** ⛔ Blocked — architecture not dispatched by the ONNX Runtime GenAI model builder

#### Why It's Blocked

The verified `config.json` shows a genuinely new hybrid architecture, previewing Qwen4 rather than extending Qwen3:

| Feature | Verified config field | Why It Breaks GenAI |
|---------|-----------------------|----------------------|
| **Hybrid linear + sparse attention** | `layer_types`: 48-entry array of `"linear_attention"` / `"full_attention"` in a 3:1 repeating pattern (12 × (3 × linear → 1 × full)); `linear_num_key_heads: 16`, `linear_num_value_heads: 48`, `linear_key_head_dim`/`linear_value_head_dim: 128` (Gated DeltaNet) | Closest existing precedent is `Qwen35TextModel`/`Qwen35MoeTextModel` (dispatched only for `architectures[0] in {"Qwen3_5ForConditionalGeneration", "Qwen3_5MoeForConditionalGeneration"}`), which already implement a linear-attention/full-attention hybrid and an MTP self-speculative head (`enable_mtp` extra option, documented in `builder.py` as "Export the Qwen3.6 MoE MTP self-speculative head"). But `qwen4_exp` is a **different architecture string** with no dispatch entry of its own — the builder doesn't fall back to a "closest" model type. |
| **Qwen Sparse Attention (QSA) indexer** | `indexer_budget: 2048`, `indexer_compress_ratio: 4`, `indexer_head_dim: 128`, `indexer_kv_heads: 1`, `indexer_n_heads: 4` on `full_attention` layers | No `indexer_*` field appears anywhere in `onnxruntime-genai`'s builder source (verified via GitHub code search on `microsoft/onnxruntime-genai`, 2026-08-26: 0 results for `indexer_budget`). QSA's micro-block sparse indexing is a new op family, not a parameterization of existing GQA/MHA attention. |
| **Gated Residual** | `hc_count: 4`, `hc_lowrank: 320` (4-branch gated residual with a 320-dim bottleneck, read/write gates per branch) | 0 results for `hc_lowrank` in the builder source (verified via GitHub code search, 2026-08-26). No precedent for a multi-branch gated residual stream in any dispatched model — this is architecturally distinct from every model currently in `builders/*.py`. |
| **N-gram Embedding** | `ngram_size: 3`, `ngram_vocab_size_base: 20000000`, `heads_per_ngram: 8`, `split_ngram_parts: 128`, `make_ngram_vocab_size_divisible_by: 128` | 0 results for `ngram_vocab_size_base` in the builder source (verified via GitHub code search, 2026-08-26). A 20M-entry n-gram-indexed embedding table has no analog anywhere in the builder — it isn't a bigger version of any existing embedding op, it's a new lookup mechanism keyed on token n-grams rather than single token IDs. |
| **MoE + shared expert** | `num_experts: 512`, `num_experts_per_tok: 10`, `shared_expert_intermediate_size: 640` | MoE routing exists in the builder for CUDA-only paths (`GPTOSSModel`, `Qwen35MoeTextModel`), so this alone would not block dispatch — it's additive to the blockers above, not a blocker on its own. |
| **Multimodal wrapper** | `image_token_id`, `video_token_id`, `vision_config` (a `qwen4_exp` vision sub-config, separate `depth`/`hidden_size`/`patch_size` fields) | Same class of issue documented for Gemma 4 / Muse Glimmer: a text-only extraction may be possible in principle but was not attempted here, since the architecture string itself isn't dispatched regardless. |

#### Verified failure (live builder run, 2026-08-26)

Both `transformers==5.14.1` (the newest available at investigation time) and `onnxruntime-genai==0.15.1` (this repo's pinned version) were installed and run live against the real HuggingFace repo — no local weights were downloaded (a `config_only=true` run only needs `config.json`, which is sufficient to reach the same dispatch/config-loading code path a full conversion would hit first):

```
python -m onnxruntime_genai.models.builder -m Qwen/Qwen3.8-Flash-Next -o ./out -p int4 -e cpu --extra_options config_only=true
```

Failed with:

```
ValueError: The checkpoint you are trying to load has model type `qwen4_exp` but Transformers does not
recognize this architecture. This could be because of an issue with the checkpoint, or because your
version of Transformers is out of date.
```

raised from `transformers/models/auto/configuration_auto.py` (`CONFIG_MAPPING["qwen4_exp"]` → `KeyError`) inside `onnxruntime_genai/models/builder.py`'s `get_hf_details()` → `AutoConfig.from_pretrained()` call. The same error occurs with `trust_remote_code=True` — the repository ships no custom `modeling_*.py`/`configuration_*.py` files, so there is no remote-code fallback. **This means the failure happens at the `transformers` config-loading stage, before `onnxruntime-genai`'s own architecture dispatch table (`builder.py`'s `config.architectures[0] ==` chain) is even reached.** A GitHub code search of `microsoft/onnxruntime-genai` (2026-08-26) confirms zero occurrences of `qwen4_exp`, `qwen4`, `hc_lowrank`, `indexer_budget`, or `ngram_vocab_size_base` anywhere in the repository — this is not a partially-supported architecture, it has no footprint in the builder at all.

#### What Would Unblock It

1. `transformers` adding a `Qwen4Exp`/`qwen4_exp` config + model class (a prerequisite `onnxruntime-genai` itself depends on via `AutoConfig`/`AutoModel`).
2. `onnxruntime-genai` adding a `Qwen4ExpForConditionalGeneration` builder case with: a QSA sparse-attention op (indexer + micro-block routing), a gated-residual op (multi-branch read/write gates), and an n-gram embedding lookup op — three genuinely new op families, not extensions of existing Qwen3.5/GQA/MoE code.
3. A documented MTP-head export path, extending the existing `enable_mtp`/Qwen3.6 self-speculative-head support to this architecture.
4. Legal review of the Qwen Community License 1.0's revenue/MAU disclosure and MaaS-licensing clauses before any `elbruno/*-onnx` publication were ever to become possible.

#### Recommended Alternatives

- **GGUF via llama.cpp/Unsloth** — `unsloth/Qwen3.8-Flash-Next-GGUF` ships day-0 quantized GGUF; see `docs/plans/gguf-sibling-package-proposal.md` for the costed sibling-package proposal, or the community `LLamaSharp` binding for ad-hoc use outside this library.
- **Qwen3-14B-Instruct** (14.77B) — ✅ native ONNX, already in this catalog with the `Qwen3` chat template.
- **Qwen2.5-32B-Instruct** (32B) — ✅ native ONNX, largest dense Qwen model currently supported.

---

## Future Outlook

### Near-Term (2025)

**Likely to be converted:**
- ✅ Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT — **DONE** (converted and uploaded to elbruno HuggingFace repos)
- ✅ Llama-3.2-3B-Instruct — **DONE** (converted and uploaded to elbruno/Llama-3.2-3B-Instruct-onnx)
- ✅ Llama-3.3-70B — **DONE** (converted to INT4 ONNX using CUDA, uploaded to elbruno/Llama-3.3-70B-Instruct-onnx)
- ✅ Qwen3-8B, Qwen3-32B (architecture compatibility expected to be solid)
- ✅ Gemma-3-12B-IT (likely works with current builder)

**Unlikely without builder updates:**
- ❌ Mixtral-8x7B, Llama-4-Scout, Llama-4-Maverick (all MoE — requires builder update)
- ❌ StableLM-2-1.6B-Chat (unsupported architecture — requires builder update)
- ❌ DeepSeek-V3 (671B + MoE + impractical for local)
- ❌ Inkling (975B + MoE + multimodal — data-center only)
- ❌ Muse Glimmer 30B (gated GQA + multimodal wrapper — requires new builder attention op)
- ❌ Nemotron 3.5 Lightning 30B-A3B (`nemotron_h` Mamba-2 + MoE + MTP — requires new builder architecture, plus OpenMDW-1.1 notice compliance before any republish)

> **Note:** Gemma 4 family (E2B, E4B, 12B, 26B-A4B, 31B) was previously listed here as blocked (PLE architecture). The blocker monitor is retired because the architecture path is understood, but the family remains **manual conversion only** until public ONNX artifacts are actually validated and published. See the Gemma 4 section above.

> **Note:** Muse Glimmer 30B and Nemotron 3.5 Lightning 30B-A3B are tracked as one upstream issue each (see `docs/plans/muse-glimmer-genai-issue-draft.md` and `docs/plans/nemotron-3.5-lightning-genai-issue-draft.md`). Following the Gemma 4 lesson, **no scheduled/polling GitHub Actions monitor is planned for either model** — terminal-state tracking only, no daily comment spam.

### Mid-Term (2025–2026)

**If ONNX Runtime GenAI adds MoE support:**
- ✅ Mixtral-8x7B-Instruct-v0.1
- ✅ Llama-4-Scout (17B MoE)
- ⚠️ Llama-4-Maverick (128-expert — very heavy, still impractical)
- ⚠️ DeepSeek-V3 (would help, but 671B still too large)
- ⚠️ Inkling (would help, but 975B + multimodal still data-center only)
- ⚠️ Nemotron 3.5 Lightning 30B-A3B (MoE support alone is not enough — still needs Mamba-2 SSM cache support and a `nemotron_h`-aware builder case)

**If ONNX Runtime GenAI adds architecture support:**
- ✅ StableLM-2-1.6B-Chat (custom architecture)
- ✅ Muse Glimmer 30B (gated-attention builder op + multimodal text-only extraction)

### Long-Term Verdict

| Model | Realistic Timeline | Effort Level |
|-------|-------------------|--------------|
| Gemma-4-E2B-IT | ✅ Done | ✅ Manual conversion path only (`onnxruntime-genai` v0.15.1+; no validated public ONNX repo yet) |
| Gemma-4-E4B-IT | ✅ Done | ✅ Manual conversion path only (`onnxruntime-genai` v0.15.1+; no validated public ONNX repo yet) |
| Gemma-4-12B-IT | ✅ Done | ✅ Manual conversion path only (`onnxruntime-genai` v0.15.1+; no validated public ONNX repo yet) |
| Gemma-4-26B-A4B-IT | ✅ Done | ✅ Manual conversion path only (`onnxruntime-genai` v0.15.1+; no validated public ONNX repo yet) |
| Gemma-4-31B-IT | ✅ Done | ✅ Manual conversion path only (`onnxruntime-genai` v0.15.1+; no validated public ONNX repo yet) |
| Qwen3-8B, Qwen3-32B | 2025 | ✅ Low (architecture compatible) |
| Gemma-3-12B-IT | 2025 | ✅ Low (Gemma-2 compatible) |
| Mixtral-8x7B | 2025–2026 | 🔴 High (requires MoE builder support) |
| Llama-4-Scout | 2025–2026 | 🔴 High (requires MoE builder support) |
| Llama-3.3-70B | ✅ Done | ✅ Resolved (CUDA conversion) |
| StableLM-2-1.6B | 2025–2026 | 🔴 High (requires new architecture support) |
| Llama-4-Maverick | 2026+ | 🔴 Very High (complex MoE, rarely practical) |
| DeepSeek-V3 | 2026+ | 🔴 Impractical (even with MoE, too large) |
| Inkling | Not viable | 🔴 Impractical (975B MoE + multimodal, data-center only) |
| Muse Glimmer 30B | 2026+ | 🔴 High (new gated-attention builder op + multimodal text-only extraction unresolved) |
| Nemotron 3.5 Lightning 30B-A3B | 2026+ | 🔴 High (new `nemotron_h` builder architecture: Mamba-2 SSM cache + real MoE routing + MTP, plus OpenMDW-1.1 notice compliance) |

---

## Workaround Strategies

### For Architecture Blockers (StableLM, MoE models)

1. **Use proven alternatives** — Mistral-7B, Phi-4, Qwen2.5, Llama-3.1-8B all work perfectly
2. **Monitor builder releases** — Follow https://github.com/microsoft/onnxruntime-genai/releases
3. **Contribute to builder** — If you have ONNX expertise, help add MoE or StableLM support

### For Memory Blockers (70B models)

1. **Cloud GPU rental:**
   - Azure: A100 80GB, ~$4–8/hour
   - Runpod: A100, ~$1.50–3/hour
   - Google Colab: Free T4, but too small (16GB VRAM)

2. **Rent/borrow high-RAM machine:**
   - Check if your organization has 512GB+ systems
   - Data centers often have spare capacity

3. **Use smaller variant:**
   - DeepSeek-R1-Distill-Llama-70B → use DeepSeek-R1-Distill-Qwen-14B (already converted)
   - Llama-3.3-70B → use Llama-3.1-8B (native ONNX)

### For Gated Model Issues (Command-R)

1. **Accept license on HuggingFace UI**
2. **Verify account permissions** (use HF CLI: `huggingface-cli login`)
3. **Use alternative** — Qwen2.5-32B has no gating, better performance

---

## Reporting New Models or Blockers

If you encounter:
- A new model you'd like to convert
- A blocker not listed here
- A successful workaround for a blocked model

Please open an issue: https://github.com/ElBruno/ElBruno.LocalLLMs/issues

Include:
- Model ID and HuggingFace link
- Error message (if any)
- Your system specs (RAM, GPU, OS)

---

## See Also

- 📚 [Supported Models](supported-models.md) — complete list of working models
- 🔧 [ONNX Conversion Guide](onnx-conversion.md) — how to convert models yourself
- 📝 [Contributing](CONTRIBUTING.md) — add a new model to the library
- 🐍 [Conversion Scripts](../scripts/README.md) — detailed script reference
- 📋 [Muse Glimmer 30B issue draft](plans/muse-glimmer-genai-issue-draft.md) — upstream `onnxruntime-genai` feature request (draft)
- 📋 [Nemotron 3.5 Lightning 30B-A3B issue draft](plans/nemotron-3.5-lightning-genai-issue-draft.md) — upstream `onnxruntime-genai` feature request (draft)
- 📦 [GGUF sibling package proposal](plans/gguf-sibling-package-proposal.md) — evaluated fallback for models that ship GGUF but not ONNX
