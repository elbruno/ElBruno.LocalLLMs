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
