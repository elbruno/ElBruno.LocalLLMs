# Blocked Models Reference

This document details models that cannot be converted to ONNX yet, along with the specific blockers, workarounds, and when they might become available.

---

## ⚠️ Llama Gated Model Status

Meta's Llama models use **per-model license gates** on HuggingFace. Having access to one Llama model does **not** grant access to others:

- **Llama-3.2-3B-Instruct** — Access request is currently **"awaiting review"**. Llama 3.2 has a separate license from Llama 3.1, so existing Llama 3.1 approval does not carry over.
- **Llama-3.3-70B-Instruct** — Requires a **separate access request** at https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct. This is a different license agreement from both Llama 3.1 and 3.2.

Until access is granted, these models cannot be downloaded or converted. Use `Llama-3.1-8B-Instruct` (already converted, native ONNX) as an alternative.

---

## Quick Summary

| Model | Params | Blocker | Status | Next Step |
|-------|--------|---------|--------|-----------|
| **StableLM-2-1.6B-Chat** | 1.6B | Unsupported architecture | ⛔ Blocked | Wait for builder support or use standard ONNX |
| **Mixtral-8x7B-Instruct-v0.1** | 46.7B (MoE) | MoE routing not supported | ⛔ Blocked | Wait for builder MoE support or use Mistral-7B |
| **DeepSeek-R1-Distill-Llama-70B** | 70B | RAM: ~450GB needed for INT4 | ⛔ Blocked | Use 512GB+ machine, cloud GPU, or smaller DeepSeek-R1-Distill-Qwen-14B |
| **Command-R (35B)** | 35B | Gated model / license issue | ⛔ Blocked | Verify HuggingFace license or use CohereForAI/c4ai-command-r-plus |
| **Llama-3.3-70B-Instruct** | 70B | RAM: ~450GB needed for INT4 | ⛔ Blocked | Use 512GB+ machine, cloud GPU, or smaller alternative |

---

## Architecture Limitations (No Current Builder Support)

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

### Llama-3.3-70B-Instruct

**Model:** meta-llama/Llama-3.3-70B-Instruct  
**Parameters:** 70B  
**HuggingFace:** https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct

#### Why It's Blocked

**Same RAM constraint as DeepSeek-R1-Distill-Llama-70B:** A 70B model requires ~450 GB RAM for INT4 quantization.

Additionally:
- Llama-3.3 is very new; some tooling may not be fully optimized
- License acceptance was recently added; verify access on HuggingFace
- Group Query Attention (GQA) in Llama-3.3 requires proper builder support (available in v0.12.1+)

#### What You Can Do

Follow the same steps as DeepSeek-R1-Distill-Llama-70B:

1. **512GB+ RAM machine** (conversion: 2–4 hours)
2. **Cloud GPU instance** (A100 with 80 GB VRAM; cost ~$2–4/hour; conversion: 20–30 min)
3. **Use smaller Llama alternative:**

#### Recommended Alternatives

- **Llama-3.1-8B-Instruct** (8B) — ✅ native ONNX, excellent, only 8–12 GB RAM
- **Llama-3.2-3B-Instruct** (3B) — being converted now, needs only 6–8 GB RAM
- **Qwen2.5-32B-Instruct** (32B) — native ONNX, better instruction-following than Llama-3.1-8B

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

## Future Outlook

### Near-Term (2025)

**Likely to be converted:**
- ✅ Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT — **DONE** (converted and uploaded to elbruno HuggingFace repos)
- 🔄 Llama-3.2-3B, Llama-3.3-70B — blocked by Meta license gates (see top of this doc)
- ✅ Qwen3-8B, Qwen3-32B (architecture compatibility expected to be solid)
- ✅ Gemma-3-12B-IT (likely works with current builder)

**Unlikely without builder updates:**
- ❌ Mixtral-8x7B, Llama-4-Scout, Llama-4-Maverick (all MoE — requires builder update)
- ❌ StableLM-2-1.6B-Chat (unsupported architecture — requires builder update)
- ❌ DeepSeek-V3 (671B + MoE + impractical for local)

### Mid-Term (2025–2026)

**If ONNX Runtime GenAI adds MoE support:**
- ✅ Mixtral-8x7B-Instruct-v0.1
- ✅ Llama-4-Scout (17B MoE)
- ⚠️ Llama-4-Maverick (128-expert — very heavy, still impractical)
- ⚠️ DeepSeek-V3 (would help, but 671B still too large)

**If ONNX Runtime GenAI adds architecture support:**
- ✅ StableLM-2-1.6B-Chat (custom architecture)

### Long-Term Verdict

| Model | Realistic Timeline | Effort Level |
|-------|-------------------|--------------|
| Qwen3-8B, Qwen3-32B | 2025 | ✅ Low (architecture compatible) |
| Gemma-3-12B-IT | 2025 | ✅ Low (Gemma-2 compatible) |
| Mixtral-8x7B | 2025–2026 | 🔴 High (requires MoE builder support) |
| Llama-4-Scout | 2025–2026 | 🔴 High (requires MoE builder support) |
| Llama-3.3-70B | 2025–2026 | ⚠️ Medium (RAM blocker solvable with cloud) |
| StableLM-2-1.6B | 2025–2026 | 🔴 High (requires new architecture support) |
| Llama-4-Maverick | 2026+ | 🔴 Very High (complex MoE, rarely practical) |
| DeepSeek-V3 | 2026+ | 🔴 Impractical (even with MoE, too large) |

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
