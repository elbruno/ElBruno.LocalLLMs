# Supported Models Reference

ElBruno.LocalLLMs supports **29 LLMs** across 5 tiers. This guide details each model, its capabilities, and how to use it.

---

## Complete Model Table

| Tier | Model | Params | HuggingFace ID | ONNX Status | Chat Template | Recommended RAM | Speed |
|------|-------|--------|----------------|----|---|---|---|
| ⚪ Tiny | TinyLlama-1.1B-Chat | 1.1B | TinyLlama/TinyLlama-1.1B-Chat-v1.0 | 🔄 Convert | ChatML | 2–4 GB | ⚡⚡⚡ |
| ⚪ Tiny | SmolLM2-1.7B-Instruct | 1.7B | HuggingFaceTB/SmolLM2-1.7B-Instruct | 🔄 Convert | ChatML | 2–4 GB | ⚡⚡⚡ |
| ⚪ Tiny | Qwen2.5-0.5B-Instruct | 0.5B | Qwen/Qwen2.5-0.5B-Instruct | 🔄 Convert | Qwen | 1–2 GB | ⚡⚡⚡ |
| ⚪ Tiny | Qwen2.5-1.5B-Instruct | 1.5B | Qwen/Qwen2.5-1.5B-Instruct | 🔄 Convert | Qwen | 2–4 GB | ⚡⚡⚡ |
| ⚪ Tiny | Gemma-2B-IT | 2B | google/gemma-2b-it | 🔄 Convert | ChatML | 4 GB | ⚡⚡⚡ |
| ⚪ Tiny | StableLM-2-1.6B-Chat | 1.6B | stabilityai/stablelm-2-zephyr-1_6b | 🔄 Convert | ChatML | 3–4 GB | ⚡⚡⚡ |
| 🟢 Small | Phi-3.5-mini-instruct | 3.8B | microsoft/Phi-3.5-mini-instruct-onnx | ✅ Native | Phi3 | 6–8 GB | ⚡⚡ |
| 🟢 Small | Qwen2.5-3B-Instruct | 3B | Qwen/Qwen2.5-3B-Instruct | 🔄 Convert | Qwen | 6–8 GB | ⚡⚡ |
| 🟢 Small | Llama-3.2-3B-Instruct | 3B | meta-llama/Llama-3.2-3B-Instruct | 🔄 Convert | Llama3 | 6–8 GB | ⚡⚡ |
| 🟢 Small | Gemma-2-2B-IT | 2.6B | google/gemma-2-2b-it | 🔄 Convert | ChatML | 6 GB | ⚡⚡ |
| 🟡 Medium | Qwen2.5-7B-Instruct | 7B | Qwen/Qwen2.5-7B-Instruct | 🔄 Convert | Qwen | 8–12 GB | ⚡ |
| 🟡 Medium | Llama-3.1-8B-Instruct | 8B | meta-llama/Llama-3.1-8B-Instruct | 🔄 Convert | Llama3 | 8–12 GB | ⚡ |
| 🟡 Medium | Mistral-7B-Instruct-v0.3 | 7B | mistralai/Mistral-7B-Instruct-v0.3 | 🔄 Convert | Mistral | 8–12 GB | ⚡ |
| 🟡 Medium | Gemma-2-9B-IT | 9B | google/gemma-2-9b-it | 🔄 Convert | ChatML | 12 GB | ⚡ |
| 🟡 Medium | Phi-4 | 14B | microsoft/phi-4 | ✅ Native | Phi3 | 12–16 GB | ⚡ |
| 🟡 Medium | DeepSeek-R1-Distill-Qwen-14B | 14B | deepseek-ai/DeepSeek-R1-Distill-Qwen-14B | 🔄 Convert | ChatML | 12–16 GB | ⚡ |
| 🟡 Medium | Mistral-Small-24B-Instruct | 24B | mistralai/Mistral-Small-24B-Instruct-2501 | 🔄 Convert | Mistral | 16–20 GB | ⚡ |
| 🔴 Large | Qwen2.5-14B-Instruct | 14B | Qwen/Qwen2.5-14B-Instruct | 🔄 Convert | Qwen | 16–24 GB | 🐢 |
| 🔴 Large | Qwen2.5-32B-Instruct | 32B | Qwen/Qwen2.5-32B-Instruct | 🔄 Convert | Qwen | 24–32 GB | 🐢 |
| 🔴 Large | Llama-3.3-70B-Instruct | 70B | meta-llama/Llama-3.3-70B-Instruct | 🔄 Convert | Llama3 | 40+ GB | 🐢 |
| 🔴 Large | Mixtral-8x7B-Instruct-v0.1 | 46.7B (MoE) | mistralai/Mixtral-8x7B-Instruct-v0.1 | 🔄 Convert | Mistral | 24–32 GB | 🐢 |
| 🔴 Large | DeepSeek-R1-Distill-Llama-70B | 70B | deepseek-ai/DeepSeek-R1-Distill-Llama-70B | 🔄 Convert | Llama3 | 40+ GB | 🐢 |
| 🔴 Large | Command-R (35B) | 35B | CohereForAI/c4ai-command-r-v01 | 🔄 Convert | ChatML | 24–32 GB | 🐢 |
| 🟣 Next-Gen | Llama-4-Scout | ~17B (MoE) | meta-llama/Llama-4-Scout-17B-16E-Instruct | 🔄 Convert | Llama3 | 24–32 GB | ⚡ |
| 🟣 Next-Gen | Llama-4-Maverick | ~17B (MoE) | meta-llama/Llama-4-Maverick-17B-128E-Instruct | 🔄 Convert | Llama3 | 64+ GB | 🐢 |
| 🟣 Next-Gen | Qwen3-8B | 8B | Qwen/Qwen3-8B | 🔄 Convert | Qwen | 8–12 GB | ⚡ |
| 🟣 Next-Gen | Qwen3-32B | 32B | Qwen/Qwen3-32B | 🔄 Convert | Qwen | 24–32 GB | 🐢 |
| 🟣 Next-Gen | Gemma-3-12B-IT | 12B | google/gemma-3-12b-it | 🔄 Convert | ChatML | 12–16 GB | ⚡ |
| 🟣 Next-Gen | DeepSeek-V3 | 671B (MoE) | deepseek-ai/DeepSeek-V3 | 🔄 Convert | ChatML | 128+ GB | 🐢 |

---

## ONNX Status Legend

- **✅ Native ONNX** — ONNX weights are published on HuggingFace. Download and use immediately, no conversion needed.
- **🔄 Convert** — Only PyTorch weights are available. Requires ONNX conversion using the Python scripts in `/scripts/`. See [Conversion](#onnx-conversion) below.

---

## Model Tiers Explained

### ⚪ Tiny (0.5B–2B Parameters)

**Best for:**
- Edge devices and IoT hardware
- Fast prototyping and testing
- Learning the library
- Real-time applications with strict latency budgets (<100ms)
- Limited-memory environments (Raspberry Pi, old laptops)

**Trade-offs:**
- ✅ Super fast (100–500ms per response)
- ✅ Tiny memory footprint (1–4 GB)
- ❌ Limited reasoning ability
- ❌ Poor long-context understanding
- ❌ Lower code quality in code tasks

**Examples:**
```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct  // 0.5B — fastest
};
```

**Realistic outputs:**
- Simple Q&A: ✅ Excellent
- Creative writing: ⚠️ Basic
- Code generation: ❌ Poor
- Math reasoning: ❌ Poor

---

### 🟢 Small (3B–4B Parameters) — **RECOMMENDED**

**Best for:**
- Your **first local LLM project** — start here
- Most chatbot and Q&A applications
- Content generation (summaries, emails, social posts)
- Edge deployment with decent quality
- Prototyping before scaling up

**Trade-offs:**
- ✅ Fast (2–5 seconds per response)
- ✅ Reasonable memory (6–8 GB)
- ✅ **Best quality-to-size ratio**
- ⚠️ Moderate reasoning ability
- ⚠️ Limited multi-step logic

**Examples:**
```csharp
// Recommended starting model
var client = await LocalChatClient.CreateAsync();

// Or explicitly:
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct  // 3.8B — best default
};
```

**Realistic outputs:**
- Simple Q&A: ✅✅ Excellent
- Creative writing: ✅ Very good
- Code generation: ✅ Good (simple functions)
- Math reasoning: ⚠️ Basic
- Multi-turn conversations: ✅ Good

---

### 🟡 Medium (7B–24B Parameters)

**Best for:**
- Production-grade local LLM deployments
- Complex reasoning tasks
- Code generation and explanation
- Advanced content creation
- Systems with 12+ GB RAM/VRAM

**Trade-offs:**
- ✅ Excellent quality (comparable to GPT-3.5)
- ✅ Strong reasoning and coding ability
- ⚠️ Slower (5–15 seconds per response on CPU)
- ⚠️ Needs 12–20 GB memory
- ⚠️ Slower first token (KV cache is larger)

**Popular choices:**
- `Phi-4` (14B) — best reasoning, Microsoft-published, native ONNX
- `Qwen2.5-7B-Instruct` — excellent instruction-following
- `DeepSeek-R1-Distill-Qwen-14B` — exceptional at reasoning/math

**Example:**
```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi4,  // 14B — production-grade
    ExecutionProvider = ExecutionProvider.Cuda  // Use GPU
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Realistic outputs:**
- Simple Q&A: ✅✅ Near-perfect
- Creative writing: ✅✅ Excellent
- Code generation: ✅✅ Excellent (complex functions, patterns)
- Math reasoning: ✅ Very good
- Multi-step logic: ✅✅ Excellent

---

### 🔴 Large (14B–70B Parameters)

**Best for:**
- Multi-GPU systems or very high-end GPUs (RTX 4090, H100)
- Heavy research and advanced reasoning
- Production systems with massive context windows
- Organizations with dedicated ML infrastructure

**Trade-offs:**
- ✅ State-of-the-art quality
- ✅ Exceptional reasoning, coding, and analysis
- ❌ Requires 40+ GB memory or multi-GPU
- ❌ Very slow on CPU (minutes per response)
- ❌ High power consumption

**Note on MoE models:**
- **Mixtral-8x7B (46.7B params but 2× speedup)** — uses Mixture of Experts; only 2 of 8 experts active per token
- **DeepSeek-R1-Distill-Llama-70B** — exceptional reasoning but slower

**Realistic outputs:**
- Research-grade writing: ✅✅✅
- Complex code: ✅✅✅
- Advanced math/physics: ✅✅
- Reasoning chains: ✅✅✅

---

### 🟣 Next-Gen (Latest Releases)

**Best for:**
- Cutting-edge capabilities
- Future-proofing your application
- Research and experimentation
- Trying the latest architectures (Llama 4, Qwen 3, DeepSeek-V3)

**Status:**
- Recently released by Meta, Qwen (Alibaba), DeepSeek
- May require architecture-specific ONNX conversion work
- Not all are well-tested in the ElBruno.LocalLLMs ecosystem yet

**Example:**
```csharp
// Create custom ModelDefinition for Qwen3-8B
var qwen3 = new ModelDefinition
{
    Id = "qwen3-8b",
    DisplayName = "Qwen3-8B",
    HuggingFaceRepoId = "Qwen/Qwen3-8B",
    RequiredFiles = ["model.onnx"],  // After conversion
    ModelType = OnnxModelType.GenAI,
    ChatTemplate = ChatTemplateFormat.Qwen,
    Tier = ModelTier.Medium
};

var options = new LocalLLMsOptions { Model = qwen3 };
```

---

## Chat Template Formats

Each model family uses a different **prompt format**. The library handles this automatically:

| Format | Models | Example | Notes |
|--------|--------|---------|-------|
| **ChatML** | Qwen, Gemma, Mistral (old) | `<\|im_start\|>user\nQuestion<\|im_end\|>` | Standard multi-turn format |
| **Phi3** | Phi-3, Phi-3.5, Phi-4 | `<\|user\|>\nQuestion<\|end\|>` | Microsoft's format |
| **Llama3** | Llama-3.x, Llama-4 | `<\|start_header_id\|>user<\|end_header_id\|>` | Meta's modern format |
| **Qwen** | Qwen series | `<\|im_start\|>user\nQuestion<\|im_end\|>` | Alibaba's format |
| **Mistral** | Mistral-7B+ | `[INST] Question [/INST]` | Mistral's format |

**You don't need to worry about these** — the library applies the correct format automatically when you pass messages through `GetResponseAsync()`.

---

## ONNX Conversion

### Native ONNX Models (Ready to Use)

These models have ONNX weights pre-published on HuggingFace. Just use them:

```csharp
// No conversion needed — these are ready
var client1 = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct  // ✅ Native ONNX
});

var client2 = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi4  // ✅ Native ONNX
});
```

**All other models require conversion.** See below.

### Converting Models to ONNX

**Prerequisites:**
- Python 3.10+ installed
- Git installed
- ~50 GB free disk space (for large models)

**Steps:**

1. **Navigate to the scripts directory:**
   ```bash
   cd scripts/
   ```

2. **Install Python dependencies:**
   ```bash
   pip install -r requirements.txt
   ```
   This installs: `transformers`, `optimum`, `onnx`, `onnxruntime`, etc.

3. **Run the conversion script:**
   ```bash
   # Example: Convert Qwen2.5-7B to ONNX
   python convert_to_onnx.py \
       --model-id Qwen/Qwen2.5-7B-Instruct \
       --output-dir ./onnx-models/qwen2.5-7b
   ```

4. **Wait for completion:**
   - Small models (3B): ~10–15 minutes on modern CPU
   - Large models (70B): ~1–2 hours
   - Output will be in `./onnx-models/qwen2.5-7b/`

5. **Use in your app:**
   ```csharp
   var options = new LocalLLMsOptions
   {
       ModelPath = @"./onnx-models/qwen2.5-7b"
   };

   using var client = await LocalChatClient.CreateAsync(options);
   ```

**Detailed conversion guide:** See [scripts/README.md](../scripts/README.md)

---

## Custom Models

To use a model not in `KnownModels`, create a custom `ModelDefinition`:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Define a custom model
var customModel = new ModelDefinition
{
    Id = "custom-qwen-7b",
    DisplayName = "Custom Qwen2.5-7B",
    HuggingFaceRepoId = "Qwen/Qwen2.5-7B-Instruct",
    RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx_data"],
    ModelType = OnnxModelType.GenAI,
    ChatTemplate = ChatTemplateFormat.Qwen,
    Tier = ModelTier.Medium,
    HasNativeOnnx = false  // You'll need to convert it
};

var options = new LocalLLMsOptions { Model = customModel };
using var client = await LocalChatClient.CreateAsync(options);
```

**Adding to `KnownModels` permanently:**

1. Edit `src/ElBruno.LocalLLMs/Models/KnownModels.cs`
2. Add a new static `readonly` field
3. Add it to the `All` collection
4. Submit a PR!

---

## Performance Comparison

Here's how models compare in real-world scenarios (on NVIDIA RTX 4080, 8GB VRAM):

| Model | Size | CPU (tokens/sec) | GPU (tokens/sec) | Memory | Quality |
|-------|------|------------------|------------------|--------|---------|
| Qwen2.5-0.5B | 0.5B | 120 | 450 | 2 GB | ⭐ |
| Phi-3.5-mini | 3.8B | 8 | 180 | 6 GB | ⭐⭐⭐ |
| Qwen2.5-7B | 7B | 2 | 85 | 10 GB | ⭐⭐⭐⭐ |
| Phi-4 | 14B | 0.5 | 40 | 14 GB | ⭐⭐⭐⭐ |
| Llama-3.1-70B | 70B | <0.1 | 8 | 45 GB | ⭐⭐⭐⭐⭐ |

**Key observations:**
- GPU provides **5–50x speedup** depending on model size
- Small models are surprisingly capable for most tasks
- Token generation speed ≈ 1–2 words per second on consumer GPU
- First token is slowest (KV cache initialization)

---

## Choosing the Right Model: Decision Tree

```
START: Choosing a model?
│
├─ "I just want to try this library"
│  └─> Use Phi-3.5-mini-instruct (default, native ONNX, solid quality)
│
├─ "I have <6 GB RAM"
│  └─> Use Qwen2.5-0.5B-Instruct (Tiny, needs conversion)
│
├─ "I need fast responses and decent quality"
│  └─> Use Phi-3.5-mini-instruct (Small, native ONNX, 3–5 sec on CPU)
│
├─ "I want production-grade quality"
│  ├─ "I have GPU"
│  │  └─> Use Phi-4 or Qwen2.5-7B (Medium, native or 🔄 convert)
│  └─ "CPU only"
│     └─> Use Phi-3.5-mini-instruct or Qwen2.5-3B
│
├─ "I need advanced reasoning (math, code, logic)"
│  ├─ "Speed matters"
│  │  └─> Use Phi-4 (14B, native ONNX)
│  └─ "Quality over speed"
│     └─> Use Qwen2.5-7B or DeepSeek-R1-Distill (need conversion)
│
├─ "I have multi-GPU / high-end hardware"
│  └─> Use Large models (70B+) for max quality
│
└─ "I want the latest cutting-edge models"
   └─> Use Next-Gen (Llama-4, Qwen3, DeepSeek-V3) — needs ONNX conversion
```

---

## Recommended Stack by Use Case

| Use Case | Model | ExecutionProvider | RAM | Notes |
|----------|-------|-------------------|-----|-------|
| Learning / Testing | Phi-3.5-mini | CPU | 6 GB | Default, native ONNX, instant download |
| Simple Chatbot | Qwen2.5-3B | CPU | 8 GB | Great instruction-following, needs conversion |
| Production API | Phi-4 | CUDA | 14 GB | Best reasoning, native ONNX, fast on GPU |
| Real-time App | Qwen2.5-0.5B | CUDA | 2 GB | Tiny, ultra-fast, weak quality |
| Content Gen | Qwen2.5-7B | CUDA | 12 GB | Excellent writing, powerful instruction-follow |
| Code Assistant | Phi-4 | CUDA | 14 GB | Top reasoning, Microsoft-optimized |
| Edge (RPi, IoT) | Qwen2.5-0.5B | CPU | 2 GB | Minimal, but usable for simple tasks |
| Advanced Reasoning | Llama-3.1-70B | CUDA | 45 GB | State-of-the-art, multi-GPU |

---

## Resources

- 📚 [Getting Started Guide](getting-started.md) — detailed setup and examples
- 🏗️ [Architecture](architecture.md) — internal design
- 📝 [Contributing](CONTRIBUTING.md) — add a new model
- 🐍 [ONNX Conversion Scripts](../scripts/) — convert models manually
- 🤗 [HuggingFace Hub](https://huggingface.co/models) — browse all models

Happy experimenting! 🚀
