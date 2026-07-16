# 🌟 Gemma 4 Is Here — And My C# Library Is (Almost) Ready

> **🎨 Image prompt:** "A futuristic crystal gemstone glowing with four distinct colorful facets (blue, green, yellow, red) floating above an open laptop running C# code, with neural network patterns radiating outward, dark background with subtle Google colors, 16:9 aspect ratio, cinematic lighting, digital art style"

⚠️ _This blog post was created with the help of AI tools. The geeky fun and the 🤖 in C# are 100% mine._

---

Hi!

So Google just dropped **Gemma 4** — their most capable open model family yet — and I couldn't resist. I spent a good chunk of time digging into the architecture, trying to convert models, hitting walls, finding workarounds, and hitting more walls. Here's where things stand with **ElBruno.LocalLLMs**.

Spoiler: the library is _ready_ for Gemma 4. The ONNX runtime... not so much. But let me tell you the whole story.

---

## Wait, What's Gemma 4?

Google released four new models on April 2, 2026, and they're pretty wild:

| Model | Parameters | What's Cool | Context |
|-------|-----------|-------------|---------|
| **E2B IT** | 5.1B (only 2.3B active!) | Tiny but punches above its weight | 128K |
| **E4B IT** | 8B (4.5B active) | Sweet spot for most use cases | 128K |
| **26B A4B IT** | 25.2B (3.8B active) | MoE — only fires 3.8B params per token 🤯 | 256K |
| **31B IT** | 30.7B | The big one, dense, no tricks | 256K |

The magic sauce is something called **Per-Layer Embeddings (PLE)** — basically, each transformer layer gets its own little embedding input. That's how a 5.1B model acts like a 2.3B one. Clever stuff.

Oh, and they're all **Apache 2.0**. No gating, no license hoops. I like that.

---

## What I Got Working (v0.8.0)

### ✅ Model Definitions — Done

All four Gemma 4 variants are registered and ready to go:

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Gemma4E2BIT  // Smallest, edge-optimized
};
```

I added `Gemma4E2BIT`, `Gemma4E4BIT`, `Gemma4_26BA4BIT`, and `Gemma4_31BIT`. The moment ONNX models exist, you just point and shoot.

### ✅ Chat Template — Already Works

Here's the fun part: Gemma 4 uses the **exact same chat template** as Gemma 2 and 3:

```
<start_of_turn>user
What is the capital of France?<end_of_turn>
<start_of_turn>model
```

My existing `GemmaFormatter` handles it perfectly. Zero code changes needed. System messages fold into the first user turn, tool calling works — the whole thing just... works. I love when that happens.

### ✅ Tool Calling — Yep, That Too

Gemma 4 natively supports function calling, and my formatter already handles the Gemma tool-calling format with proper JSON function definitions. No changes needed.

### ✅ Tests — A Lot of Them

I went a bit overboard here (no regrets):

- **6 model definition tests** — making sure all four variants are correctly registered
- **9 tool-calling tests** — validating function calling scenarios with Gemma 4
- **195 multilingual tests** — this one deserves its own section (see below)

All **697 tests** pass. ✅

### ✅ Conversion Scripts — Ready and Waiting

I wrote dedicated Python and PowerShell conversion scripts:

```bash
python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b
```

They're ready. They just need a runtime that can handle Gemma 4. Which brings me to...

---

## ⏳ The Honest Part: ONNX Conversion Is Blocked

OK, here's where I hit a wall. **The ONNX conversion doesn't work yet.** And it's not because I didn't try — trust me, I _really_ tried.

### What's the Problem?

Gemma 4 has three architectural features that `onnxruntime-genai` v0.12.2 simply doesn't support:

1. **Per-Layer Embeddings (PLE)** — each layer needs a separate `per_layer_inputs` tensor. The runtime expects one embedding output. Not three dozen.

2. **Variable Head Dimensions** — sliding attention layers use `head_dim=256`, full attention layers (every 5th one) use `512`. The runtime config only has ONE `head_size` field. Pick one? Yeah, no.

3. **KV Cache Sharing** — 35 layers share only 15 unique KV cache pairs. The runtime expects a 1:1 mapping. Math doesn't math.

### What I Tried (The Fun Part)

I didn't just shrug and move on. Here's my adventure:

- 🔧 **Patched the GenAI builder** to route Gemma 4 through the Gemma 3 pipeline — it actually produced a 1.6GB ONNX file! But then the runtime choked with a shape mismatch at the full attention layers. So close.
- 🔍 **Examined the onnx-community models** — they have the right structure, but the I/O format is incompatible with GenAI's KV cache management.
- 🧪 **Tried loading as `Gemma4ForCausalLM`** — nope, weights are stored under a multimodal prefix. Mismatch everywhere.
- 🔎 **Searched for pre-release builds** — nothing. 0.12.2 is the latest.
- 📋 **Checked GitHub issues/PRs** — zero Gemma 4 mentions in the repo.

### So When Will It Work?

The moment `onnxruntime-genai` adds Gemma 4 support, I'm ready to go:

- Model definitions ✅
- Chat template ✅
- Tests ✅
- Conversion scripts ✅
- Documentation ✅

I'm watching: [microsoft/onnxruntime-genai releases](https://github.com/microsoft/onnxruntime-genai/releases)

---

## Bonus: I Went Multilingual

While I was in testing mode, I figured — why not make sure all my formatters handle every language properly? So I added **195 multilingual tests** covering:

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

All **7 formatters** (ChatML, Phi3, Llama3, Qwen, Mistral, Gemma, DeepSeek) handle Unicode correctly. If you're running models locally, you probably care about this. I know I do.

---

## Try It Out

Grab v0.8.0:

```bash
dotnet add package ElBruno.LocalLLMs --version 0.8.0
```

Gemma 4 ONNX models aren't ready yet, but there are **25+ other models** that work right now:

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
- 📖 [Supported Models](https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/supported-models.md)
- 🔧 [ONNX Conversion Guide](https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/onnx-conversion.md)
- 🚫 [Blocked Models Reference](https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/blocked-models.md)
- 🌐 [Google Gemma 4 Announcement](https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/)
- 🐙 [GitHub Repository](https://github.com/elbruno/ElBruno.LocalLLMs)

---

Happy coding! 🤖

— Bruno
