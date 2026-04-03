# 🌟 Gemma 4 Support Coming to ElBruno.LocalLLMs

⚠️ _This blog post was created with the help of AI tools._

---

Hi!

Google just dropped **Gemma 4** — their most capable open model family yet — and we're adding support to **ElBruno.LocalLLMs**! Here's what's ready and what's next.

---

## What Is Gemma 4?

Released April 2, 2026, Gemma 4 is a family of **four models** with groundbreaking architecture features:

| Model | Parameters | Architecture | Context |
|-------|-----------|-------------|---------|
| **E2B IT** | 5.1B (2.3B effective) | Dense + PLE | 128K |
| **E4B IT** | 8B (4.5B effective) | Dense + PLE | 128K |
| **26B A4B IT** | 25.2B (3.8B active) | MoE + PLE | 256K |
| **31B IT** | 30.7B | Dense | 256K |

**Key innovations:**
- 🧩 **Per-Layer Embeddings (PLE)** — each transformer layer gets its own embedding input, enabling smaller effective parameter counts with higher quality
- 🔀 **Mixture of Experts** — the 26B variant activates only 3.8B parameters per token
- 🌍 **Multimodal** — text, image, audio, and video understanding
- ✅ **Apache 2.0** — fully open, no gating

All four models support **tool calling** and have a **128K–256K context window**.

---

## What's Ready Now (v0.8.0)

### ✅ Model Definitions

All four Gemma 4 variants are registered in `KnownModels`:

```csharp
// Use any Gemma 4 model (when ONNX conversion is available)
var options = new LocalLLMsOptions
{
    Model = KnownModels.Gemma4E2BIT  // Smallest, edge-optimized
};
```

Available models:
- `KnownModels.Gemma4E2BIT` — 2.3B effective, great for edge/mobile
- `KnownModels.Gemma4E4BIT` — 4.5B effective, balanced performance
- `KnownModels.Gemma4_26BA4BIT` — MoE, 3.8B active, efficient large model
- `KnownModels.Gemma4_31BIT` — 30.7B dense, maximum quality

### ✅ Chat Template Support

Gemma 4 uses the same chat template as Gemma 2 and 3:

```
<start_of_turn>user
What is the capital of France?<end_of_turn>
<start_of_turn>model
```

The existing `GemmaFormatter` handles this perfectly — no changes needed. System messages are folded into the first user turn, exactly like previous Gemma versions.

### ✅ Tool Calling Support

Gemma 4 natively supports function/tool calling. Our formatter handles the Gemma tool-calling format with proper JSON function definitions.

### ✅ Comprehensive Test Coverage

We added **215+ new tests** for Gemma 4:

- **6 model definition tests** — verify all four variants are correctly registered
- **9 tool-calling formatter tests** — validate function calling with Gemma 4 models
- **195 multilingual tests** — 20+ languages and scripts across all formatters (CJK, Cyrillic, Arabic, Hebrew, Indic, European diacritics, emoji, zero-width characters)

All **697 tests** pass. ✅

### ✅ Conversion Scripts Ready

Dedicated Python and PowerShell conversion scripts are prepared:

```bash
# Convert Gemma 4 E2B (smallest)
python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b

# PowerShell
.\scripts\convert_gemma4.ps1 -ModelSize e2b -OutputDir .\models\gemma4-e2b
```

---

## ⏳ What's Pending: ONNX Runtime Support

Here's the honest truth: **the ONNX conversion is blocked** by the current `onnxruntime-genai` runtime (v0.12.2).

### Why?

Gemma 4 introduces three architectural features that the GenAI runtime doesn't support yet:

1. **Per-Layer Embeddings (PLE)** — Each transformer layer receives a separate `per_layer_inputs` tensor from the embedding layer. The runtime expects a single embedding output.

2. **Variable Attention Head Dimensions** — Sliding attention layers use `head_dim=256`, while full attention layers (every 5th) use `global_head_dim=512`. The runtime's `genai_config.json` only supports a single `head_size`.

3. **KV Cache Sharing** — 35 layers share only 15 unique KV cache pairs. The runtime expects one KV cache per layer.

### What We Tried

We didn't just accept "not supported" — we investigated thoroughly:

- ✅ Patched the GenAI builder to route Gemma 4 through Gemma 3 pipeline — conversion produced 1.6GB ONNX file, but runtime failed with shape mismatch at full attention layers
- ✅ Examined onnx-community models — correct I/O structure but incompatible with GenAI's KV cache management
- ✅ Attempted loading as `Gemma4ForCausalLM` — weights stored under multimodal prefix, mismatch
- ✅ Searched for pre-release GenAI builds — none available
- ✅ Checked GitHub issues/PRs — no Gemma 4 support tracked

### When Will It Work?

The moment `onnxruntime-genai` adds Gemma 4 support, we're ready:
- Model definitions ✅
- Chat template ✅
- Tests ✅
- Conversion scripts ✅
- Documentation ✅

Monitor: [microsoft/onnxruntime-genai releases](https://github.com/microsoft/onnxruntime-genai/releases)

---

## The Bigger Picture: Multilingual Testing

While working on Gemma 4, we also added **195 multilingual formatter tests** covering:

| Script/Language | Examples |
|----------------|----------|
| CJK | 日本語, 中文, 한국어 |
| Cyrillic | Русский |
| Arabic | العربية (RTL) |
| Hebrew | עברית (RTL) |
| Devanagari | हिन्दी |
| Tamil | தமிழ் |
| Thai | ไทย |
| European | Ñ, Ü, Ø, Ž, Ą |
| Emoji | 🤖, 👋, 🌍 |
| Zero-width | ZWJ, ZWNJ characters |

These tests validate all **7 formatters** (ChatML, Phi3, Llama3, Qwen, Mistral, Gemma, DeepSeek) handle Unicode correctly — important for a library that runs models locally across all locales.

---

## Get Started

Update to v0.8.0:

```bash
dotnet add package ElBruno.LocalLLMs --version 0.8.0
```

While Gemma 4 ONNX models aren't available yet, you can use the other **25+ supported models** right now:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Gemma 2 works great today
var options = new LocalLLMsOptions { Model = KnownModels.Gemma2_2BIT };
using var client = await LocalChatClient.CreateAsync(options);
var response = await client.GetResponseAsync([
    new(ChatRole.User, "Tell me about Gemma 4!")
]);
Console.WriteLine(response.Text);
```

---

## Links

- 📦 [NuGet Package](https://www.nuget.org/packages/ElBruno.LocalLLMs)
- 📖 [Supported Models](supported-models.md)
- 🔧 [ONNX Conversion Guide](onnx-conversion.md)
- 🚫 [Blocked Models Reference](blocked-models.md)
- 🌐 [Google Gemma 4 Announcement](https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/)
- 🐙 [GitHub Repository](https://github.com/elbruno/ElBruno.LocalLLMs)

---

_Happy local LLM-ing!_ 🤖

— Bruno
