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

| Tier | Model | Params | HuggingFace ID | RAM/VRAM |
|------|-------|--------|----------------|----------|
| ⚪ Tiny | TinyLlama-1.1B-Chat | 1.1B | TinyLlama/TinyLlama-1.1B-Chat-v1.0 | ~2-4 GB |
| ⚪ Tiny | SmolLM2-1.7B-Instruct | 1.7B | HuggingFaceTB/SmolLM2-1.7B-Instruct | ~2-4 GB |
| ⚪ Tiny | Qwen2.5-0.5B-Instruct | 0.5B | Qwen/Qwen2.5-0.5B-Instruct | ~1-2 GB |
| ⚪ Tiny | Qwen2.5-1.5B-Instruct | 1.5B | Qwen/Qwen2.5-1.5B-Instruct | ~2-4 GB |
| ⚪ Tiny | Gemma-2B-IT | 2B | google/gemma-2b-it | ~4 GB |
| 🟢 Small | Phi-3.5-mini-instruct | 3.8B | microsoft/Phi-3.5-mini-instruct-onnx | ~6-8 GB |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | Qwen/Qwen2.5-3B-Instruct | ~6-8 GB |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | meta-llama/Llama-3.2-3B-Instruct | ~6-8 GB |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | Qwen/Qwen2.5-7B-Instruct | ~8-12 GB |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | meta-llama/Llama-3.1-8B-Instruct | ~8-12 GB |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | mistralai/Mistral-7B-Instruct-v0.3 | ~8-12 GB |
| 🟡 Medium | Phi-4 | 14B | microsoft/phi-4 | ~12-16 GB |
| 🟡 Medium | DeepSeek-R1-Distill-Qwen-14B | 14B | deepseek-ai/DeepSeek-R1-Distill-Qwen-14B | ~12-16 GB |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | Qwen/Qwen2.5-14B-Instruct | ~16-24 GB |
| 🔴 Large | Llama-3.3-70B-Instruct | 70B | meta-llama/Llama-3.3-70B-Instruct | ~40+ GB |
| 🔴 Large | Mixtral-8x7B-Instruct-v0.1 | 46.7B (MoE) | mistralai/Mixtral-8x7B-Instruct-v0.1 | ~24-32 GB |
| 🔴 Large | DeepSeek-R1-Distill-Llama-70B | 70B | deepseek-ai/DeepSeek-R1-Distill-Llama-70B | ~40+ GB |

#### Notes on Model Tiers
- **⚪ Tiny** — Edge/mobile, IoT, fast prototyping. Limited chat quality but great for testing & demos.
- **🟢 Small** — Best quality-to-size ratio. Recommended starting point for the library.
- **🟡 Medium** — Production-quality local inference. DeepSeek-R1 distills excel at reasoning/code.
- **🔴 Large** — Heavy workloads, multi-GPU. Mixtral MoE only activates 2 experts per token (efficient).

### Design Patterns (from reference repos)

- NuGet packaging via Directory.Build.props + CI/CD
- Model download via ElBruno.HuggingFace.Downloader with local cache
- ONNX conversion scripts in /scripts or /python
- Solution structure: src/, tests/, samples/, docs/
- Microsoft.Extensions.AI interface compatibility (IChatClient)
