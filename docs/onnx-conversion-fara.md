# ONNX Conversion Guide — microsoft/Fara1.5-9B

> **Updated July 2026:** Fara1.5-9B now has a native ONNX INT4 conversion published at [`elbruno/Fara1.5-9B-onnx`](https://huggingface.co/elbruno/Fara1.5-9B-onnx). Set `EnsureModelDownloaded = true` in `LocalLLMsOptions` to auto-download — **no manual conversion required**.
>
> The rest of this document is kept for reference in case you want to build your own conversion (e.g., different quantization level or custom output directory).

---

## Quick Start (Auto-Download — Recommended)

```csharp
using ElBruno.LocalLLMs;

var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true,   // downloads ~5 GB on first run from elbruno/Fara1.5-9B-onnx
    MaxSequenceLength = 4096,
    Temperature = 0.1f,
};

await using var client = new LocalVisionChatClient(options);
```

The library downloads from `elbruno/Fara1.5-9B-onnx` and caches locally. Subsequent runs use the cached copy.

---

## Conversion Details (Published Build)

| Field | Value |
|---|---|
| **Source model** | `microsoft/Fara1.5-9B` |
| **ONNX repo** | `elbruno/Fara1.5-9B-onnx` |
| **Architecture** | `qwen3_5` (`Qwen3_5ForConditionalGeneration`) |
| **Parameters** | 9 billion |
| **Precision** | INT4 (k-quant, `k_quant` algorithm) |
| **Output size** | ~4.7 GB (`model.onnx` + `model.onnx.data`) |
| **Context length** | 32,768 tokens (capped from official 262K — see note below) |
| **Builder** | `python -m onnxruntime_genai.models.builder` v0.14.1 |
| **License** | MIT (inherited from source model) |

### Context Length Note

Fara's official context is 262,144 tokens. ONNX Runtime GenAI requires `context_length` to be set statically at export time and cannot grow dynamically. The published build caps it at **32,768 tokens**, which is sufficient for 10–20 screenshots with action history in typical computer-use trajectories. If you need longer context, build your own conversion (see below).

### Output Files

```
fara-onnx-int4/
├── model.onnx              # ONNX graph (pointer file)
├── model.onnx.data         # INT4 quantized weights (~4.7 GB)
├── genai_config.json       # ORT-GenAI runtime config (context_length = 32768)
├── tokenizer.json          # Qwen3.5 tokenizer (~19 MB)
├── tokenizer_config.json
├── config.json
└── chat_template.jinja
```

---

## Model Overview

| Field | Value |
|---|---|
| **HuggingFace ID** | `microsoft/Fara1.5-9B` |
| **Parameters** | 9 billion |
| **Base architecture** | `qwen3_5` (Qwen3.5-9B fine-tune) |
| **Task** | Computer use agent — browser automation via pixel-grounded actions |
| **License** | MIT |
| **Official ONNX** | Not published by Microsoft |
| **Community GGUF** | `prithivMLmods/Fara1.5-9B-GGUF` |

Fara1.5-9B is a multimodal computer use agent (CUA) trained by Microsoft Research AI Frontiers. It observes the browser through screenshots and emits grounded tool calls (click, type, scroll, navigate).

---

## Building Your Own Conversion (Optional)

Use the provided script if you want a different quantization level, a higher context window, or a CUDA-targeted build.

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

### Run the Conversion Script

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
2. Runs `python -m onnxruntime_genai.models.builder -i <local-path> -p int4 -e cpu`
3. Patches `genai_config.json` context_length to 32,768
4. Validates output

Conversion takes 5–30 minutes depending on CPU.

### Manual Conversion (Without Script)

```bash
# Download model
hf download microsoft/Fara1.5-9B --local-dir ./fara-pytorch

# Convert
python -m onnxruntime_genai.models.builder \
  -i ./fara-pytorch \
  -o ./fara-onnx-int4 \
  -p int4 \
  -e cpu

# Patch context length in genai_config.json (set context_length and max_length to 32768)
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

