# ONNX Conversion Guide

Most models in the `ElBruno.LocalLLMs` library need to be converted from HuggingFace format to ONNX before they can run with ONNX Runtime GenAI. This guide covers the conversion process.

## Which Models Need Conversion?

| Status | Models | Notes |
|--------|--------|-------|
| ✅ **Native ONNX** | Phi-3.5 mini instruct, Phi-4 | Published with ONNX weights on HuggingFace — no conversion needed |
| 🔄 **Requires Conversion** | All other 21 models | Must be exported from PyTorch to ONNX format |

When you use a native ONNX model (like `KnownModels.Phi35MiniInstruct`), the library downloads pre-built ONNX files directly. For all other models, you need to convert them first and point the library to the converted files via `LocalLLMsOptions.ModelPath`.

## Prerequisites

- **Python 3.10+** — required for the conversion tools
- **pip** — Python package manager
- **Disk space** — 2-4x the model size during conversion (e.g., a 7B model may need ~30 GB temporarily)
- **RAM** — at least 16 GB recommended; 32+ GB for models >7B

## Setup

Install the conversion dependencies:

```bash
cd scripts
pip install -r requirements.txt
```

The `requirements.txt` includes:

```
optimum[onnxruntime]>=1.18.0
onnxruntime>=1.17.0
transformers>=4.40.0
torch>=2.2.0
```

## Conversion Process

### Step 1: Basic Conversion (INT4 Quantization)

INT4 quantization is the default and recommended for local inference — it produces the smallest files with minimal quality loss:

```bash
python scripts/convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-0.5B-Instruct \
    --output-dir ./models/qwen2.5-0.5b
```

### Step 2: Use the Converted Model

Point `LocalLLMsOptions.ModelPath` to the output directory:

```csharp
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct,
    ModelPath = "./models/qwen2.5-0.5b"
});
```

## Conversion Options

### Quantization Levels

| Option | Flag | Size | Quality | Recommended For |
|--------|------|------|---------|-----------------|
| INT4 | `--quantize int4` (default) | Smallest | Good | Local inference, edge devices |
| INT8 | `--quantize int8` | Medium | Better | When quality matters more than size |
| None | `--quantize none` | Largest | Best | Benchmarking, quality comparison |

### Examples

**INT8 quantization:**

```bash
python scripts/convert_to_onnx.py \
    --model-id meta-llama/Llama-3.2-3B-Instruct \
    --output-dir ./models/llama-3.2-3b \
    --quantize int8
```

**Full precision (no quantization):**

```bash
python scripts/convert_to_onnx.py \
    --model-id microsoft/Phi-3.5-mini-instruct \
    --output-dir ./models/phi-3.5-mini \
    --quantize none
```

**Models requiring `--trust-remote-code`:**

Some models (notably Qwen) need the `--trust-remote-code` flag to load custom model code from HuggingFace:

```bash
python scripts/convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-3B-Instruct \
    --output-dir ./models/qwen2.5-3b \
    --trust-remote-code
```

## How It Works

The conversion script (`scripts/convert_to_onnx.py`) performs two stages:

1. **Export** — Uses [HuggingFace Optimum](https://huggingface.co/docs/optimum/) to export the PyTorch model to ONNX format with the `text-generation-with-past` task (which includes KV cache support for efficient generation)
2. **Quantize** — Applies post-training quantization using ONNX Runtime's quantization tools to reduce model size

The output directory will contain `.onnx` files plus any associated data files, ready for use with ONNX Runtime GenAI.

## Troubleshooting

### Common Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| `ModuleNotFoundError: No module named 'optimum'` | Missing dependencies | Run `pip install -r scripts/requirements.txt` |
| `OutOfMemoryError` during conversion | Insufficient RAM | Close other applications, use a machine with more RAM, or try a smaller model |
| `OSError: ... does not appear to have a file named config.json` | Wrong model ID | Verify the model ID on [huggingface.co](https://huggingface.co) |
| `ValueError: ... requires trust_remote_code=True` | Model uses custom code | Add `--trust-remote-code` flag |
| `No ONNX files found for quantization` | Export failed silently | Check for errors in the export step; ensure sufficient disk space |
| Converted model produces gibberish | Wrong quantization or incomplete conversion | Try `--quantize none` first to isolate; ensure all output files are present |

### Disk Space Planning

| Model Size | Raw Download | During Conversion | Final (INT4) |
|-----------|-------------|-------------------|-------------|
| ~1B params | ~2 GB | ~6 GB | ~0.7 GB |
| ~3B params | ~6 GB | ~18 GB | ~2 GB |
| ~7B params | ~14 GB | ~42 GB | ~4 GB |
| ~14B params | ~28 GB | ~84 GB | ~8 GB |
| ~70B params | ~140 GB | ~420 GB | ~40 GB |

> **Tip:** Clean up the intermediate files after conversion. Only the final output directory is needed at runtime.

## See Also

- [scripts/README.md](../scripts/README.md) — conversion script reference and usage examples
- [Supported Models](supported-models.md) — full list of models with ONNX status
- [Getting Started](getting-started.md) — using converted models in your application
