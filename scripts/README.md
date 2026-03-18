# ONNX Conversion Scripts

Convert HuggingFace models to ONNX format for use with `ElBruno.LocalLLMs`.

## Prerequisites

- Python 3.10+
- pip

## Setup

```bash
pip install -r requirements.txt
```

## Usage

### Basic conversion (INT4 quantization by default)

```bash
python convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-0.5B-Instruct \
    --output-dir ./models/qwen2.5-0.5b
```

### With INT8 quantization

```bash
python convert_to_onnx.py \
    --model-id meta-llama/Llama-3.2-3B-Instruct \
    --output-dir ./models/llama-3.2-3b \
    --quantize int8
```

### No quantization (full precision)

```bash
python convert_to_onnx.py \
    --model-id microsoft/Phi-3.5-mini-instruct \
    --output-dir ./models/phi-3.5-mini \
    --quantize none
```

### Models requiring trust-remote-code

```bash
python convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-3B-Instruct \
    --output-dir ./models/qwen2.5-3b \
    --trust-remote-code
```

## Notes

- **Phi-3.5 and Phi-4** already have native ONNX weights on HuggingFace — no conversion needed. Use them directly with `KnownModels.Phi35MiniInstruct` or `KnownModels.Phi4`.
- Conversion requires significant disk space and RAM. Expect 2-4x the model size during conversion.
- INT4 quantization produces the smallest models with minimal quality loss — recommended for local inference.
- Output files can be used directly with `LocalLLMsOptions.ModelPath`.

## Supported Models

| Model | HuggingFace ID | Native ONNX? |
|-------|---------------|--------------|
| Phi-3.5 mini | `microsoft/Phi-3.5-mini-instruct-onnx` | ✅ Yes |
| Phi-4 | `microsoft/phi-4-onnx` | ✅ Yes |
| Qwen2.5-0.5B | `Qwen/Qwen2.5-0.5B-Instruct` | ❌ Convert |
| Qwen2.5-3B | `Qwen/Qwen2.5-3B-Instruct` | ❌ Convert |
| Llama-3.2-3B | `meta-llama/Llama-3.2-3B-Instruct` | ❌ Convert |
