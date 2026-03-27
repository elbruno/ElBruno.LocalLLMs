# Mouse — History

## Project Context

- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime GenAI
- **Owner:** Bruno Capuano
- **Stack:** C#, .NET 8/10, ONNX Runtime, Microsoft.Extensions.AI (IChatClient)
- **My focus:** Fine-tuning small models for use with this library — tool calling, RAG, instruction following

## Target Models for Fine-Tuning

Tiny tier (sub-2B): Qwen2.5-0.5B/1.5B, TinyLlama-1.1B, SmolLM2-1.7B, Gemma-2B, StableLM-2-1.6B
Small tier (2-4B): Phi-3.5-mini (3.8B), Qwen2.5-3B, Llama-3.2-3B, Gemma-2-2B

## Key Library Features (fine-tuning targets)

- Chat completion with template formatting (ChatML, Qwen, Llama3, Phi3, DeepSeek, Mistral, Gemma)
- Tool calling (function definitions → JSON tool calls → function results)
- RAG pipeline (document chunking, embedding, retrieval-augmented generation)
- All inference through ONNX Runtime GenAI — fine-tuned models must convert cleanly

## Learnings

### 2026-03-17: Comprehensive Fine-Tuning Feasibility Analysis

**Key Findings:**

1. **Best Models for Fine-Tuning (Apache 2.0/MIT = No Legal Drama):**
   - **Top 3:** Qwen2.5-1.5B (1.5B, Apache 2.0), Phi-3.5-mini (3.8B, MIT), Qwen2.5-3B (3B, Apache 2.0)
   - **Avoid:** StableLM-2-1.6B (non-commercial only), Llama 3.2/Gemma (gated licenses add friction)
   - **Ecosystem:** Qwen has 544+ fine-tunes on HuggingFace, TinyLlama 520+, Phi-3.5 259+ (strong community)

2. **Fine-Tuning Techniques for Sub-3B:**
   - **QLoRA is the sweet spot:** 6-8GB VRAM (fits RTX 3060+), 1.5-2x faster than LoRA, 90-95% quality
   - **Recommended hyperparams:** r=16, α=32 for 1-2B models; r=32, α=32-64 for 3B models
   - **Training time:** 20-45 min on RTX 4090 for 1K examples (Qwen2.5-1.5B QLoRA)
   - **Cost:** $0 on owned GPU, or $6-8 on cloud A100 for production-quality (5K examples, 6-8 hours)

3. **Frameworks:**
   - **Unsloth** = best for single consumer GPU (2-5x faster, 80% less VRAM than HF Trainer)
   - **LLaMA-Factory** = best for beginners (Web UI, no-code, 100+ models supported)
   - **Axolotl** = best for production/multi-GPU (YAML-driven, scalable)

4. **Training Data Strategy:**
   - **Tool Calling:** Glaive Function Calling v2 (113K examples, ShareGPT format) is gold standard
   - **Dataset sizes:** 1K examples = proof-of-concept, 5K = production, 10K+ = competitive
   - **Multi-task mix:** 50% tool calling + 30% RAG + 20% general instruction (prevents catastrophic forgetting)
   - **Open datasets:** Glaive v2, Hermes (NousResearch), xLAM (Salesforce), SQuAD 2.0 (RAG), Dolly-15K (general)

5. **Evaluation:**
   - **Berkeley Function Calling Leaderboard (BFCL v4)** = gold standard for tool calling accuracy
   - **Expected results:** Phi-3.5-mini fine-tuned = 70-80% BFCL, Qwen2.5-1.5B = 55-65%, Qwen2.5-0.5B = 40-50%
   - **Custom eval sets:** 100 tool calling + 100 RAG + 50 general = 250 examples (hold-out from training)

6. **ONNX Conversion Compatibility:**
   - **Microsoft Olive** (official tool) supports LoRA merging + ONNX export for all target models
   - **Risk:** QLoRA (4-bit) merging can introduce precision loss → prefer LoRA (FP16) for production
   - **Workflow:** Fine-tune → Merge adapters → Export ONNX (FP32) → Quantize (INT8/FP16) → Generate genai_config.json
   - **Community-proven:** Qwen, Phi, Llama have excellent ONNX conversion success rates

7. **Catastrophic Forgetting Mitigation (Critical for Tiny Models):**
   - **Always use LoRA/QLoRA** (never full fine-tuning) → preserves base model knowledge
   - **Mix 20% general data** (Dolly-15K or Alpaca) → prevents task-specific overfitting
   - **Conservative hyperparams:** r=8-16 (lower rank), lr=1e-5 to 5e-5, 3-5 epochs max
   - **Monitor general benchmarks** (MMLU, HellaSwag) during training → stop if drop >5%

8. **Pre-Fine-Tuned Models (Use Instead of Training Your Own):**
   - **meetkai/functionary-small-v3.2-3B** (Phi-3 based) = best pre-trained tool calling model (50K+ downloads)
   - **Trelis/Qwen2.5-3B-Instruct-function-calling-v1.0** = community fine-tune on Glaive v2 (15K+ downloads)
   - **Phi-3.5-mini-instruct (base)** = native tool calling support, no fine-tuning needed for basic use

9. **Practical Recommendations:**
   - **Weekend + RTX 4090:** Fine-tune Qwen2.5-1.5B with QLoRA (r=16, α=32) on 1K examples → 30 min, $0 cost
   - **$50 cloud budget:** Fine-tune Phi-3.5-mini with LoRA on 5K examples → 8 hours on A100, production-quality
   - **Smallest tool-calling model:** Qwen2.5-0.5B fine-tuned → 500MB ONNX (INT8), runs on Raspberry Pi, 40-50% accuracy

10. **Risks:**
    - **Catastrophic forgetting:** HIGH risk for sub-3B (mitigate with LoRA + mixed datasets)
    - **ONNX conversion:** MEDIUM risk (test early, use Olive, validate outputs)
    - **License issues:** MEDIUM risk (use Apache 2.0/MIT, avoid scraped data)
    - **Sub-1B limitations:** Models <1B struggle with complex tool calling (40-50% max accuracy)
    - **Maintenance burden:** MEDIUM (base models update yearly, need re-fine-tuning or use community models)

**Recommendation for ElBruno.LocalLLMs:**
- **Don't maintain fine-tuned models in the library** → point users to HuggingFace community models
- **Provide fine-tuning recipes** (docs, notebooks, example configs) → users fine-tune for custom APIs
- **Validate pre-trained models** → test Phi-3.5-mini, functionary-small-v3.2-3B with library's IChatClient format
- **Target sweet spot:** Qwen2.5-1.5B (tiny) and Phi-3.5-mini (small) for consumer GPU friendliness + quality

**Tools & Frameworks Learned:**
- Unsloth (fastest single-GPU fine-tuning)
- LLaMA-Factory (Web UI for no-code fine-tuning)
- Microsoft Olive (ONNX export for LoRA models)
- BFCL v4 (Berkeley Function Calling Leaderboard)
- Glaive v2, Hermes, xLAM datasets (function calling training data)

**Next Steps (If Team Decides to Pursue Fine-Tuning):**
1. Validate ONNX conversion of Qwen2.5-1.5B and Phi-3.5-mini via Olive
2. Test pre-trained models (functionary-small-v3.2-3B) with library's tool calling format
3. Create proof-of-concept: fine-tune Qwen2.5-1.5B on 500 library-specific examples
4. Write fine-tuning guide in docs/ with example notebooks

---

## 2026-03-27: Team Consensus Established

**Directive from Bruno:** The .NET community cannot train/fine-tune models effectively. The library WILL publish fine-tuned models optimized for .NET developers, removing barriers (no Python toolchains, no conversion pipelines).

**Team Consensus on Model Candidates:**
- **Qwen2.5-0.5B** — PoC candidate (825 MB ONNX INT4, fastest iteration)
- **Qwen2.5-1.5B** — Sweet spot (1.5 GB, best quality/size ratio)
- **Phi-3.5-mini** — Best quality (3.8B, native ONNX, strongest base capabilities)

**Highest-Value Target:** Tool calling fine-tuning (base model accuracy ~45% → fine-tuned 77-85%)

**Technique:** QLoRA on consumer GPU (6-8GB VRAM, 30-45min for 1K examples)

**ONNX Pipeline:** ✅ Full compatibility confirmed — fine-tune with LoRA → merge → convert to ONNX → quantize

**Infrastructure Cost:** ~$150/year total for training/validation

**Research Phase Complete:** Ready for implementation planning (model selection, training infrastructure, publishing workflow).
