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

### 2025-07-15 — Large Tier Batch Conversion

**Converted 2 of 6 Large tier models successfully.**

- **Qwen2.5-14B-Instruct and Qwen2.5-32B-Instruct both converted flawlessly.** Qwen2.5 family has a perfect 6/6 track record (0.5B through 32B). INT4 sizes: 14B → 11.3 GB, 32B → 22.1 GB.
- **Command-R (CohereForAI/c4ai-command-r-v01) is GATED** — 403 Forbidden. Cohere requires license acceptance at https://huggingface.co/CohereLabs/c4ai-command-r-v01. This was not previously known.
- **Mixtral-8x7B-Instruct-v0.1 (MoE) is NOT SUPPORTED** — `NotImplementedError` from builder v0.12.1. The Mixture-of-Experts architecture is fundamentally not recognized, not a size issue.
- **DeepSeek-R1-Distill-Llama-70B hit MemoryError** — Downloaded and read all 80 decoder layers successfully, but the INT4 quantization step (`matmul_nbits_quantizer.py`) ran out of memory trying to build the quantized ONNX graph. This happened even with 450 GB RAM available. The bottleneck is the ONNX protobuf serialization holding the entire graph + quantized weights in memory simultaneously.
- **Llama-3.3-70B-Instruct is GATED** — 403, separate license from Llama 3.1. Even if access is granted, it would likely hit the same MemoryError as DeepSeek-70B.
- **70B models appear to be beyond the practical limit** for `onnxruntime_genai` builder INT4 CPU conversion on this machine. Alternative approaches: `optimum` library, `llama.cpp` GGUF conversion, or a machine with more RAM (likely needs 500+ GB for the quantization step).
- Conversion times: 14B ~10 min total, 32B ~25 min total. The 70B download alone took ~18 min (130+ GB of safetensors).
