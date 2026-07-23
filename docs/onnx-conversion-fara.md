# ONNX Conversion Guide — microsoft/Fara1.5-9B

> **Status as of July 2026:** No official Microsoft ONNX release exists for Fara1.5-9B. This guide documents the best-available conversion path using the ORT-GenAI community builder pattern for Qwen VL architectures (Fara is fine-tuned from Qwen3.5-9B). Claims marked **[confirmed]** are verified; **[inferred]** are based on architectural similarity.

---

## Quick Start (Experienced Users)

```bash
# 1. Install tools
pip install onnxruntime-genai huggingface-hub transformers torch

# 2. Get the community Qwen3-VL builder (closest validated equivalent)
mkdir fara-onnx-work && cd fara-onnx-work
mkdir pytorch_reference

hf download onnx-community/Qwen3-4B-VL-ONNX \
  --include "modeling_qwen3_vl.py" \
  --local-dir "./pytorch_reference"

hf download onnx-community/Qwen3-4B-VL-ONNX \
  --include "builder.py" \
  --local-dir "."

# 3. Download the model
hf download microsoft/Fara1.5-9B --local-dir "./fara-pytorch"

# 4. Convert (INT4 recommended)
python builder.py \
  --input "./fara-pytorch" \
  --reference "./pytorch_reference" \
  --output "./fara-onnx-int4" \
  --precision int4

# 5. Use in ElBruno.LocalLLMs
# Set ModelPath = "./fara-onnx-int4" in LocalLLMsOptions
```

> **Windows PowerShell alternative:** See the [Windows-specific commands](#windows-powershell) section below.

---

## Model Overview

| Field | Value |
|---|---|
| **HuggingFace ID** | `microsoft/Fara1.5-9B` |
| **Parameters** | 9 billion |
| **Base architecture** | Qwen3.5-9B (VLM — vision-language decoder) |
| **Modalities** | Text + screenshots (vision-only perception, no DOM) |
| **Context length** | 262,144 tokens (cap at 32,768 for ONNX) |
| **Task** | Computer use agent — browser automation via pixel-grounded actions |
| **License** | MIT |
| **Official ONNX** | ❌ Not available |
| **Community GGUF** | ✅ `prithivMLmods/Fara1.5-9B-GGUF` |

Fara1.5-9B is a multimodal computer use agent (CUA) trained by Microsoft Research AI Frontiers. It observes the browser through screenshots and emits grounded tool calls (click, type, scroll, navigate). Architecturally it is a Qwen3.5-9B VLM, which determines the conversion approach.

---

## Architecture Notes (Informs Conversion)

Fara1.5-9B shares its VLM backbone with the `Qwen/Qwen3-VL-*` family **[confirmed — HuggingFace model card]**. The ORT-GenAI community builder for Qwen3-VL (`onnx-community/Qwen3-4B-VL-ONNX`) is the most directly applicable conversion tool **[inferred by architectural equivalence]**.

The ORT-GenAI model builder (as of v0.14.x) supports a `Qwen` architecture type — it does **not** list a dedicated `fara` type in its builder registry. If a `fara` model type is added to ORT-GenAI in a future release, those commands will supersede the community builder approach documented here.

---

## Prerequisites

### Hardware

| Requirement | Minimum | Recommended |
|---|---|---|
| **RAM (conversion)** | 32 GB | 48+ GB |
| **Disk (total)** | 60 GB free | 80 GB free |
| **GPU (optional)** | — | 16+ GB VRAM for CUDA provider |

**Disk space breakdown for Fara1.5-9B:**

| Stage | Space Used |
|---|---|
| PyTorch download (`hf download`) | ~18 GB (BF16 safetensors) |
| Intermediate ONNX (during conversion) | ~36–40 GB |
| Final output — INT4 quantized | ~5–6 GB |
| Final output — INT8 quantized | ~9–10 GB |

### Software

| Requirement | Version | Notes |
|---|---|---|
| Python | 3.10+ | 3.11 recommended |
| `onnxruntime-genai` | ≥ 0.14.1 | Or install from source |
| `transformers` | ≥ 5.2.0 | Fara requires ≥ 5.2.0 |
| `torch` | ≥ 2.11.0 | CPU is fine for conversion |
| `huggingface-hub` | ≥ 0.24.0 | For `hf download` CLI |
| `hf` CLI | any | `pip install huggingface-hub[cli]` |

```bash
pip install "onnxruntime-genai>=0.14.1" \
            "transformers>=5.2.0" \
            "torch>=2.11.0" \
            "huggingface-hub[cli]>=0.24.0"
```

---

## Step-by-Step Conversion

### Step 1: Set Up Working Directory

```bash
mkdir fara-onnx-work
cd fara-onnx-work
mkdir pytorch_reference
```

### Step 2: Get the Qwen3-VL Builder Scripts

The `onnx-community/Qwen3-4B-VL-ONNX` repo provides a validated `builder.py` and patched `modeling_qwen3_vl.py` for Qwen3 VL architectures. Download just these files — they are small and you do not need the full ONNX weights:

```bash
hf download onnx-community/Qwen3-4B-VL-ONNX \
  --include "modeling_qwen3_vl.py" \
  --local-dir "./pytorch_reference"

hf download onnx-community/Qwen3-4B-VL-ONNX \
  --include "builder.py" \
  --local-dir "."

hf download onnx-community/Qwen3-4B-VL-ONNX \
  --include "qwen3vl-oga-inference.py" \
  --local-dir "."
```

### Step 3: Download Fara1.5-9B from HuggingFace

```bash
hf download microsoft/Fara1.5-9B --local-dir "./fara-pytorch"
```

This downloads ~18 GB of BF16 safetensors. Ensure you have a stable connection — use `--resume-download` if interrupted:

```bash
hf download microsoft/Fara1.5-9B --local-dir "./fara-pytorch" --resume-download
```

### Step 4: Convert to ONNX

#### Option A: INT4 Quantization (Recommended)

Best balance of size (~5-6 GB) and quality for local inference:

```bash
python builder.py \
  --input "./fara-pytorch" \
  --reference "./pytorch_reference" \
  --output "./fara-onnx-int4" \
  --precision int4
```

#### Option B: INT8 Quantization (Higher Quality)

Better output quality at roughly double the size:

```bash
python builder.py \
  --input "./fara-pytorch" \
  --reference "./pytorch_reference" \
  --output "./fara-onnx-int8" \
  --precision int8
```

#### Option C: FP32 (Benchmarking Only)

Full precision — only for evaluating conversion quality, not practical for local inference:

```bash
python builder.py \
  --input "./fara-pytorch" \
  --reference "./pytorch_reference" \
  --output "./fara-onnx-fp32" \
  --precision fp32
```

Conversion takes 15–45 minutes on a CPU, depending on hardware.

### Windows PowerShell

```powershell
# Step 2: Get builder scripts
hf download onnx-community/Qwen3-4B-VL-ONNX `
  --include "modeling_qwen3_vl.py" `
  --local-dir ".\pytorch_reference"

hf download onnx-community/Qwen3-4B-VL-ONNX `
  --include "builder.py" "qwen3vl-oga-inference.py" `
  --local-dir "."

# Step 3: Download model
hf download microsoft/Fara1.5-9B --local-dir ".\fara-pytorch"

# Step 4: Convert
& python builder.py `
  --input ".\fara-pytorch" `
  --reference ".\pytorch_reference" `
  --output ".\fara-onnx-int4" `
  --precision int4
```

---

## Expected Output Structure

After a successful conversion, the output directory contains:

```
fara-onnx-int4/
├── model.onnx                  # Text decoder (INT4 quantized)
├── model.onnx.data             # Weights tensor data (external data file)
├── qwen3vl-vision.onnx         # Vision encoder (FP32)
├── qwen3vl-embedding.onnx      # Image-token embedding injector (FP32)
├── genai_config.json           # ORT-GenAI runtime config
├── tokenizer.json              # Qwen3 tokenizer
├── tokenizer_config.json
├── special_tokens_map.json
└── added_tokens.json
```

The three ONNX files correspond to the three stages of multimodal inference:
1. **Vision encoder** — processes the screenshot pixel data into feature vectors
2. **Embedding injector** — merges visual features into the token embedding space
3. **Text decoder** — autoregressive generation of the action response

---

## genai_config.json

The builder generates this automatically. For reference, the key fields for a Qwen VL model **[confirmed from onnx-community/Qwen3-4B-VL-ONNX]**:

```json
{
  "model": {
    "type": "qwen_vl",
    "context_length": 32768,
    "decoder": {
      "filename": "model.onnx"
    },
    "vision": {
      "filename": "qwen3vl-vision.onnx",
      "num_image_tokens": 1176
    },
    "embedding": {
      "filename": "qwen3vl-embedding.onnx"
    }
  },
  "search": {
    "max_length": 32768
  }
}
```

> **Note on `model.type`:** The mission context mentions a `"fara"` model type for ORT-GenAI. As of ORT-GenAI v0.14.x the builder does not register a `fara` architecture separately — the generated config will use `"qwen_vl"` (or the builder's Qwen VL equivalent). If a future ORT-GenAI release adds a first-class `fara` type, update this field. Check the ORT-GenAI release notes at https://github.com/microsoft/onnxruntime-genai/releases.

---

## Validation

### Text-Only Sanity Check

```bash
python qwen3vl-oga-inference.py \
  -m "./fara-onnx-int4" \
  -e follow_config \
  --non-interactive \
  -pr "Say hello in one short sentence."
```

Expected: model loads and returns a short greeting.

### Screenshot / Vision Sanity Check

```bash
python qwen3vl-oga-inference.py \
  -m "./fara-onnx-int4" \
  -e follow_config \
  --non-interactive \
  --image_paths "./test_image.png" \
  -pr "Describe what you see in this screenshot in one sentence."
```

### Python Validation (ORT-GenAI API)

```python
import onnxruntime_genai as og
from pathlib import Path

model_path = "./fara-onnx-int4"

# Load model
model = og.Model(model_path)
processor = model.create_multimodal_processor()
tokenizer_stream = processor.create_stream()

# Text-only test
prompts = ["<|im_start|>user\nSay hello.<|im_end|>\n<|im_start|>assistant\n"]
inputs = processor(prompts)
params = og.GeneratorParams(model)
params.set_inputs(inputs)
params.set_search_options(max_length=64)

generator = og.Generator(model, params)
print("Output: ", end="", flush=True)
while not generator.is_done():
    generator.compute_logits()
    generator.generate_next_token()
    new_token = generator.get_next_tokens()[0]
    print(tokenizer_stream.decode(new_token), end="", flush=True)
print()

del generator
print("✅ Model loaded and inference succeeded.")
```

---

## Integration with ElBruno.LocalLLMs

### Folder Structure

Point `ModelPath` to the root of the converted output directory:

```
your-app/
└── models/
    └── fara-9b/          ← this is ModelPath
        ├── model.onnx
        ├── model.onnx.data
        ├── qwen3vl-vision.onnx
        ├── qwen3vl-embedding.onnx
        ├── genai_config.json
        └── tokenizer.json
```

### C# Usage

```csharp
using ElBruno.LocalLLMs;

var options = new LocalLLMsOptions
{
    ModelPath = @".\models\fara-9b"
};

using var client = await LocalChatClient.CreateAsync(options);

var response = await client.CompleteAsync(
    "You are a computer use agent. Describe the next action to take."
);
Console.WriteLine(response);
```

### NuGet Package Selection

| Execution Provider | NuGet Package | Use When |
|---|---|---|
| **CPU** | `Microsoft.ML.OnnxRuntimeGenAI` | Default; any machine |
| **DirectML** (Windows GPU) | `Microsoft.ML.OnnxRuntimeGenAI.DirectML` | Windows with AMD/Intel/NVIDIA GPU |
| **CUDA** | `Microsoft.ML.OnnxRuntimeGenAI.Cuda` | NVIDIA GPU with CUDA toolkit |

For Fara1.5-9B (9B parameters), **DirectML or CUDA is strongly recommended** — CPU inference at 9B scale will be very slow (minutes per generation).

---

## Community Fallback: GGUF (llama.cpp)

If the ONNX conversion fails or ORT-GenAI support is incomplete for your use case, a validated community GGUF conversion is available:

**Repository:** `prithivMLmods/Fara1.5-9B-GGUF`

| File | Quant | Size | Quality |
|---|---|---|---|
| `Fara1.5-9B.Q4_K_M.gguf` | Q4_K_M | 5.63 GB | ⭐⭐⭐⭐ Recommended |
| `Fara1.5-9B.Q5_K_M.gguf` | Q5_K_M | 6.47 GB | ⭐⭐⭐⭐⭐ Best quality |
| `Fara1.5-9B.Q8_0.gguf` | Q8_0 | 9.53 GB | Near-lossless |
| `Fara1.5-9B.mmproj-bf16.gguf` | BF16 | 922 MB | Vision projector (required) |

Usage with llama.cpp (for validation and non-ONNX paths):

```bash
# Download
hf download prithivMLmods/Fara1.5-9B-GGUF \
  Fara1.5-9B.Q4_K_M.gguf \
  Fara1.5-9B.mmproj-bf16.gguf \
  --local-dir "./fara-gguf"

# Run (llama.cpp llava-cli)
llama-llava-cli \
  -m "./fara-gguf/Fara1.5-9B.Q4_K_M.gguf" \
  --mmproj "./fara-gguf/Fara1.5-9B.mmproj-bf16.gguf" \
  --image "./screenshot.png" \
  -p "What actions would you take next?"
```

> **Note:** The GGUF path does NOT integrate with ElBruno.LocalLLMs (which requires ORT-GenAI). Use GGUF only for testing and validation, or as a standalone inference path.

---

## Troubleshooting

| Problem | Cause | Solution |
|---|---|---|
| `ModuleNotFoundError: No module named 'onnxruntime_genai'` | Package not installed | `pip install onnxruntime-genai` |
| `KeyError: 'qwen3_vl'` during conversion | Transformers version too old | `pip install -U transformers` (need ≥ 5.2.0) |
| `RuntimeError: Expected pixel_values in CHW format` | Multiple images in one call | Run one image per inference call |
| `OutOfMemoryError` during conversion | Insufficient RAM | 32 GB minimum; close all other apps |
| Conversion hangs indefinitely | Deadlock in multiprocessing on Windows | Run in a fresh terminal; try `set TOKENIZERS_PARALLELISM=false` |
| `FileNotFoundError: modeling_qwen3_vl.py` | Missing reference file | Re-run the `hf download onnx-community/...` step for `pytorch_reference` |
| `genai_config.json` missing after conversion | Builder exited early | Check for errors in the conversion log; ensure disk space ≥ 60 GB free |
| Model outputs only repeated tokens or gibberish | INT4 precision too aggressive | Retry with `--precision int8` |
| `context_length` error at runtime | Default config uses 262K tokens | Edit `genai_config.json` → set `"context_length": 32768` and `"max_length": 32768` |
| CUDA out of memory during inference | 9B model too large for VRAM | Use INT4; or switch to CPU (`-e cpu`) |
| `model type fara not found` (if ORT-GenAI adds fara type) | Config still says `qwen_vl` | Update `genai_config.json` → `"type": "fara"` |

### Context Length Warning

The official Fara1.5-9B supports 262,144 token context. ONNX Runtime GenAI requires that `context_length` be set at export time and cannot grow dynamically. **Cap it at 32,768 tokens** (or lower) when converting:

If the builder does not offer a `--context_length` flag, edit `genai_config.json` after conversion:

```json
{
  "model": {
    "context_length": 32768
  },
  "search": {
    "max_length": 32768
  }
}
```

For multi-screenshot trajectories (Fara's primary use case), 32K tokens is sufficient for approximately 10–20 screenshots with action history.

---

## Alternative: ORT-GenAI Model Builder (Built-In Qwen Path)

If a future ORT-GenAI release adds first-class Fara or Qwen3.5-VL support to its built-in `python -m onnxruntime_genai.models.builder`, the command would be:

```bash
# [INFERRED — not yet validated; use community builder above for now]
python -m onnxruntime_genai.models.builder \
  --model_name_or_path microsoft/Fara1.5-9B \
  --output ./fara-onnx-int4 \
  --precision int4 \
  --execution_provider cpu
```

Check whether this is supported by running:

```bash
python -m onnxruntime_genai.models.builder --help | grep -i "fara\|qwen"
```

If `qwen` or `fara` appears in the output, the built-in builder is available and should be preferred over the community builder.

---

## Confirmed vs Inferred Summary

| Claim | Status | Source |
|---|---|---|
| Fara1.5-9B is based on Qwen3.5-9B | ✅ Confirmed | HuggingFace model card |
| Fara1.5-9B has no official ONNX release | ✅ Confirmed | HuggingFace search, July 2026 |
| Community GGUF available at prithivMLmods | ✅ Confirmed | HuggingFace |
| ORT-GenAI builder supports `Qwen` arch | ✅ Confirmed | ORT-GenAI GitHub README |
| Qwen3-VL community builder works for Qwen3.5 VL variants | ✅ Confirmed | onnx-community/Qwen3-4B-VL-ONNX |
| ORT-GenAI has a `fara` model type | ❓ Unconfirmed | Not found in builder registry as of v0.14.x |
| Fara converts cleanly with Qwen3-VL builder | 🔄 Inferred | Same architecture family; not directly tested |
| INT4 quality is acceptable for CUA tasks | 🔄 Inferred | Based on Qwen VL INT4 results |

---

## See Also

- [onnx-conversion.md](onnx-conversion.md) — General ONNX conversion guide for this library
- [supported-models.md](supported-models.md) — Full model support matrix
- [getting-started.md](getting-started.md) — Using converted models in C#
- [ORT-GenAI Releases](https://github.com/microsoft/onnxruntime-genai/releases) — Check for new `fara` type support
- [onnx-community/Qwen3-4B-VL-ONNX](https://huggingface.co/onnx-community/Qwen3-4B-VL-ONNX) — Reference builder and scripts
- [microsoft/Fara1.5-9B](https://huggingface.co/microsoft/Fara1.5-9B) — Official model card
- [prithivMLmods/Fara1.5-9B-GGUF](https://huggingface.co/prithivMLmods/Fara1.5-9B-GGUF) — Community GGUF fallback
