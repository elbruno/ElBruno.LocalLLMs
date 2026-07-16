# 🧠 Someone Asked Me to Add a 975B Model to My Library — Here's Why I Can't (Yet)

![Inkling support evaluation — a 975B multimodal brain towering over a small C# laptop in a data center](../images/blog-inkling-support.png)

⚠️ _This blog post was created with the help of AI tools. The geeky fun and the 🤖 in C# are 100% mine._

---

## TL;DR

Someone asked me to add **[Inkling](https://huggingface.co/thinkingmachines/Inkling)** — Thinking Machines' shiny new open-weights model — to **ElBruno.LocalLLMs**. I did the evaluation, and the answer is: **not for now.** 🔴

Inkling is a **975B-parameter, Mixture-of-Experts, multimodal** (text + image + audio) model. My library runs *local* LLMs in .NET through ONNX Runtime GenAI, and Inkling hits **every** wall at once:

- **MoE routing** isn't supported by the ONNX GenAI builder.
- **Multimodal in/out** has no path through the text-only conversion pipeline.
- **Size:** ~490 GB+ of weights *even at INT4* — this is a data-center model, not a laptop model.
- **NVFP4 numerics** + custom architecture = no conversion path exists.

So I documented it honestly as **Not Viable** in [`blocked-models.md`](blocked-models.md), pointed folks at hosted APIs for Inkling itself, and recommended small local alternatives (**Phi-4**, **Qwen2.5-32B**, **DeepSeek-R1-Distill-Qwen-14B**). If you want it on your own hardware today — you'd need a rack, not a ThinkPad. 😅

---

## The Longer Story

Hi! 👋

Every now and then someone opens an issue or drops me a message: *"Hey Bruno, can you add **&lt;shiny new model&gt;** to your library?"* I love those messages. It means people are actually using **ElBruno.LocalLLMs** to run models locally in .NET, and they want the latest and greatest.

This time the request was **Inkling**, from Thinking Machines. And it *is* a fascinating model. But after digging in, I had to give the answer nobody likes to hear: **not right now.** Let me walk you through *why* — because the "why" is genuinely interesting, and it says a lot about the difference between a **frontier cloud model** and a **local model**.

### First, what is Inkling?

Inkling is a general-purpose, **natively multimodal** model. It takes **text, images, and audio** as input and produces text output. Under the hood:

| Property | Value |
|----------|-------|
| **Parameters** | 975B total, 41B active |
| **Architecture** | 66-layer decoder-only transformer |
| **Feed-forward** | Sparse **Mixture-of-Experts** — 6 of 256 experts routed + 2 shared per token |
| **Attention** | Hybrid local + global layers |
| **Vision** | Hierarchical patch encoder |
| **Audio** | Discrete token encoding |
| **Numerics** | BF16 and NVFP4 |

On paper? Gorgeous. It's an impressive piece of engineering, and it benchmarks right alongside the big closed models. But almost every one of those beautiful properties is a **brick wall** for local inference.

### What my library actually does

Quick reminder on how **ElBruno.LocalLLMs** works: it lets you run local LLMs in .NET through the standard `IChatClient` interface, powered by **ONNX Runtime GenAI**. To add a model, I:

1. Convert its weights to **ONNX GenAI format** (usually via `optimum` / the `onnxruntime-genai` builder).
2. Register a `ModelDefinition` in `KnownModels.cs`.
3. Ship it so you can `dotnet add package` and go.

The whole design assumes a model you can realistically **download and run on your own machine** — a laptop, a workstation, maybe a single GPU. That assumption is the crux of the problem.

### The four walls 🧱

**Wall #1 — Mixture of Experts.** ONNX Runtime GenAI's model builder doesn't support MoE routing. It can't express "pick 6 of 256 experts per token." This is the *same* blocker that keeps Mixtral and DeepSeek-V3 out of the library. Inkling just has a *lot* more experts.

**Wall #2 — Multimodal in/out.** My conversion path is **text-generation only** (`text-generation-with-past`). Inkling's vision patch encoder and discrete audio tokenizer have nowhere to go in that pipeline. Even if the text backbone converted cleanly, the image and audio front-ends wouldn't.

**Wall #3 — Size.** This is the big one. 975B parameters is roughly **~2 TB in BF16**. Quantize it all the way down to INT4 and you're *still* looking at **~490 GB+ of weights** — plus multiple terabytes of RAM just to *run the conversion*. For inference you'd need hundreds of GB of VRAM across multiple data-center GPUs. That's not "local" by any definition I can ship to you.

**Wall #4 — Numerics + custom architecture.** Inkling ships in BF16 and **NVFP4** (an NVIDIA 4-bit format, not ONNX). Combined with its custom multimodal MoE design, there's simply **no export path** today.

Any *one* of these would be enough to block it. Inkling has all four. It's essentially a bigger, multimodal sibling of DeepSeek-V3 (671B MoE), which I'd already marked as **Not Viable** for the same reasons.

### So what did I actually do?

I didn't want this to be a silent "no." So I:

- ✅ Ran the full evaluation and wrote it up in **[`docs/blocked-models.md`](blocked-models.md)** with a proper blocker table.
- ✅ Added a heads-up (with a link) in **[`docs/supported-models.md`](supported-models.md)** so nobody wastes an afternoon trying to convert it.
- ✅ Logged it in the changelog.

And most importantly — **alternatives**:

- 👉 **Want Inkling specifically?** Use it via a **hosted API** (the Tinker cookbook or a third-party inference provider). That's what it's built for.
- 👉 **Want something great that runs locally in .NET today?** Try **Phi-4** (14B), **Qwen2.5-32B**, or **DeepSeek-R1-Distill-Qwen-14B**. All native ONNX, all ready to go with `KnownModels`.

### The takeaway

Not every frontier model is meant to run on your machine — and that's okay. Part of maintaining a *local* LLM library is being honest about where "local" ends and "data center" begins. Inkling is a spectacular model. It's just not a **local** one.

If the ONNX Runtime GenAI builder adds MoE + multimodal support down the line, I'll revisit. Until then, Inkling stays on the "not viable" list — documented, explained, and with good alternatives right next to it.

Thanks for the request, whoever you are. Keep 'em coming. 🤖

**— Bruno**

---

📌 _Full technical breakdown: [Blocked Models Reference](blocked-models.md) · Models that DO run locally: [Supported Models](supported-models.md)_
