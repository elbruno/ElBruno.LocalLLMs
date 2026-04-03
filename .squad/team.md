# Squad Team

> ElBruno.LocalLLMs — C# local LLM chat completion library

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Morpheus | Lead | `.squad/agents/morpheus/charter.md` | 🏗️ Active |
| Trinity | Core Dev | `.squad/agents/trinity/charter.md` | 🔧 Active |
| Tank | Tester | `.squad/agents/tank/charter.md` | 🧪 Active |
| Switch | DevOps | `.squad/agents/switch/charter.md` | ⚙️ Active |
| Dozer | ML Engineer | `.squad/agents/dozer/charter.md` | 🔬 Active |
| Mouse | Fine-Tuning Specialist | `.squad/agents/mouse/charter.md` | 🔬 Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Active |
| Ralph | Work Monitor | — | 🔄 Monitor |

## Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Dependencies:** ElBruno.HuggingFace.Downloader (model downloading), ONNX Runtime GenAI
- **Reference Repos:** elbruno/elbruno.localembeddings, elbruno/ElBruno.QwenTTS
- **Created:** 2026-03-17

### Target Models

| Tier | Model | Params | HuggingFace ID | RAM/VRAM | ONNX Status |
|------|-------|--------|----------------|----------|-------------|
| ⚪ Tiny | TinyLlama-1.1B-Chat | 1.1B | TinyLlama/TinyLlama-1.1B-Chat-v1.0 | ~2-4 GB | 🔄 Convert |
| ⚪ Tiny | SmolLM2-1.7B-Instruct | 1.7B | HuggingFaceTB/SmolLM2-1.7B-Instruct | ~2-4 GB | 🔄 Convert |
| ⚪ Tiny | Qwen2.5-0.5B-Instruct | 0.5B | Qwen/Qwen2.5-0.5B-Instruct | ~1-2 GB | 🔄 Convert |
| ⚪ Tiny | Qwen2.5-1.5B-Instruct | 1.5B | Qwen/Qwen2.5-1.5B-Instruct | ~2-4 GB | 🔄 Convert |
| ⚪ Tiny | Gemma-2B-IT | 2B | google/gemma-2b-it | ~4 GB | 🔄 Convert |
| ⚪ Tiny | StableLM-2-1.6B-Chat | 1.6B | stabilityai/stablelm-2-zephyr-1_6b | ~3-4 GB | 🔄 Convert |
| 🟢 Small | Phi-3.5-mini-instruct | 3.8B | microsoft/Phi-3.5-mini-instruct-onnx | ~6-8 GB | ✅ Native ONNX |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | Qwen/Qwen2.5-3B-Instruct | ~6-8 GB | 🔄 Convert |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | meta-llama/Llama-3.2-3B-Instruct | ~6-8 GB | 🔄 Convert |
| 🟢 Small | Gemma-2-2B-IT | 2.6B | google/gemma-2-2b-it | ~6 GB | 🔄 Convert |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | Qwen/Qwen2.5-7B-Instruct | ~8-12 GB | 🔄 Convert |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | meta-llama/Llama-3.1-8B-Instruct | ~8-12 GB | 🔄 Convert |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | mistralai/Mistral-7B-Instruct-v0.3 | ~8-12 GB | 🔄 Convert |
| 🟡 Medium | Gemma-2-9B-IT | 9B | google/gemma-2-9b-it | ~12 GB | 🔄 Convert |
| 🟡 Medium | Phi-4 | 14B | microsoft/phi-4 | ~12-16 GB | ✅ Native ONNX |
| 🟡 Medium | DeepSeek-R1-Distill-Qwen-14B | 14B | deepseek-ai/DeepSeek-R1-Distill-Qwen-14B | ~12-16 GB | 🔄 Convert |
| 🟡 Medium | Mistral-Small-24B-Instruct | 24B | mistralai/Mistral-Small-24B-Instruct-2501 | ~16-20 GB | 🔄 Convert |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | Qwen/Qwen2.5-14B-Instruct | ~16-24 GB | 🔄 Convert |
| 🔴 Large | Qwen2.5-32B-Instruct | 32B | Qwen/Qwen2.5-32B-Instruct | ~24-32 GB | 🔄 Convert |
| 🔴 Large | Llama-3.3-70B-Instruct | 70B | meta-llama/Llama-3.3-70B-Instruct | ~40+ GB | 🔄 Convert |
| 🔴 Large | Mixtral-8x7B-Instruct-v0.1 | 46.7B (MoE) | mistralai/Mixtral-8x7B-Instruct-v0.1 | ~24-32 GB | 🔄 Convert |
| 🔴 Large | DeepSeek-R1-Distill-Llama-70B | 70B | deepseek-ai/DeepSeek-R1-Distill-Llama-70B | ~40+ GB | 🔄 Convert |
| 🔴 Large | Command-R (35B) | 35B | CohereForAI/c4ai-command-r-v01 | ~24-32 GB | 🔄 Convert |
| 🟣 Next-Gen | Llama-4-Scout | ~17B active (MoE) | meta-llama/Llama-4-Scout-17B-16E-Instruct | ~24-32 GB | 🔄 Convert |
| 🟣 Next-Gen | Llama-4-Maverick | ~17B active (MoE) | meta-llama/Llama-4-Maverick-17B-128E-Instruct | ~64+ GB | 🔄 Convert |
| 🟣 Next-Gen | Qwen3-8B | 8B | Qwen/Qwen3-8B | ~8-12 GB | 🔄 Convert |
| 🟣 Next-Gen | Qwen3-32B | 32B | Qwen/Qwen3-32B | ~24-32 GB | 🔄 Convert |
| 🟣 Next-Gen | Gemma-3-12B-IT | 12B | google/gemma-3-12b-it | ~12-16 GB | 🔄 Convert |
| 🟣 Next-Gen | Gemma-4-E2B-IT | E2B (2.3B eff) | google/gemma-4-E2B-it | ~4-6 GB | 🔄 Convert |
| 🟣 Next-Gen | Gemma-4-E4B-IT | E4B (4.5B eff) | google/gemma-4-E4B-it | ~6-10 GB | 🔄 Convert |
| 🟣 Next-Gen | Gemma-4-26B-A4B-IT | 26B MoE (3.8B active) | google/gemma-4-26B-A4B-it | ~24-32 GB | 🔄 Convert |
| 🟣 Next-Gen | Gemma-4-31B-IT | 31B | google/gemma-4-31B-it | ~24-40 GB | 🔄 Convert |
| 🟣 Next-Gen | DeepSeek-V3 | 671B (MoE) | deepseek-ai/DeepSeek-V3 | ~128+ GB | 🔄 Convert |

#### ONNX Status Legend
- **✅ Native ONNX** — Published ONNX weights on HuggingFace, ready to use directly
- **🔄 Convert** — Requires HuggingFace → ONNX conversion (use Python scripts in /scripts)

#### Notes on Model Tiers
- **⚪ Tiny** — Edge/mobile, IoT, fast prototyping. Limited chat quality but great for testing & demos.
- **🟢 Small** — Best quality-to-size ratio. Recommended starting point for the library.
- **🟡 Medium** — Production-quality local inference. DeepSeek-R1 distills excel at reasoning/code.
- **🔴 Large** — Heavy workloads, multi-GPU. Mixtral/Command-R MoE architectures are efficient.
- **🟣 Next-Gen** — Latest releases (Llama 4, Qwen3, Gemma 3, DeepSeek-V3). May need architecture-specific ONNX conversion work.

### Design Patterns (from reference repos)

- NuGet packaging via Directory.Build.props + CI/CD
- Model download via ElBruno.HuggingFace.Downloader with local cache
- ONNX conversion scripts in /scripts or /python
- Solution structure: src/, tests/, samples/, docs/
- Microsoft.Extensions.AI interface compatibility (IChatClient)
