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

| Tier | Model | Params | HuggingFace ID |
|------|-------|--------|----------------|
| 🟢 Small | Phi-3.5-mini-instruct | 3.8B | microsoft/Phi-3.5-mini-instruct-onnx |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | Qwen/Qwen2.5-3B-Instruct |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | meta-llama/Llama-3.2-3B-Instruct |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | Qwen/Qwen2.5-7B-Instruct |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | meta-llama/Llama-3.1-8B-Instruct |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | mistralai/Mistral-7B-Instruct-v0.3 |
| 🟡 Medium | Phi-4 | 14B | microsoft/phi-4 |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | Qwen/Qwen2.5-14B-Instruct |
| 🔴 Large | Llama-3.1-70B-Instruct | 70B | meta-llama/Llama-3.1-70B-Instruct |

### Design Patterns (from reference repos)

- NuGet packaging via Directory.Build.props + CI/CD
- Model download via ElBruno.HuggingFace.Downloader with local cache
- ONNX conversion scripts in /scripts or /python
- Solution structure: src/, tests/, samples/, docs/
- Microsoft.Extensions.AI interface compatibility (IChatClient)
