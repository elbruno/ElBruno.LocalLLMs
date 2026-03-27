---
license: apache-2.0
language:
- en
tags:
- qwen2.5
- onnx
- onnxruntime-genai
- int4
- tool-calling
- local-llm
- dotnet
- elbruno
- fine-tuned
base_model: {{BASE_MODEL}}
model-index:
- name: {{MODEL_NAME}}
  results: []
---

# {{MODEL_NAME}}

Fine-tuned version of [{{BASE_MODEL}}](https://huggingface.co/{{BASE_MODEL}}) optimized for **{{CAPABILITY}}** in [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs).

> **No Python needed.** Download and use directly in .NET with ONNX Runtime GenAI.

## Model Details

| Property | Value |
|----------|-------|
| **Base Model** | {{BASE_MODEL}} |
| **Fine-Tuning** | QLoRA (rank 16, alpha 32) |
| **Training Data** | Tool calling + RAG + instruction following (5,000 examples) |
| **Format** | ONNX INT4 (ONNX Runtime GenAI) |
| **Size** | ~{{SIZE_MB}} MB |
| **Context Length** | 2,048 tokens |
| **Parameters** | {{MODEL_SIZE}} |
| **License** | Apache 2.0 |

## Key Features

✅ **No Python needed** — Download and use directly in .NET  
✅ **Optimized for ElBruno.LocalLLMs** — Matches QwenFormatter ChatML template exactly  
✅ **Better tool calling accuracy** — Improved `<tool_call>` JSON format compliance  
✅ **RAG grounded answering** — Cites context sources accurately  
✅ **Runs on CPU** — No GPU required (faster with GPU)  
✅ **Tiny model** — {{MODEL_SIZE}} parameters fit on edge devices and laptops

## Usage with ElBruno.LocalLLMs

### Install the NuGet package

```bash
dotnet add package ElBruno.LocalLLMs
```

### C# Code Example

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Configure the fine-tuned model
var options = new LocalLLMsOptions
{
    Model = new ModelDefinition
    {
        Id = "{{MODEL_NAME}}".ToLower(),
        HuggingFaceRepoId = "{{REPO_ID}}",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        SupportsToolCalling = true
    }
};

// Create the chat client (downloads model automatically on first use)
using var client = await LocalChatClient.CreateAsync(options);

// --- Tool Calling Example ---
var tools = new List<AITool>
{
    AIFunctionFactory.Create(
        (string city) => $"{{\"temp\": 22, \"condition\": \"sunny\"}}",
        "get_weather",
        "Get current weather for a city"
    )
};

var response = await client.GetResponseAsync(
    new[] { new ChatMessage(ChatRole.User, "What's the weather in Paris?") },
    new ChatOptions { Tools = tools }
);
Console.WriteLine(response);

// --- RAG Example ---
var ragMessages = new[]
{
    new ChatMessage(ChatRole.System, "Answer based on the provided context."),
    new ChatMessage(ChatRole.User,
        "Context:\n[1] ONNX Runtime GenAI enables local LLM inference.\n\n"
        + "Question: What does ONNX Runtime GenAI do?")
};
var ragResponse = await client.GetResponseAsync(ragMessages);
Console.WriteLine(ragResponse);
```

## Training Details

### Hyperparameters

| Parameter | Value |
|-----------|-------|
| **LoRA Rank** | 16 |
| **LoRA Alpha** | 32 |
| **LoRA Dropout** | 0.05 |
| **Target Modules** | q_proj, k_proj, v_proj, o_proj, gate_proj, up_proj, down_proj |
| **Learning Rate** | 2e-4 |
| **Epochs** | 3 |
| **Batch Size** | 16 (effective: 4 × 4 gradient accumulation) |
| **Optimizer** | paged_adamw_8bit |
| **Scheduler** | Cosine with 50-step warmup |
| **Max Sequence Length** | 2,048 |
| **Precision** | FP16 (mixed precision training) |

### Training Data

The model was fine-tuned on a curated dataset of 5,000 examples:

| Category | Count | Source |
|----------|-------|--------|
| Tool Calling | 2,000 | Glaive Function Calling v2 + custom ElBruno.LocalLLMs examples |
| RAG Grounded | 1,500 | MS MARCO + custom library documentation Q&A |
| Chat Template | 1,500 | Alpaca + ShareGPT (filtered, reformatted to ChatML) |

All training data matches the exact format produced by `QwenFormatter.cs` — including `<tool_call>` tags, ChatML tokens (`<|im_start|>`, `<|im_end|>`), and tool result formatting.

### Training Framework

- **[Unsloth](https://github.com/unslothai/unsloth)** — 2x faster QLoRA training with 50% less VRAM
- **[HuggingFace TRL](https://github.com/huggingface/trl)** — SFTTrainer for supervised fine-tuning
- **Hardware:** NVIDIA RTX 4090 (24 GB VRAM) or equivalent

## Benchmark Results

<!-- Replace with actual benchmark results after evaluation -->

| Metric | Base Model | Fine-Tuned | Improvement |
|--------|-----------|-----------|-------------|
| Tool Call Accuracy | — | — | — |
| JSON Format Compliance | — | — | — |
| RAG Citation Accuracy | — | — | — |
| ChatML Adherence | — | — | — |
| Inference Speed (tokens/sec) | — | — | — |

*Benchmarks will be updated after comprehensive evaluation.*

## ONNX Conversion Pipeline

The model was converted using this pipeline:

```
Qwen2.5 Base → QLoRA Fine-tune → Merge LoRA → ONNX Export (INT4)
```

1. **Fine-tune** with QLoRA (Unsloth + TRL)
2. **Merge** LoRA adapters into base model (`merge_lora.py`)
3. **Convert** to ONNX with `onnxruntime_genai.models.builder` INT4 quantization (`convert_to_onnx.py`)
4. **Validate** against QwenFormatter test suite (`validate_onnx.py`)
5. **Upload** to HuggingFace (`upload_to_hf.py`)

All scripts are available at: [`scripts/finetune/`](https://github.com/elbruno/ElBruno.LocalLLMs/tree/main/scripts/finetune)

## Intended Use

### Primary Use Cases

- **Tool Calling** — Small model that reliably produces `<tool_call>` JSON for function execution
- **RAG** — Grounded answering with source citations from provided context
- **Local Inference** — Privacy-preserving AI on laptops, edge devices, and CI/CD pipelines
- **.NET Applications** — Seamless integration via ElBruno.LocalLLMs NuGet package

### Out of Scope

- Complex multi-step reasoning (use 7B+ models)
- Multilingual tasks (English-only training data)
- Long-context tasks beyond 2,048 tokens
- Safety-critical applications without additional guardrails

## Limitations

- **{{MODEL_SIZE}} model** — Limited reasoning compared to larger models (3B, 7B, 14B)
- **English only** — Not trained on multilingual data
- **Simple tools** — Best with 1–3 tools per conversation; may struggle with 10+ complex tools
- **INT4 quantization** — Slight quality degradation (~1-3%) compared to FP16, especially on edge cases
- **No streaming tool calls** — Tool call output is generated as a complete block

## Citation

```bibtex
@misc{{{MODEL_NAME.lower().replace('-', '_').replace('.', '_')}}},
  author = {{Bruno Capuano}},
  title = {{{MODEL_NAME}}},
  year = {2026},
  publisher = {HuggingFace},
  url = {https://huggingface.co/{{REPO_ID}}}
}
```

## Acknowledgments

- **Base Model:** [Qwen Team](https://github.com/QwenLM/Qwen2.5) — Qwen2.5 family
- **Training Framework:** [Unsloth](https://github.com/unslothai/unsloth) — Fast QLoRA training
- **ONNX Conversion:** [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai) — Microsoft
- **Training Data:** [Glaive AI](https://huggingface.co/glaiveai) — Function calling dataset
- **Library:** [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs) — .NET local LLM inference
