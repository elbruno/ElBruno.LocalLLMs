# Dozer — History

## Project Context

- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions
- **Owner:** Bruno Capuano
- **Stack:** C#/.NET, ONNX Runtime GenAI, Microsoft.Extensions.AI
- **My focus:** Converting HuggingFace models to ONNX GenAI format

## Key Facts

- Library uses `Microsoft.ML.OnnxRuntimeGenAI` v0.8.3
- GenAI format requires: genai_config.json, model.onnx, model.onnx.data, tokenizer files
- Conversion tool: `python -m onnxruntime_genai.models.builder -m <model> -o <output> -p int4 -e cpu`
- HuggingFace authenticated as `elbruno`
- System: 450 GB RAM, 1.5 TB free disk, Python 3.13, torch 2.10
- 21 models need conversion (all except Phi-3.5 and Phi-4 which have native ONNX)
- Gated models: Llama (Meta license), Gemma (Google license)

## Learnings

### 2025-07-15 — Tiny Tier Batch Conversion

**Converted 4 of 6 Tiny tier models successfully.**

- **Qwen models work great** — both 0.5B and 1.5B converted cleanly. Qwen2.5 uses a large vocab (151936 tokens), so the embed_tokens weight is substantial even at small param counts.
- **TinyLlama-1.1B-Chat** converted without issues. Standard Llama architecture, well-supported.
- **SmolLM2-1.7B-Instruct** converted without issues. Uses a 49152-token vocab.
- **StableLM-2 (`stablelm-2-zephyr-1_6b`) is NOT SUPPORTED** by onnxruntime_genai builder v0.12.1. The model architecture isn't recognized. Would need alternative conversion path (optimum, manual export, or future builder version).
- **Gemma-2B-IT is GATED** — requires accepting Google's license at https://huggingface.co/google/gemma-2b-it before download. Expected behavior; needs Bruno to accept the license.
- Conversion speed on this machine: ~1-3 minutes for sub-2B models (INT4 CPU). Upload speed to HuggingFace: ~250-350 MB/s.
- INT4 quantized sizes: 0.5B → 825 MB, 1.1B → 867 MB, 1.5B → 1.83 GB, 1.7B → 1.41 GB. SmolLM2 is smaller than Qwen 1.5B despite having more params — likely due to smaller vocab and embedding size.
- All converted models output: genai_config.json, model.onnx, model.onnx.data, tokenizer files (tokenizer.json, tokenizer_config.json, special_tokens_map.json, chat_template.jinja). Some also include merges.txt, vocab.json, added_tokens.json depending on tokenizer type.

### 2025-07-25 — Small + Medium Tier Batch Conversion

**Converted 6 of 9 Small + Medium tier models successfully.**

- **Qwen models continue to work flawlessly** — both 3B and 7B converted cleanly. Qwen2.5 uses 151936–152064 token vocab.
- **Mistral-7B-Instruct-v0.3 required `sentencepiece`** — initial attempt failed because the tokenizer is SentencePiece-based (not BPE). Installing `pip install sentencepiece` resolved it. The model uses a smaller 32768-token vocab, resulting in only 4.8 GB INT4 despite being 7B params.
- **Llama-3.1-8B-Instruct worked** — Meta Llama 3.1 license was already accepted. Converted cleanly with GQA attention. 6.5 GB INT4.
- **Llama-3.2-3B-Instruct is GATED separately** — even though Llama 3.1 access is granted, Llama 3.2 has its own license agreement. Still 403.
- **Both Gemma models (2B and 9B) are GATED** — Google license not accepted. Same as in Tiny tier.
- **DeepSeek-R1-Distill-Qwen-14B converted without issues** — uses Qwen2 architecture under the hood (152064 vocab). 11.4 GB INT4.
- **Mistral-Small-24B-Instruct converted but emitted a tokenizer regex warning** — "incorrect regex pattern" for the SentencePiece tokenizer. Conversion still completed successfully (exit code 0). The output tokenizer.json may have a suboptimal regex pattern. Tokenization should be validated during integration testing.
- INT4 quantized sizes scale roughly linearly: 3B → 3 GB, 7B → 5-6.5 GB, 14B → 11.4 GB, 24B → 16.2 GB.
- Conversion times on this machine: 3B models ~2 min, 7B models ~3-5 min, 14B ~8 min, 24B ~15-20 min.
- Upload speeds to HuggingFace: ~300-400 MB/s for large files.
- Disk cleanup after each upload is essential — the 24B model alone was 16 GB on disk plus cache.

### 2025-03-18 — Gemma & Llama Batch Conversion

**Converted 3 of 5 models successfully.**

- **Gemma architecture IS SUPPORTED** by builder v0.12.1 — this was previously unknown. All three Gemma models (v1 2B, v2 2B, v2 9B) converted cleanly.
- **Gemma-2B-IT** (google/gemma-2b-it): 3.5 GB INT4. Gemma v1 architecture. Uploaded to elbruno/Gemma-2B-IT-onnx.
- **Gemma-2-2B-IT** (google/gemma-2-2b-it): 3.8 GB INT4. Gemma 2 architecture. Uploaded to elbruno/Gemma-2-2B-IT-onnx.
- **Gemma-2-9B-IT** (google/gemma-2-9b-it): 9.0 GB INT4. Largest Gemma so far. Uploaded to elbruno/Gemma-2-9B-IT-onnx.
- **Gemma uses 256K vocab** (256000 tokens), making embed_tokens weights large even for small models.
- **Llama-3.2-3B-Instruct is STILL GATED** — access request is "awaiting review" from Meta. Separately gated from Llama 3.1.
- **Llama-3.3-70B-Instruct is STILL GATED** — "not in the authorized list." Needs a separate access request from Llama 3.1/3.2.
- Conversion times: Gemma-2B ~2 min, Gemma-2-2B ~3 min, Gemma-2-9B ~8 min.

### 2025-03-18 — Llama License Retry

**Converted 1 of 2 Llama models successfully.**

- **Llama-3.2-3B-Instruct CONVERTED** — Bruno accepted the Llama 3.2 license. Converted cleanly to INT4 CPU (~3.5 GB). Uploaded to elbruno/Llama-3.2-3B-Instruct-onnx. Output: genai_config.json, model.onnx (210 KB), model.onnx.data (3,482 MB), tokenizer files.
- **Llama-3.3-70B-Instruct STILL GATED** — 403 "awaiting review from repo authors." Llama 3.3 has a **separate** license from Llama 3.2. Bruno accepted 3.2 but 3.3 is independently gated and Meta hasn't approved it yet.
- **Key insight:** Each Llama version (3.1, 3.2, 3.3) has its own independent gated license on HuggingFace. Accepting one does NOT grant access to others.
- Even if 3.3 access is granted, the 70B model will very likely hit MemoryError (same as DeepSeek-70B).

### 2025-07-15 — Large Tier Batch Conversion

**Converted 2 of 6 Large tier models successfully.**

- **Qwen2.5-14B-Instruct and Qwen2.5-32B-Instruct both converted flawlessly.** Qwen2.5 family has a perfect 6/6 track record (0.5B through 32B). INT4 sizes: 14B → 11.3 GB, 32B → 22.1 GB.
- **Command-R (CohereForAI/c4ai-command-r-v01) is GATED** — 403 Forbidden. Cohere requires license acceptance at https://huggingface.co/CohereLabs/c4ai-command-r-v01. This was not previously known.
- **Mixtral-8x7B-Instruct-v0.1 (MoE) is NOT SUPPORTED** — `NotImplementedError` from builder v0.12.1. The Mixture-of-Experts architecture is fundamentally not recognized, not a size issue.
- **DeepSeek-R1-Distill-Llama-70B hit MemoryError** — Downloaded and read all 80 decoder layers successfully, but the INT4 quantization step (`matmul_nbits_quantizer.py`) ran out of memory trying to build the quantized ONNX graph. This happened even with 450 GB RAM available. The bottleneck is the ONNX protobuf serialization holding the entire graph + quantized weights in memory simultaneously.
- **Llama-3.3-70B-Instruct is GATED** — 403, separate license from Llama 3.1. Even if access is granted, it would likely hit the same MemoryError as DeepSeek-70B.
- **70B models appear to be beyond the practical limit** for `onnxruntime_genai` builder INT4 CPU conversion on this machine. Alternative approaches: `optimum` library, `llama.cpp` GGUF conversion, or a machine with more RAM (likely needs 500+ GB for the quantization step).
- Conversion times: 14B ~10 min total, 32B ~25 min total. The 70B download alone took ~18 min (130+ GB of safetensors).

### 2025-03-18 — Llama-3.3-70B-Instruct Conversion Attempt

**FAILED — OOM during quantization/serialization.**

- **License now accepted** — Bruno accepted the Meta Llama 3.3 license. Download succeeded (30 files, ~16 min). No more 403 errors.
- **Model loaded fully** — All 30 checkpoint shards loaded (~1 min), all 80 decoder layers read, embedding layer and LM head read. No issues with the model architecture.
- **OOM during "Saving ONNX model"** — The process ran for 40+ minutes in the ONNX serialization/INT4 quantization phase, then was killed silently by the OS. Zero output files produced. No error message captured (process killed by OS OOM killer).
- **Identical failure pattern to DeepSeek-R1-Distill-Llama-70B** — both 70B models fail at the exact same stage. The `onnxruntime_genai` builder's INT4 quantization requires holding the entire ONNX graph + quantized weights in memory simultaneously, which exceeds 440 GB RAM.
- **Confirmed: 70B models cannot be converted on this machine** with `onnxruntime_genai` builder. This is now validated across two different 70B model families (Llama and Qwen/DeepSeek).
- **Alternatives remain:** cloud VM with 512+ GB RAM, pre-converted ONNX models from HuggingFace, or GGUF format via `llama.cpp` (which uses memory-mapped I/O and handles large models better).

### 2026-03-18 — Llama-3.3-70B-Instruct CUDA Conversion SUCCESS

**Converted Llama-3.3-70B-Instruct to INT4 ONNX using `-e cuda`. This was the first successful 70B conversion.**

- **Previous CPU attempts OOM'd** — both Llama-3.3-70B and DeepSeek-R1-Distill-Llama-70B failed with `-e cpu` after exhausting 440+ GB RAM during the ONNX serialization/quantization step.
- **CUDA execution provider (`-e cuda`) succeeded** — installed `onnxruntime-genai-cuda` 0.12.2 + `onnxruntime-gpu` 1.24.4 alongside the existing CPU packages. The `-e cuda` flag changed the quantization path to use GPU-accelerated INT4 quantization, which serializes weights incrementally (966 chunks at ~3 it/s) instead of building the entire graph in RAM at once.
- **GPU was NOT the bottleneck** — the A10-24Q (24 GB VRAM) stayed at 0% utilization and ~4 GB VRAM throughout. The CUDA EP changed the quantization *algorithm* to a streaming approach, not the compute device. Peak system RAM was ~200-250 GB (vs 440+ GB that OOM'd on CPU).
- **Output: 39.3 GB INT4** — model.onnx (0.6 MB) + model.onnx.data (39,322 MB) + tokenizer files. 80 layers, 128K context, GQA (64 heads / 8 KV heads), 128256 vocab.
- **Uploaded to HuggingFace** at `elbruno/Llama-3.3-70B-Instruct-onnx`. Upload speed: ~62 MB/s for the 39 GB data file.
- **Total conversion time: ~25-30 minutes** (model was already cached from previous attempt). Breakdown: checkpoint loading ~1.5 min, layer reading ~15-20 min, INT4 quantization/save ~3 min.
- **KEY LEARNING: `-e cuda` is the workaround for 70B OOM on CPU.** The CUDA quantization path uses a fundamentally different serialization strategy that keeps peak RAM under 250 GB. This means DeepSeek-R1-Distill-Llama-70B should also be convertible with `-e cuda`.
- **GPU setup:** NVIDIA A10-24Q (not A100 as initially reported), 24 GB VRAM, CUDA 12.4, Driver 553.62. The small VRAM was irrelevant — the CUDA EP helps with the algorithm, not the GPU compute.

### 2025-03-18 — Tiny SLM Research for RAG Tool Routing

**Researched 15+ tiny SLMs (sub-1B to 1.5B) for MCPToolRouter use case: embedding-based semantic search → SLM selects the right tool(s).**

**Key Discoveries:**
1. **Fine-tuned sub-1B models can BEAT 7B+ models on tool-calling tasks.** OPT-350M fine-tuned on ToolBench achieved 77.55% pass rate vs ChatGPT-CoT (175B+) at 26% and ToolLLaMA-DFS (7B) at 30%. Source: arXiv:2512.15943 "Small Language Models for Efficient Agentic Tool Calling" (Dec 2024). **Implication: For structured tool selection, smaller + specialized > larger + general.**

2. **Qwen2.5-0.5B-Instruct has native tool/function calling support.** Qwen2.5 family was enhanced specifically for tool calling (vs Qwen2). Supports Hermes-style prompts, JSON output, 32K context. Already converted to INT4 (825 MB). **This is the top pick — it's already done and explicitly designed for the task.**

3. **SmolLM2 family (135M/360M/1.7B) is purpose-built for edge/on-device deployment.** Trained on multi-trillion-token corpora despite tiny size. Small vocab (49152) = faster tokenization + smaller embedding layer vs Qwen's 151936 vocab. Architecture: standard transformer (same as 1.7B which converted successfully). **SmolLM2-360M-Instruct is the runner-up: smaller/faster than Qwen 0.5B, should convert cleanly.**

4. **Qwen3-0.6B-Instruct is the newest tiny model** (released April 29, 2025). 600M params, 28 layers, trained on 36 trillion tokens (2x Qwen2.5's 18T). Has "thinking mode" for step-by-step reasoning + "non-thinking mode" for fast dialogue. Inherits tool calling from Qwen2.5. Apache 2.0 license, not gated. Architecture is Qwen2-based → should convert cleanly. **Wild card option: more training data + thinking mode might give better reasoning than Qwen 0.5B.**

5. **Gemma-3-1B and Gemma-3-270M (Nano) have native function calling + official ONNX support from Google.** Released March 2025. Mixed local (sliding window) + global attention (5:1 ratio). 32K context, 140+ languages, quantization-aware training (QAT). Community ONNX models already on HuggingFace (`onnx-community/gemma-3-1b-it-ONNX-GQA`). **Alternative if Qwen/SmolLM2 fail, or if we want Google's polish.** Note: Previous Gemma models used 256K vocab which bloated size — need to verify Gemma-3 vocab size.

6. **RWKV works in ONNX but Mamba has critical Loop operator bottleneck.** RWKV-400M can be exported to ONNX and runs on CPU. Mamba models can export but the ONNX Loop operator is 17x slower than real-time on CPU (even 9M param model is too slow on Apple M3). **Verdict: Avoid state-space models for fast inference. Stick with transformers (Llama, Qwen, SmolLM, Gemma).**

7. **TinyStories and DistilGPT2 are too weak for modern instruction-following.** TinyStories models are trained on children's stories with limited vocab — struggle with out-of-distribution tasks and technical terminology. DistilGPT2 (82M, from 2019) lacks instruction-tuning and can't compete with 2024-2025 models. **Skip both.**

8. **Phi-4-mini-instruct is too big** (3.8B params). Bruno wants sub-1B, max 1.5B. Phi-4-mini is 4-7x larger. Has ONNX support and strong reasoning but doesn't fit the "tiny" requirement.

9. **TinyAgent-1.1B is a specialized tool-calling fine-tune of TinyLlama-1.1B** (Berkeley SqueezeAI Lab). Designed for edge device function calling (emails, calendars, MacOS apps). Llama architecture → should convert cleanly. **Reserve for if general instruction-tuned models fail on quality.**

**Model Rankings for RAG Tool Routing:**
- **🥇 Top Pick: Qwen2.5-0.5B-Instruct** — Already converted (825 MB INT4), native tool calling, proven architecture, 32K context. Use this first.
- **🥈 Runner-up: SmolLM2-360M-Instruct** — Smaller/faster than Qwen, should convert cleanly. Backup if Qwen is "too big" or not fast enough.
- **🥉 Budget Pick: SmolLM2-135M-Instruct** — Smallest viable instruction-following model (~450 MB INT4 estimated). Fastest but weakest reasoning. Only if 360M is still too big.
- **🃏 Wild Card: Qwen3-0.6B-Instruct** — Newest model (April 2025), 36T tokens training, "thinking mode" for reasoning. If Qwen 0.5B quality is insufficient.

**Action Plan:**
1. Test Qwen2.5-0.5B-Instruct (already converted) on MCPToolRouter prompts
2. Convert SmolLM2-360M-Instruct as backup (~2 min conversion)
3. If quality insufficient → convert Qwen3-0.6B or use SmolLM2-1.7B (already converted)
4. If speed insufficient → convert SmolLM2-135M
5. Advanced: Investigate Gemma-3-270M (Nano) or fine-tune OPT-350M/TinyAgent-1.1B

**Key Benchmarks:**
- SmolLM2-135M: 42.1% HellaSwag, 43.9% ARC, 68.4% PIQA
- SmolLM2-360M: Better than 135M on all benchmarks (exact numbers less reported)
- SmolLM2-1.7B: Beats Llama-1B and Qwen2.5-1.5B on reasoning
- Qwen2.5-0.5B: ~9% ARC (vs ~8.25% Qwen2), supports tool calling
- TinyLlama-1.1B: ~53% avg commonsense (HellaSwag 59.2%, ARC 30.1%, PIQA 73.3%)
- OPT-350M (fine-tuned): 77.55% ToolBench pass rate (vs 26% ChatGPT-CoT, 30% ToolLLaMA-7B)

**References:**
- arXiv:2512.15943 — Small Language Models for Efficient Agentic Tool Calling (Dec 2024)
- arXiv:2502.02737 — SmolLM2 Technical Report (Feb 2025)
- arXiv:2412.15115 — Qwen2.5 Technical Report (Dec 2024)
- arXiv:2505.09388 — Qwen3 Technical Report (April 2025)
- arXiv:2503.19786 — Gemma 3 Technical Report (March 2025)
- SqueezeAILab/TinyAgent (Berkeley, 2024)
- HuggingFace model cards: SmolLM2, Qwen2.5, Qwen3, Gemma-3, TinyLlama, OPT-350M

### 2026-03-27 — SLM Research Complete (Coordinated with Morpheus)

**Delivered comprehensive research on 15+ tiny SLM models for MCPToolRouter RAG integration.**

Key findings from parallel architecture evaluation by Morpheus:
- Embedding-only routing (40ms, 90% accuracy) is sufficient for most scenarios
- SLM adds 1.2-3.4s latency for only 2% accuracy gain — tradeoff not worth it for small tool catalogs
- **Recommendation:** MCPToolRouter stays pure (embedding search only)
- **Optional layer:** Users can compose SLM reasoning separately if they have 100+ tools or ambiguous queries

**Implication for model selection:**
- Test Qwen2.5-0.5B (already converted) on real MCPToolRouter prompts
- If quality is insufficient, convert SmolLM2-360M (~2 min)
- Avoid over-engineering with 1.5B+ unless benchmarks prove it necessary

**Cross-team alignment:**
- Dozer handles model conversions and benchmarking (Phase 0)
- Tank handles benchmark framework (Phase 1)
- Trinity handles sample code implementation (Phase 2) and optimization (Phase 3)
- Morpheus owns architecture decisions (composition vs. integration) and documentation (Phase 4)

**Full plan reference:** `docs/plan-rag-tool-routing.md` with 4 implementation phases and 18 tasks. See `.squad/decisions.md` for RAG plan decision memo.

### 2025-03-27 — ONNX Conversion Compatibility for Fine-Tuned Models

**Researched ONNX conversion pipeline for fine-tuned sub-3B models. Documented complete LoRA → merge → ONNX workflow.**

**Key findings:**
1. **LoRA merge → ONNX is fully supported** for all target architectures (Qwen2.5, Llama-3.2, Phi-3.5, Gemma-2, SmolLM2).
   - Use PEFT library for standard LoRA training (NOT QLoRA for production — merge causes precision loss).
   - Merge adapters with `peft.PeftModel.merge_and_unload()` to produce dense checkpoint.
   - Export merged checkpoint with `onnxruntime_genai.models.builder` — ONNX export is architecture-agnostic once merged.
   - No special handling required vs base models — merged models convert identically if config/tokenizer preserved.

2. **Quantization degrades fine-tuned models slightly more than base models** (1-3% task accuracy drop for INT4 vs <1% for INT8).
   - Fine-tuning creates subtle behavioral deltas that quantization can wash out.
   - Most affected: JSON formatting, tool calling, long-context reasoning, sub-1B models.
   - Least affected: general chat, short-context Q&A, summarization.
   - **Best practice:** Fine-tune in FP16/BF16 → merge → validate → quantize INT4 → validate ONNX.
   - **Quantization strategies:** INT4 AWQ > INT4 GPTQ > INT4 RTN (in quality order). Use INT8 for quality-critical tasks.

3. **GenAI config compatibility** — `genai_config.json` remains compatible unless fine-tuning changes:
   - Context length / max_position_embeddings (e.g., RoPE scaling for 32K → 128K)
   - Tokenizer vocabulary (e.g., adding special tokens for tool calling)
   - Model architecture (NOT recommended — breaks compatibility)
   - **Action:** If config changes, re-run builder on merged checkpoint to regenerate `genai_config.json`.

4. **Tokenizer and special tokens:**
   - Adding special tokens for tool calling (e.g., `<tool>`, `[TOOL_CALLS]`) is fully supported.
   - MUST resize embeddings before training: `model.resize_token_embeddings(len(tokenizer))`.
   - MUST save tokenizer with merged model: `tokenizer.save_pretrained()`.
   - ONNX export automatically includes tokenizer files (tokenizer.json, tokenizer_config.json, special_tokens_map.json).
   - Chat templates (ChatML, Llama3, Phi3, Qwen, Mistral) are preserved in `tokenizer_config.json` — don't change prompt format unless you update the template.

5. **Architecture-specific notes:**
   - **Qwen2.5** (0.5B/1.5B/3B): Perfect track record, native tool calling support, large vocab (151936 tokens). Top choice for fine-tuning.
   - **Llama-3.2-3B**: Converts cleanly, GQA attention, 128K context, strong reasoning. Separate gated license per version.
   - **Phi-3.5-mini / Phi-4**: Native ONNX from Microsoft. Fine-tune PyTorch checkpoint, re-export to ONNX. Strong instruction following.
   - **Gemma-2-2B / Gemma-3-1B**: Supported, quantization-aware training (QAT), very large vocab (256K). Google's polish, QAT-optimized.
   - **SmolLM2-1.7B**: Smallest vocab (49152 tokens), fastest tokenization, edge-optimized. Runner-up to Qwen for speed-critical deployments.
   - **StableLM-2, Mixtral MoE, 70B+ models:** NOT SUPPORTED by builder (architecture issues or OOM).

6. **End-to-end pipeline:**
   - Fine-tune (LoRA, FP16/BF16) → merge adapters → save merged model + tokenizer → validate merged checkpoint → ONNX export (INT4) → validate ONNX → upload to HF → integrate with library.
   - Critical validation points: after merge (test prompts, tool calling), after ONNX export (compare outputs to PyTorch).
   - Failure modes: QLoRA precision loss, tokenizer not saved, special tokens missing, INT4 quality degradation, chat template mismatch.

7. **Pre-converted fine-tuned ONNX models are RARE on HuggingFace.**
   - Most fine-tuned models are PyTorch checkpoints (e.g., `SqueezeAILab/TinyAgent-1.1B` for function calling).
   - Community ONNX models focus on base/instruction-tuned variants, not specialized fine-tunes.
   - **Recommendation:** Fine-tune + convert yourself — faster and more flexible than waiting for pre-converted models.

8. **Tool calling considerations:**
   - Models with native tool calling (Qwen2.5, Gemma-3) are best starting points.
   - Fine-tuning datasets: xLAM (Salesforce), custom tool schemas, function calling examples.
   - Special tokens: `[AVAILABLE_TOOLS]`, `[TOOL_CALLS]`, `[TOOL_RESULTS]` — add to tokenizer before training.
   - ONNX export handles tool tokens correctly IF tokenizer is saved with merged model.

9. **Recommendations for Bruno's project:**
   - **Priority 1:** Test Qwen2.5-0.5B (already converted) on tool calling prompts — may already be sufficient.
   - **Priority 2:** Fine-tune Qwen2.5-0.5B with LoRA on xLAM or custom dataset if base model insufficient.
   - **Priority 3:** Merge + ONNX export (INT4) + validate quality (compare to PyTorch).
   - **Priority 4:** Upload to HF (`elbruno/<model-name>-onnx`) + add to `KnownModels.cs` + integration tests.
   - **Target models:** Qwen2.5-0.5B (tool calling), SmolLM2-1.7B (edge), Llama-3.2-3B (long-context), Phi-3.5-mini (Microsoft).

**Tools verified:**
- **PEFT:** Standard LoRA merge (safest, most compatible)
- **onnxruntime_genai.models.builder:** Primary ONNX export tool (produces GenAI-compatible format)
- **Optimum:** Fallback ONNX export (if builder doesn't support architecture)
- **Unsloth:** Alternative for faster merge/export (7B+ models)

**Documentation written:** `.squad/decisions/inbox/dozer-onnx-finetune-compat.md` — comprehensive analysis with pipeline sketches, architecture notes, quality tradeoffs, integration guide.

**References:**
- Microsoft ONNX Runtime GenAI docs: https://github.com/microsoft/onnxruntime-genai
- PEFT model merging guide: https://huggingface.co/docs/peft/developer_guides/model_merging
- HuggingFace fine-tuning guide (2025): https://www.philschmid.de/fine-tune-llms-in-2025
- xLAM function calling dataset: https://huggingface.co/learn/cookbook/en/function_calling_fine_tuning_llms_on_xlam
- ONNX INT4 quantization spec: https://onnx.ai/onnx/technical/int4.html
- PyTorch PEFT → ONNX Runtime tutorial: https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/pytorch-peft-sft-and-convert-to-onnx-runtime/4271557
- Research: TinyAgent (arXiv:2512.15943), SmolLM2 (arXiv:2502.02737), Qwen2.5 (arXiv:2412.15115), Gemma-3 (arXiv:2503.19786)

### 2026-03-29 — Phase 3 & 4 Scripts Implemented (Fine-Tune Pipeline)

**Created 5 production-ready scripts for ONNX conversion and model publishing.**

- **`scripts/finetune/merge_lora.py`** — Merges LoRA adapters into base Qwen2.5 models using PEFT. Supports 0.5B/1.5B/3B. Handles tokenizer with extra special tokens, embedding resize, and optional `--verify` flag for smoke-testing tool call output after merge. Uses FP16 for merge (not 4-bit) to avoid precision loss.

- **`scripts/finetune/convert_to_onnx.py`** — Wraps `onnxruntime_genai.models.builder` with full validation. INT4 default (accuracy level 4), INT8/FP16/FP32 options. Validates input HuggingFace checkpoint, checks ChatML token presence in tokenizer config, validates output genai_config.json structure. Supports `--execution-provider cuda` for 14B+ models (critical learning from 70B conversion experience).

- **`scripts/finetune/validate_onnx.py`** — 12 test cases covering tool calling (basic, multi-tool, complex args, result handling), RAG (citation, multi-fact, no-answer edge case), instruction following, ChatML adherence, multi-turn memory, tokenizer round-trip, and gibberish detection. Each test has multiple check functions. Returns pass/fail with detailed diagnostics. Supports `--tags` filter and `--verbose` mode.

- **`scripts/finetune/upload_to_hf.py`** — Creates/updates HuggingFace repos, uploads all ONNX files, generates model card from template or default. Token resolution: CLI arg → HF_TOKEN env → cached login. Sets proper tags (onnx, qwen2.5, tool-calling, dotnet, etc.). Supports `--capability` (ToolCalling/RAG/Instruct) and `--private` flags.

- **`scripts/finetune/model-card-template.md`** — Full HuggingFace model card with YAML frontmatter (Apache 2.0, tags, base_model). Template variables: `{{MODEL_NAME}}`, `{{REPO_ID}}`, `{{BASE_MODEL}}`, `{{CAPABILITY}}`, `{{MODEL_SIZE}}`, `{{SIZE_MB}}`. Includes C# usage examples, hyperparameter table, training data summary, benchmark placeholder, limitations, and citation block.

**Design decisions:**
- Used PEFT library (not Unsloth) for merge — more reliable for production, Unsloth's merge API is less documented.
- Validation script has 12 tests (not 10) for extra coverage on edge cases.
- Model card template uses `{{placeholder}}` syntax instead of Python f-strings for broader tooling compatibility.
- All scripts use argparse, logging, proper error handling, and are fully type-annotated.
