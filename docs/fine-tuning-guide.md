# Fine-Tuning Guide for .NET Developers

> **Audience:** .NET developers who want to use pre-fine-tuned models or train their own.

---

## Overview

ElBruno.LocalLLMs provides **fine-tuned Qwen2.5 models** that are optimized for specific tasks like tool calling and RAG. These models are ready to download and use — no Python or training expertise required.

For advanced users, this guide also explains how to fine-tune your own models.

---

## Using Pre-Fine-Tuned Models

The easiest path: download a fine-tuned model from HuggingFace and use it in your .NET app.

### Train with Google Colab (No Local GPU Needed)

If you want to train the models yourself, use the Google Colab notebook — it runs the
full pipeline (train → merge → ONNX convert → upload) on a free cloud GPU:

[![Open In Colab](https://colab.research.google.com/assets/colab-badge.svg)](https://colab.research.google.com/github/elbruno/ElBruno.LocalLLMs/blob/main/scripts/finetune/train_and_publish.ipynb)

1. Click the badge above to open in Colab
2. Set **Runtime → Change runtime type → T4 GPU**
3. Add your HuggingFace token (`HF_TOKEN`) in the 🔑 Secrets sidebar
4. Pick a variant: `"ToolCalling"`, `"RAG"`, or `"Instruct"`
5. Click **Runtime → Run all** — takes ~30 minutes total

The notebook handles everything: installing Unsloth, downloading training data,
QLoRA fine-tuning, LoRA merge, ONNX INT4 conversion, validation, and HuggingFace upload.
See [`scripts/finetune/train_and_publish.ipynb`](../scripts/finetune/train_and_publish.ipynb)
for the source.

### Available Models

| Model | Size | Task | HuggingFace ID |
|-------|------|------|----------------|
| Qwen2.5-0.5B-ToolCalling | ~1 GB | Tool/function calling | `elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling` |
| Qwen2.5-0.5B-RAG | ~1 GB | RAG with citations | `elbruno/Qwen2.5-0.5B-LocalLLMs-RAG` |
| Qwen2.5-0.5B-Instruct | ~1 GB | General-purpose | `elbruno/Qwen2.5-0.5B-LocalLLMs-Instruct` |

> **Coming soon:** 1.5B and 3B variants for even better quality.

### Quick Start

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Use the fine-tuned tool calling model
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05B_ToolCalling
};

using var client = await LocalChatClient.CreateAsync(options);

// Define tools — same API as always
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather)
};

var response = await client.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "What's the weather in Tokyo?")],
    new ChatOptions { Tools = tools });
```

The fine-tuned model downloads automatically from HuggingFace on first use (~1 GB). After that, it's cached locally.

### Which Model Should I Use?

| Scenario | Recommended Model |
|----------|-------------------|
| Tool/function calling | `KnownModels.Qwen25_05B_ToolCalling` |
| RAG with document grounding | `KnownModels.Qwen25_05B_RAG` |
| General chat + tools + RAG | `KnownModels.Qwen25_05B_Instruct_FineTuned` |
| Best quality (larger model) | `KnownModels.Qwen25_3BInstruct` (base, no fine-tune needed) |
| Smallest possible model | `KnownModels.Qwen25_05BInstruct` (base, 0.5B) |

**Rule of thumb:** A fine-tuned 0.5B model often matches or exceeds a base 1.5B model on its specialized task. If you know your use case (e.g., tool calling), pick the fine-tuned variant.

---

## When to Fine-Tune vs Use Base Models

### Use Base Models When

- You need **general-purpose** chat (3B+ models are already good)
- Your use case is **simple Q&A** without tools or RAG
- You want the **largest context window** (fine-tuning doesn't change this)
- You're **prototyping** and don't have training data yet

### Use Fine-Tuned Models When

- You need **reliable tool calling** from small models (0.5B–1.5B)
- Your app uses **RAG** and you want better source citations
- You need the model to follow a **specific output format** consistently
- You want **smaller model + better quality** instead of a larger base model

---

## Fine-Tuning Your Own Model

> **Prerequisites:** Python 3.10+, PyTorch, a GPU with 16+ GB VRAM (RTX 4090 or cloud A100).

This section is for advanced users who want to create custom fine-tuned models for their own domains.

### Step 1: Prepare Training Data

Training data must match the **exact format** used by ElBruno.LocalLLMs' QwenFormatter. Use ShareGPT format:

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant with access to the following tools:\n\n[{\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"description\":\"Get weather for a city\",\"parameters\":{\"type\":\"object\",\"properties\":{\"city\":{\"type\":\"string\"}}}}}]"
    },
    {
      "from": "human",
      "value": "What's the weather in Paris?"
    },
    {
      "from": "gpt",
      "value": "<tool_call>\n{\"name\": \"get_weather\", \"arguments\": {\"city\": \"Paris\"}}\n</tool_call>"
    }
  ]
}
```

**Critical:** The `<tool_call>` tags and JSON structure must match what QwenFormatter produces. See `src/ElBruno.LocalLLMs/Templates/QwenFormatter.cs` for the exact format.

### Step 2: Fine-Tune with Unsloth

[Unsloth](https://github.com/unslothai/unsloth) is the recommended framework — it's fast and memory-efficient.

```bash
pip install unsloth
```

Key hyperparameters for Qwen2.5-0.5B:

| Parameter | Value | Notes |
|-----------|-------|-------|
| Learning rate | 2e-4 | Standard for LoRA |
| Epochs | 3 | 2–5 depending on dataset size |
| LoRA rank | 16 | 8–32 range; higher = more capacity |
| LoRA alpha | 32 | Usually 2x rank |
| Batch size | 4 | Adjust for your GPU memory |
| Max sequence length | 2048 | Match the library's default |

Training time estimates:

| Model | GPU | Dataset Size | Time |
|-------|-----|-------------|------|
| Qwen2.5-0.5B | RTX 4090 | 5K examples | ~2 hours |
| Qwen2.5-1.5B | RTX 4090 | 5K examples | ~4 hours |
| Qwen2.5-3B | A100 80GB | 5K examples | ~3 hours |

### Step 3: Merge LoRA Weights

After training, merge the LoRA adapter back into the base model:

```python
# Merge LoRA weights into base model
model.save_pretrained_merged("merged_model", tokenizer)
```

### Step 4: Convert to ONNX

Use the ONNX Runtime GenAI model builder to convert the merged model:

```bash
pip install onnxruntime-genai

python -m onnxruntime_genai.models.builder \
  -m merged_model \
  -o onnx_model \
  -p int4 \
  -e cpu
```

This produces an INT4-quantized ONNX model ready for ElBruno.LocalLLMs.

### Step 5: Use in Your .NET App

Point `LocalLLMsOptions.ModelPath` at your converted model:

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct,  // Base definition for chat template
    ModelPath = @"C:\models\my-fine-tuned-model"  // Your ONNX model path
};

using var client = await LocalChatClient.CreateAsync(options);
```

### Step 6: Publish to HuggingFace (Optional)

Share your model with the community:

```bash
huggingface-cli upload your-username/YourModel-onnx ./onnx_model
```

---

## Training Data Resources

Useful datasets for fine-tuning:

| Dataset | Source | Use Case |
|---------|--------|----------|
| Glaive Function Calling v2 | [HuggingFace](https://huggingface.co/datasets/glaiveai/glaive-function-calling-v2) | Tool calling (113K examples) |
| MS MARCO | [Microsoft](https://microsoft.github.io/msmarco/) | RAG/question answering |
| Stanford Alpaca | [GitHub](https://github.com/tatsu-lab/stanford_alpaca) | Instruction following |

**Important:** Always convert training data to match the QwenFormatter template format before training. Mismatched formats will produce a model that doesn't work well with the library.

---

## Troubleshooting

### Model produces garbled output
- Ensure training data matches QwenFormatter's exact token format
- Verify ONNX conversion completed without errors
- Try reducing quantization (use INT8 instead of INT4)

### Training runs out of memory
- Reduce batch size to 1–2
- Use gradient checkpointing (`gradient_checkpointing=True`)
- Reduce LoRA rank to 8
- Use Unsloth's 4-bit training mode

### ONNX conversion fails
- Ensure the merged model loads correctly in PyTorch first
- Check `onnxruntime-genai` version compatibility
- See [ONNX Conversion Guide](onnx-conversion.md) for detailed troubleshooting

---

## Further Reading

- [FineTunedToolCalling Sample](../src/samples/FineTunedToolCalling/) — working demo
- [Tool Calling Guide](tool-calling-guide.md) — how tool calling works in the library
- [Supported Models](supported-models.md) — complete model reference
- [ONNX Conversion Guide](onnx-conversion.md) — converting models to ONNX format
- [Fine-tuning plan](plan-finetune-qwen.md) — full implementation plan with dataset details
