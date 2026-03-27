# Training Data for ElBruno.LocalLLMs Fine-Tuned Models

> **Format:** ShareGPT (conversations with `from`/`value` fields)  
> **Target:** Qwen2.5-0.5B / 1.5B / 3B fine-tuning via QLoRA  
> **Spec:** See [docs/training-data-spec.md](../docs/training-data-spec.md)

## Dataset Files

| File | Examples | Category | Description |
|------|----------|----------|-------------|
| `tool-calling-train.json` | 50 | Tool Calling | `<tool_call>` JSON output matching QwenFormatter |
| `rag-grounded-train.json` | 30 | RAG | Context-grounded answers with citations |
| `chat-template-train.json` | 20 | Instruction | General ChatML instruction-following |
| `combined-train.json` | 90 | Mixed | Shuffled union (50/30/20 ratio) |
| `validation.json` | 10 | Mixed | 10% held-out for evaluation |

## Format

All files use **ShareGPT** format:

```json
{
  "conversations": [
    { "from": "system", "value": "..." },
    { "from": "human", "value": "..." },
    { "from": "gpt", "value": "..." }
  ]
}
```

## Tool Calling Format

Tool calls use `<tool_call>` tags matching the library's `QwenFormatter`:

```
<tool_call>
{"name": "get_weather", "arguments": {"city": "Paris"}}
</tool_call>
```

## Regeneration

To download external datasets and regenerate combined data:

```bash
pip install -r ../scripts/finetune/requirements.txt
python ../scripts/finetune/prepare_training_data.py --output-dir .
```

## Sources

- **Custom examples** designed for ElBruno.LocalLLMs format
- **Glaive Function Calling v2** (external, via `prepare_training_data.py`)
- **Alpaca** (external, via `prepare_training_data.py`)

## License

Training data is released under Apache 2.0. External datasets retain their original licenses.
