# Fine-Tuning Scripts for ElBruno.LocalLLMs

Scripts for fine-tuning Qwen2.5 models to improve tool calling, RAG, and chat template adherence with the ElBruno.LocalLLMs library.

## Quick Start with Google Colab ☁️

**No local GPU required!** Open the notebook in Google Colab and train in the cloud for free:

[![Open In Colab](https://colab.research.google.com/assets/colab-badge.svg)](https://colab.research.google.com/github/elbruno/ElBruno.LocalLLMs/blob/main/scripts/finetune/train_and_publish.ipynb)

The Colab notebook runs the entire pipeline end-to-end:
1. Installs dependencies (Unsloth, TRL, ONNX Runtime)
2. Downloads training data from this repo
3. Fine-tunes Qwen2.5-0.5B with QLoRA (~15–30 min on a free T4 GPU)
4. Merges LoRA adapters and converts to ONNX INT4
5. Validates the model and uploads to HuggingFace

**Steps:**
1. Click the Colab badge above
2. Set **Runtime → Change runtime type → T4 GPU**
3. Add your `HF_TOKEN` in the 🔑 Secrets sidebar (or paste directly in the config cell)
4. Choose a `MODEL_VARIANT`: `"ToolCalling"`, `"RAG"`, or `"Instruct"`
5. Click **Runtime → Run all**

---

## Local Quick Start

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

> **Note:** Requires NVIDIA GPU with CUDA support. Tested on RTX 4090 (24 GB) and A100 (40/80 GB).

### 2. Prepare Training Data

Custom training data is already in `training-data/`. To also include external datasets (Glaive, Alpaca):

```bash
python prepare_training_data.py --output-dir ../../training-data
```

Skip external datasets (use custom only):

```bash
python prepare_training_data.py --skip-download --output-dir ../../training-data
```

### 3. Fine-Tune Qwen2.5-0.5B (PoC)

```bash
python train_qwen_05b.py \
    --data-path ../../training-data/combined-train.json \
    --val-path ../../training-data/validation.json \
    --output-dir ./output/qwen25-05b-finetuned
```

**Expected:** ~2 hours on RTX 4090, ~18 GB VRAM usage.

### 4. Merge LoRA Adapters

```bash
python merge_lora.py \
    --base-model Qwen/Qwen2.5-0.5B-Instruct \
    --adapter-path ./output/qwen25-05b-finetuned \
    --output-dir ./output/qwen25-05b-merged
```

### 5. Convert to ONNX INT4

```bash
python convert_to_onnx.py \
    --model-path ./output/qwen25-05b-merged \
    --output-dir ./output/qwen25-05b-onnx \
    --quantization int4
```

### 6. Validate ONNX Model

```bash
python validate_onnx.py --model-path ./output/qwen25-05b-onnx
```

### 7. Upload to HuggingFace

```bash
python upload_to_hf.py \
    --model-path ./output/qwen25-05b-onnx \
    --repo-name elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling \
    --token $HF_TOKEN
```

## Available Training Scripts

| Script | Model | VRAM | Time (RTX 4090) |
|--------|-------|------|-----------------|
| `train_qwen_05b.py` | Qwen2.5-0.5B | ~18 GB | ~2 hours |
| `train_qwen_15b.py` | Qwen2.5-1.5B | ~22 GB | ~4 hours |
| `train_qwen_3b.py` | Qwen2.5-3B | ~24 GB | ~8 hours (or cloud A100) |

## Script Reference

| Script | Purpose |
|--------|---------|
| `prepare_training_data.py` | Download & convert external datasets, merge with custom data |
| `train_qwen_*.py` | QLoRA fine-tuning for each model size |
| `merge_lora.py` | Merge LoRA adapters into base model |
| `convert_to_onnx.py` | Convert HuggingFace model to ONNX INT4 |
| `validate_onnx.py` | Validate ONNX model output format |
| `upload_to_hf.py` | Upload ONNX model to HuggingFace Hub |

## Hyperparameters

| Parameter | 0.5B | 1.5B | 3B |
|-----------|------|------|-----|
| LoRA rank | 16 | 32 | 32 |
| LoRA alpha | 32 | 64 | 64 |
| Learning rate | 2e-4 | 1e-4 | 1e-4 |
| Batch size | 4 | 2 | 1 |
| Grad accum | 4 | 8 | 16 |
| Epochs | 3 | 3 | 3 |

## Troubleshooting

**Out of Memory (OOM)**
- Reduce `--batch-size` to 1
- Reduce `--lora-r` to 8
- Use a cloud GPU (A100 recommended for 3B)

**Unsloth Import Error**
- Ensure CUDA toolkit is installed: `nvcc --version`
- Reinstall: `pip install unsloth[colab-new] --force-reinstall`

**Poor Training Loss**
- Check training data format with `validate_onnx.py`
- Increase epochs to 5
- Increase LoRA rank to 32

## License

Apache 2.0 — same as ElBruno.LocalLLMs.
