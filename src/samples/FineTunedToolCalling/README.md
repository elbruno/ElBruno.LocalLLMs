# FineTunedToolCalling Sample

Demonstrates using a **fine-tuned Qwen2.5-0.5B** model for tool calling with ElBruno.LocalLLMs.

## What This Sample Shows

- Loading a fine-tuned ONNX model optimized for tool/function calling
- The same agent loop pattern as [ToolCallingAgent](../ToolCallingAgent/), but with a fine-tuned model
- Improved JSON accuracy when the model generates `<tool_call>` responses
- Three tool types: time queries, math calculations, and weather lookups

## Why Fine-Tuned Models?

Base Qwen2.5-0.5B can generate tool calls, but at 0.5B parameters it sometimes produces malformed JSON or picks the wrong tool. The fine-tuned variant (`Qwen2.5-0.5B-LocalLLMs-ToolCalling`) was trained specifically on ElBruno.LocalLLMs' chat template format to:

- **Produce valid JSON** inside `<tool_call>` tags more reliably
- **Select the correct tool** based on the user's intent
- **Handle multi-tool scenarios** (calling multiple tools in sequence)

## Getting the Fine-Tuned Model

The model downloads automatically from HuggingFace on first run. You can also browse it directly:

- **HuggingFace:** [elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling](https://huggingface.co/elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling)
- **Size:** ~1 GB (ONNX INT4 quantized)
- **Format:** ONNX (ready to use, no conversion needed)

Other fine-tuned variants:

| Model | HuggingFace ID | Use Case |
|-------|---------------|----------|
| ToolCalling (0.5B) | `elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling` | Tool/function calling |
| RAG (0.5B) | `elbruno/Qwen2.5-0.5B-LocalLLMs-RAG` | RAG with source citations |
| Instruct (0.5B) | `elbruno/Qwen2.5-0.5B-LocalLLMs-Instruct` | General-purpose (all tasks) |

## How to Run

```bash
cd src/samples/FineTunedToolCalling
dotnet run
```

## Expected Output

```
🎯 Fine-Tuned Tool Calling Demo
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Comparing base vs fine-tuned Qwen2.5-0.5B for tool calling

Model: Qwen2.5-0.5B-LocalLLMs-ToolCalling
Loading fine-tuned model (first run downloads from HuggingFace)...

═══ Demo 1: Single-Turn Tool Call ═══
👤 User: What time is it in UTC?
🔧 Model requested 1 tool call(s):
   → GetCurrentTime(timezone: UTC)

═══ Demo 2: Multi-Turn Agent Loop ═══
👤 User: What's the weather like in Paris and what is 25 * 4 + 10?
   ⚙️  Round 1: model requested 2 tool call(s)
      → GetWeather(city: Paris)
      → Calculate(a: 25, op: *, b: 4)
   ...
🤖 Assistant: The weather in Paris is 18°C and partly cloudy. 25 * 4 = 100, plus 10 = 110.
```

## What to Expect

- **Cleaner tool calls** — the fine-tuned model produces valid `<tool_call>` JSON more consistently
- **Better tool selection** — picks the right tool for the query
- **Improved multi-tool handling** — correctly identifies when multiple tools are needed

## Learn More

- [Fine-Tuning Guide](../../../docs/fine-tuning-guide.md) — how to fine-tune your own models
- [Tool Calling Guide](../../../docs/tool-calling-guide.md) — comprehensive tool calling documentation
- [ToolCallingAgent](../ToolCallingAgent/) — base model tool calling sample
