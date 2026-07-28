# Auto-Download Behavior

ElBruno.LocalLLMs supports automatic model downloading out of the box. This page explains how it works, which models are eligible, and how to handle models that require manual ONNX conversion.

---

## How auto-download works

When `EnsureModelDownloaded = true` (the default), the library downloads the model from HuggingFace on first use and caches it locally. Subsequent runs reuse the cached copy.

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.MagenticBrain,
    // ModelPath is empty → auto-download from elbruno/MagenticBrain-onnx
    EnsureModelDownloaded = true
};

var client = await LocalChatClient.CreateAsync(options);
```

### Default cache directory

| Platform | Path |
|---|---|
| Windows | `%LOCALAPPDATA%\ElBruno\LocalLLMs\models` |
| macOS/Linux | `~/.local/share/ElBruno/LocalLLMs/models` |

Override with `LocalLLMsOptions.CacheDirectory`.

---

## `HasNativeOnnx` — the auto-download gate

`ModelDefinition.HasNativeOnnx` controls whether auto-download is supported:

| `HasNativeOnnx` | Meaning |
|---|---|
| `true` | ONNX artifacts are published at `HuggingFaceRepoId` — auto-download works. |
| `false` | No published ONNX artifacts yet — a local `ModelPath` must be supplied. |

### Models with auto-download support (sample)

| Model | Repo |
|---|---|
| `KnownModels.MagenticBrain` | `elbruno/MagenticBrain-onnx` |
| `KnownModels.Fara15_9B` | `elbruno/Fara1.5-9B-onnx` |
| `KnownModels.Phi35MiniInstruct` | `microsoft/Phi-3.5-mini-instruct-onnx` |
| `KnownModels.Qwen25_05BInstruct` | `elbruno/Qwen2.5-0.5B-Instruct-onnx` |

See `KnownModels.cs` for the full list. All models with `HasNativeOnnx = true` are auto-downloadable.

---

## Fail-fast validation

If you configure `EnsureModelDownloaded = true` with a model that has `HasNativeOnnx = false` and no `ModelPath`, the library throws an `InvalidOperationException` at startup with an actionable message:

```
Model 'Mixtral-8x7B-Instruct-v0.1' ('mistralai/Mixtral-8x7B-Instruct-v0.1') does not have
ONNX artifacts published for auto-download (HasNativeOnnx=false). Either:
  - Set ModelPath to a local directory containing the model converted to ONNX, or
  - Choose a model with HasNativeOnnx=true (see KnownModels for available models), or
  - Set EnsureModelDownloaded=false and supply ModelPath explicitly.
```

This prevents the confusing pattern of "auto-download enabled" UX that fails silently at inference time.

---

## Using models with `HasNativeOnnx = false`

Some models don't have published ONNX artifacts yet. To use them, convert them manually (see [onnx-conversion.md](onnx-conversion.md)) and point `ModelPath` at the output directory:

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Mixtral8x7BInstructV01,
    ModelPath = @"C:\models\Mixtral-8x7B-Instruct-v0.1-onnx",
    EnsureModelDownloaded = false
};
```

---

## Vision-capable models (`IsVisionCapable`)

Vision-language models (VLMs) accept image inputs in addition to text. Use `model.IsVisionCapable` to detect them:

```csharp
if (model.IsVisionCapable)
{
    // Use LocalVisionChatClient, not LocalChatClient
    var client = await LocalVisionChatClient.CreateAsync(options);
}
```

Currently `IsVisionCapable = true` models:

| Model | Repo |
|---|---|
| `KnownModels.Fara15_9B` | `elbruno/Fara1.5-9B-onnx` |

`IsVisionCapable` is a computed property: `ModelType == OnnxModelType.VisionGenAI`.

---

## MagenticBrain and Fara — first-run experience

Both `MagenticBrain` and `Fara15_9B` are fully supported for auto-download:

```csharp
// MagenticBrain — agentic fine-tune, no manual conversion needed
var agentOptions = new LocalLLMsOptions
{
    Model = KnownModels.MagenticBrain,
    EnsureModelDownloaded = true
};

// Fara1.5-9B — vision computer-use agent, no manual conversion needed
var visionOptions = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true
};
```

No ONNX conversion step is required for either model. The library downloads, caches, and loads them identically to phi models.

---

## Related

- [Supported models](supported-models.md)
- [ONNX conversion guide](onnx-conversion.md)
- [Getting started](getting-started.md)
- [Troubleshooting](troubleshooting-guide.md)
