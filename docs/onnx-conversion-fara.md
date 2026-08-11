# ONNX Conversion Guide — microsoft/Fara1.5-9B

> **Updated July 2026:** [`elbruno/Fara1.5-9B-onnx`](https://huggingface.co/elbruno/Fara1.5-9B-onnx) now includes the validated multimodal package for `LocalVisionChatClient`.
>
> The built-in ORT-GenAI builder is still decoder-only for Fara's `Qwen3_5ForConditionalGeneration` architecture, but `scripts/convert_fara_multimodal.py` fills the missing vision + embedding pieces, patches `genai_config.json` to `model.type = "qwen3_vl"`, writes an ORT-compatible `processor_config.json`, and has been validated locally for text + image inference before upload.

---

## Current Status

- `KnownModels.Fara15_9B` remains modeled as **VisionGenAI** and uses `LocalVisionChatClient`.
- The published Hugging Face repo now contains the full multimodal package:
  - `qwen3vl-vision.onnx`
  - `qwen3vl-embedding.onnx`
  - patched `genai_config.json` with `model.type = "qwen3_vl"`
  - ORT-compatible `processor_config.json`
- Verified locally on 2026-07-28:
  - the custom export script preserves `image_grid_thw` in `qwen3vl-vision.onnx`
  - the sample app succeeds for text inference and real image inference
  - the validated package was uploaded back to `elbruno/Fara1.5-9B-onnx`
- The upstream ORT-GenAI builder is still decoder-only for Fara, so rebuilding the package still requires the custom export script.

---

## Current Builder Limitation

| Field | Value |
|---|---|
| **Source model** | `microsoft/Fara1.5-9B` |
| **ONNX repo** | `elbruno/Fara1.5-9B-onnx` |
| **HF architecture** | `Qwen3_5ForConditionalGeneration` |
| **Parameters** | 9 billion |
| **Precision** | INT4 (k-quant, `k_quant` algorithm) |
| **Current ORT behavior** | exports the decoder/text path only |
| **Context length** | 32,768 tokens (capped from official 262K — see note below) |
| **Builder** | `python -m onnxruntime_genai.models.builder` v0.14.1 |
| **License** | MIT (inherited from source model) |

### Context Length Note

Fara's official context is 262,144 tokens. ONNX Runtime GenAI requires `context_length` to be set statically at export time and cannot grow dynamically. The published build caps it at **32,768 tokens**, which is sufficient for 10–20 screenshots with action history in typical computer-use trajectories. If you need longer context, build your own conversion (see below).

### Current Output Shape

```
fara-onnx-int4/
├── model.onnx                   # decoder-only graph emitted by current builder
├── model.onnx.data              # quantized weights sidecar
├── genai_config.json            # reports model.type = qwen3_5
├── tokenizer.json
├── tokenizer_config.json
├── processor_config.json
├── preprocessor_config.json
├── video_preprocessor_config.json
├── config.json
└── chat_template.jinja
```

This output is useful for debugging the blocker, but it is **not** a validated Fara VLM package.

---

## Model Overview

| Field | Value |
|---|---|
| **HuggingFace ID** | `microsoft/Fara1.5-9B` |
| **Parameters** | 9 billion |
| **Base architecture** | Qwen3.5-VL / Fara multimodal fine-tune |
| **Task** | Computer use agent — browser automation via pixel-grounded actions |
| **License** | MIT |
| **Official ONNX** | Not published by Microsoft |
| **Community GGUF** | `prithivMLmods/Fara1.5-9B-GGUF` |

Fara1.5-9B is a multimodal computer use agent (CUA) trained by Microsoft Research AI Frontiers. It observes the browser through screenshots and emits grounded tool calls (click, type, scroll, navigate).

---

## The New Path: Multimodal Export Script

The ORT-GenAI built-in builder can only produce the **text decoder** for Fara. For a complete multimodal package (the three ONNX files needed by `LocalVisionChatClient`), use `scripts/convert_fara_multimodal.py`:

```
qwen3vl-vision.onnx      — Fara's vision encoder (FP32, exported with torch.onnx.export)
qwen3vl-embedding.onnx   — token embedding + vision token injection (FP32)
model.onnx               — text decoder (INT4, from existing ORT-GenAI builder output)
```

With this complete package and `genai_config.json` updated to `model.type = "qwen3_vl"`, the ORT-GenAI runtime's `qwen3_vl` pipeline handles Fara as a proper VLM.

### Why this works

Fara's `Qwen3_5Model` module has:
- `model.visual` — a compatible Qwen3VL-style vision transformer
- `model.language_model.embed_tokens` — the same embedding structure

The multimodal script uses `torch.onnx.export` directly on these modules (same approach as the `onnx-community/Qwen3-4B-VL-ONNX` community export) to bypass the ORT builder limitation.

## Building Your Own Conversion (Optional)

Use the provided script to inspect the current blocker or to retry once upstream ORT-GenAI support changes. In its current form, the script refuses to republish the known incomplete export.

### Prerequisites

| Requirement | Version |
|---|---|
| Python | 3.10+ |
| `onnxruntime-genai` | ≥ 0.14.1 |
| `onnx-ir` | ≥ 0.2.0 |
| `transformers` | ≥ 5.2.0 |
| `huggingface-hub` | ≥ 0.24.0 |
| RAM | 32 GB minimum |
| Disk free | 60 GB (download + intermediate + output) |

```bash
pip install -r scripts/requirements.txt
```

### Multimodal Export Script (Recommended)

```bash
# Download Fara PyTorch weights and export all three ONNX components:
python scripts/convert_fara_multimodal.py

# Skip upload (test locally first):
python scripts/convert_fara_multimodal.py --skip-upload

# Reuse existing model.onnx decoder from a local decoder-only export:
python scripts/convert_fara_multimodal.py \
  --reuse-decoder ./fara-decoder-only \
  --onnx-dir ./fara-onnx-multimodal \
  --skip-upload
```

The multimodal script:
1. Downloads `microsoft/Fara1.5-9B` to `./cache_dir/fara-work/fara-pytorch/` (skipped if already present)
2. Loads the full PyTorch model (~18 GB RAM required)
3. Exports `qwen3vl-vision.onnx` from `model.model.visual` using `torch.onnx.export`
4. Exports `qwen3vl-embedding.onnx` from `model.model.language_model.embed_tokens`
5. Patches `genai_config.json` → `model.type = "qwen3_vl"` with vision/embedding sections
6. Creates ORT-compatible `processor_config.json` for the image preprocessing pipeline
7. Validates all six required files are present
8. Uploads the complete package to `elbruno/Fara1.5-9B-onnx`

### Text-Decoder-Only Script (Debugging Reference)

```bash
# INT4 (default, ~5 GB output)
python scripts/convert_fara.py --skip-upload

# INT8 (better quality, ~10 GB output)
python scripts/convert_fara.py --precision int8 --skip-upload

# Custom output directory
python scripts/convert_fara.py --output-dir ./my-fara-onnx --skip-upload
```

The script:
1. Downloads `microsoft/Fara1.5-9B` to `./cache_dir/fara-work/fara-pytorch/` (skipped if already present)
2. Inspects the installed ORT-GenAI builder for the known decoder-only Fara path
3. Refuses conversion/upload when that blocker is still present
4. Copies processor config files from the source model into the ONNX output when conversion is allowed
5. Patches `genai_config.json` context_length to 32,768
6. Rejects decoder-only `qwen3_5` output during validation

Conversion takes 5–30 minutes depending on CPU.

### Manual Conversion (Without Script)

```bash
# Download model
hf download microsoft/Fara1.5-9B --local-dir ./fara-pytorch

# Convert (current ORT-GenAI 0.14.1 output is decoder-only and not publishable for Fara VLM use)
python -m onnxruntime_genai.models.builder \
  -i ./fara-pytorch \
  -o ./fara-onnx-int4 \
  -p int4 \
  -e cpu

# Copy processor_config.json / preprocessor_config.json / video_preprocessor_config.json
# Patch context length in genai_config.json (set context_length and max_length to 32768)
# Reject the output if genai_config.json reports model.type = "qwen3_5" and only model.onnx is produced
```

### Use Custom Conversion in ElBruno.LocalLLMs

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    ModelPath = @"./fara-onnx-int4",   // override auto-download with local path
    MaxSequenceLength = 4096,
};
```

---

## Integration with ElBruno.LocalLLMs

```csharp
using ElBruno.LocalLLMs;

// Auto-download (recommended)
var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true,
};
await using var client = new LocalVisionChatClient(options);

// With screenshot
var messages = new List<ChatMessage>
{
    new(ChatRole.User, [
        new TextContent("What action should I take next?"),
        new ImageContent(await File.ReadAllBytesAsync("screenshot.png"), "image/png"),
    ])
};
var response = await client.CompleteAsync(messages);
Console.WriteLine(response);
```

### Fara image resolution

The published `processor_config.json` contains `width: 540` and `height: 360`, but native
inference verification shows that these values do not clamp every image to 540x360 when
`smart_resize` is enabled. The processor preserves the source aspect ratio and scales within
the configured pixel limits, aligning dimensions for the Qwen3-VL patch and merge sizes.

Measurements against `elbruno/Fara1.5-9B-onnx` with ONNX Runtime GenAI 0.15.1 on CPU:

| Source image | Resolved multimodal input tokens | Approximate processed image |
|---|---:|---:|
| 1920x1080 | 2043 image tokens (2100 total, including a 57-token prompt) | 1920x1088 |
| 800x2000 | 1578 image tokens (1635 total, including a 57-token prompt) | 800x2016 |

The approximation uses Qwen3-VL's `patch_size: 16` and `merge_size: 2`, so vision tokens are
approximately `processed_width * processed_height / 1024`. The observed values are consistent
with smart resize and inconsistent with a fixed 540x360 input, which would produce roughly 187
vision tokens regardless of source aspect ratio.

The library logs the resolved multimodal input-token count at debug level. Consumers that map
coordinates back to the source image should use the same 32-pixel alignment rule
(`patch_size * merge_size`) and the source aspect ratio; the current ONNX Runtime GenAI managed
API does not expose the processor's internal `image_grid_thw` tensor or a runtime resize
configuration surface. Therefore no speculative `MaxImagePixels` option is added here.

### NuGet Package Selection

| Execution Provider | NuGet Package |
|---|---|
| **CPU** | `Microsoft.ML.OnnxRuntimeGenAI` |
| **DirectML** (Windows GPU) | `Microsoft.ML.OnnxRuntimeGenAI.DirectML` |
| **CUDA** | `Microsoft.ML.OnnxRuntimeGenAI.Cuda` |

For Fara1.5-9B (9B parameters), **DirectML or CUDA is recommended** — CPU inference at 9B scale will be slow.

---

## Troubleshooting

| Problem | Cause | Solution |
|---|---|---|
| `ModuleNotFoundError: No module named 'onnxruntime_genai'` | Package not installed | `pip install onnxruntime-genai>=0.14.1` |
| `ModuleNotFoundError: No module named 'onnx_ir'` | Missing dep | `pip install onnx-ir>=0.2.0` |
| `OutOfMemoryError` during conversion | Insufficient RAM | 32 GB minimum; close all other apps |
| `genai_config.json` missing after conversion | Builder exited early | Check disk space; ensure ≥ 60 GB free |
| Model outputs repeated tokens | INT4 too aggressive for this model | Retry with `--precision int8` |
| Context length error at runtime | genai_config.json uses 262K | Patch to 32768 — the conversion script does this automatically |
| CUDA OOM during inference | 9B model + INT4 still needs ~6 GB VRAM | Use CPU provider (`-e cpu`) |

---

## Community Fallback: GGUF (llama.cpp)

A validated community GGUF conversion is available at `prithivMLmods/Fara1.5-9B-GGUF` for use with llama.cpp. Note: the GGUF path does NOT integrate with ElBruno.LocalLLMs (which requires ORT-GenAI).

---

## See Also

- [onnx-conversion.md](onnx-conversion.md) — General ONNX conversion guide for this library
- [supported-models.md](supported-models.md) — Full model support matrix
- [getting-started.md](getting-started.md) — Using converted models in C#
- [microsoft/Fara1.5-9B](https://huggingface.co/microsoft/Fara1.5-9B) — Official model card
- [elbruno/Fara1.5-9B-onnx](https://huggingface.co/elbruno/Fara1.5-9B-onnx) — Published ONNX conversion
