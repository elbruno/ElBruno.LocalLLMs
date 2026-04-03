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

## Gemma 4 Conversion

> ⚠️ **Status: Pending Runtime Support** — Gemma 4's novel architecture (Per-Layer Embeddings, variable attention head dimensions, KV cache sharing) is not yet supported by the onnxruntime-genai runtime (v0.12.2). Model definitions and conversion scripts are ready for when support is added. See [microsoft/onnxruntime-genai](https://github.com/microsoft/onnxruntime-genai) for updates.

Google Gemma 4 is a new model family with four sizes featuring advanced architectures like Per-Layer Embeddings (PLE) and Mixture of Experts (MoE). A dedicated conversion script is provided for future use when GenAI runtime adds support.

### Gemma 4 Model Variants

| Model | Size | Architecture | Context | HuggingFace ID |
|-------|------|--------------|---------|----------------|
| **E2B IT** | 2.3B effective (5.1B total) | Dense + PLE | 128K | `google/gemma-4-E2B-it` |
| **E4B IT** | 4.5B effective (8B total) | Dense + PLE | 128K | `google/gemma-4-E4B-it` |
| **26B A4B IT** | 3.8B active / 25.2B total | MoE (8/128 experts + 1 shared) | 256K | `google/gemma-4-26B-A4B-it` |
| **31B IT** | 30.7B | Dense | 256K | `google/gemma-4-31B-it` |

### Hardware Requirements

| Model | RAM (Conversion) | Disk Space | Recommended RAM (Inference INT4) |
|-------|-----------------|------------|----------------------------------|
| E2B | 8-12 GB | ~30 GB | 4-6 GB |
| E4B | 16-20 GB | ~50 GB | 6-10 GB |
| 26B | 48-64 GB | ~150 GB | 24-32 GB |
| 31B | 64-80 GB | ~180 GB | 24-40 GB |

### Quick Start

**Convert Gemma 4 E2B (smallest, edge-optimized):**

```bash
python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b
```

**Convert Gemma 4 26B MoE (largest, production):**

```bash
python scripts/convert_gemma4.py --model-size 26b --output-dir ./models/gemma4-26b --quantize int8
```

**PowerShell (Windows):**

```powershell
.\scripts\convert_gemma4.ps1 -ModelSize e4b -OutputDir .\models\gemma4-e4b
```

### Conversion Script Features

The `convert_gemma4.py` script is purpose-built for Gemma 4 and includes:

- ✅ **GenAI compatibility** — Uses `onnxruntime_genai.models.builder` for proper `genai_config.json` and tokenizer setup
- ✅ **Automatic trust_remote_code** — Gemma 4 requires remote code execution for custom architecture
- ✅ **MoE support** — Handles the complex Mixture of Experts routing in the 26B model
- ✅ **Pre-flight checks** — Validates RAM, disk space, and dependencies before starting
- ✅ **Output validation** — Ensures all required files are present after conversion
- ✅ **Clear progress output** — Shows real-time conversion status

> **Note:** The conversion script is ready but requires onnxruntime-genai to add Gemma 4 architecture support. Key blockers:
> - **Per-Layer Embeddings (PLE)** — Requires `per_layer_inputs` tensor not yet supported by GenAI runtime
> - **Variable head dimensions** — Sliding attention (256) vs full attention (512) head sizes
> - **KV cache sharing** — 35 layers share only 15 KV cache pairs

### Usage Examples

**E2B — Edge/Mobile (INT4, smallest):**

```bash
python scripts/convert_gemma4.py \
    --model-size e2b \
    --output-dir ./models/gemma4-e2b \
    --quantize int4
```

**E4B — Laptop/Desktop (INT8, better quality):**

```bash
python scripts/convert_gemma4.py \
    --model-size e4b \
    --output-dir ./models/gemma4-e4b \
    --quantize int8
```

**26B MoE — Server/Workstation (FP16, best quality):**

```bash
python scripts/convert_gemma4.py \
    --model-size 26b \
    --output-dir ./models/gemma4-26b \
    --quantize fp16
```

**31B Dense — High-end Server:**

```bash
python scripts/convert_gemma4.py \
    --model-size 31b \
    --output-dir ./models/gemma4-31b \
    --quantize int4
```

### Quantization Recommendations

| Model | INT4 | INT8 | FP16 | Recommended |
|-------|------|------|------|-------------|
| E2B | ~1.5 GB | ~2.5 GB | ~5 GB | **INT4** (best for edge) |
| E4B | ~2.5 GB | ~4.5 GB | ~9 GB | **INT8** (quality/size balance) |
| 26B | ~13 GB | ~25 GB | ~50 GB | **INT8** (manageable size, good quality) |
| 31B | ~16 GB | ~31 GB | ~62 GB | **INT4** (only option for most systems) |

### Using the Converted Model

After conversion, point your C# code to the output directory:

```csharp
using ElBruno.LocalLLMs;

var options = new LocalLLMsOptions
{
    ModelPath = @"./models/gemma4-e2b",  // Path to converted model
    MaxTokens = 2048,
    Temperature = 0.7f
};

using var client = await LocalChatClient.CreateAsync(options);

var response = await client.CompleteAsync(
    "Explain quantum computing in simple terms."
);
Console.WriteLine(response);
```

### Architecture Notes

**Per-Layer Embeddings (PLE)** — E2B and E4B models use a unique architecture where embeddings are distributed across layers rather than concentrated at the input. This is transparent during conversion and inference.

**Mixture of Experts (MoE)** — The 26B model contains 128 expert networks plus 1 shared expert, but only 8 are active for any given token. This provides 25.2B total parameters with only 3.8B active, giving near-31B quality at much lower inference cost. The ONNX conversion properly handles the expert routing mechanism.

### Troubleshooting Gemma 4

| Problem | Solution |
|---------|----------|
| **"trust_remote_code" error** | The script adds this automatically; if you see this error, update transformers: `pip install -U transformers` |
| **Out of memory during conversion** | Close other applications, or use a machine with more RAM. The 26B/31B models genuinely need 64-80 GB. |
| **MoE conversion fails (26B)** | Ensure you have onnxruntime-genai 0.4.0+: `pip install -U onnxruntime-genai` |
| **Converted model outputs gibberish** | Try higher precision: use `--quantize int8` or `--quantize fp16` instead of int4 |
| **Missing genai_config.json** | Use `convert_gemma4.py` not `convert_to_onnx.py` — the GenAI builder is required |
| **Slow inference on 26B model** | This is expected — MoE routing adds overhead. Use GPU if available (requires CUDA provider). |

### Important Notes

- **Always use `convert_gemma4.py`** for Gemma 4, not the generic `convert_to_onnx.py`. The GenAI builder is required for proper tokenizer and config setup.
- **Gemma 4 requires trust_remote_code** — The script handles this automatically via `--extra_options`.
- **Conversion is CPU-only** — The script targets CPU execution (`-e cpu`). For GPU inference, you'll need to separately configure the CUDA execution provider in your C# code.
- **Disk space doubles during conversion** — Ensure you have 2-3x the final model size free during conversion for intermediate files.

### Dependencies

The Gemma 4 conversion script requires:

```bash
pip install onnxruntime-genai>=0.4.0 huggingface-hub transformers torch
```

Or install all conversion dependencies:

```bash
pip install -r scripts/requirements.txt
```

## See Also

- [scripts/README.md](../scripts/README.md) — conversion script reference and usage examples
- [Supported Models](supported-models.md) — full list of models with ONNX status
- [Getting Started](getting-started.md) — using converted models in your application
