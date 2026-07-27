#!/usr/bin/env python3
"""
Convert microsoft/MagenticBrain to ONNX INT4 for ElBruno.LocalLLMs.

MagenticBrain is a 14B-parameter orchestration model fine-tuned from Qwen3-14B.
This script converts it to ONNX using onnxruntime-genai's model builder and
optionally uploads the result to elbruno/MagenticBrain-onnx on HuggingFace.

Usage:
    python convert_magentic_brain.py
    python convert_magentic_brain.py --output-dir ./my-output --skip-upload
    python convert_magentic_brain.py --precision int8

Requirements:
    pip install onnxruntime-genai>=0.14.1 huggingface-hub[cli]>=0.24.0 transformers>=5.2.0 torch>=2.11.0 psutil>=5.9.0
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

try:
    import psutil
    HAS_PSUTIL = True
except ImportError:
    HAS_PSUTIL = False

try:
    import torch
    HAS_TORCH = True
except ImportError:
    HAS_TORCH = False

# ── Constants ───────────────────────────────────────────────────────────────

SOURCE_MODEL_ID = "microsoft/MagenticBrain"
TARGET_HF_REPO  = "elbruno/MagenticBrain-onnx"

# Disk space estimates for INT4 conversion
DISK_REQUIREMENTS = {
    "int4": {"download_gb": 28, "conversion_gb": 84, "output_gb": 8},
    "int8": {"download_gb": 28, "conversion_gb": 84, "output_gb": 15},
    "fp16": {"download_gb": 28, "conversion_gb": 84, "output_gb": 28},
}

REQUIRED_OUTPUT_FILES = [
    "model.onnx",
    "genai_config.json",
    "tokenizer.json",
    "tokenizer_config.json",
]

MODEL_CARD = """\
---
license: mit
base_model: microsoft/MagenticBrain
tags:
  - onnx
  - onnxruntime-genai
  - qwen3
  - agentic
  - tool-calling
  - text-generation
---

# MagenticBrain ONNX (INT4)

This repository contains an ONNX INT4 conversion of
[microsoft/MagenticBrain](https://huggingface.co/microsoft/MagenticBrain)
for use with [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai)
and the [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs) library.

## Model Description

MagenticBrain is a 14B-parameter orchestration model fine-tuned from Qwen3-14B by
Microsoft Research AI Frontiers. It is designed for planning, tool selection, multi-turn
tool chaining, and sub-agent delegation in agentic applications.

## Conversion Details

| Field | Value |
|---|---|
| Source | `microsoft/MagenticBrain` |
| Precision | INT4 (kld-block-128 quantization) |
| Execution provider | CPU (universal) |
| Tool | `onnxruntime_genai.models.builder` |
| Architecture | Qwen3-14B decoder-only |

## Usage with ElBruno.LocalLLMs

```csharp
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.MagenticBrain,
    EnsureModelDownloaded = true   // downloads automatically on first run
});
```

## License

MIT — inherited from the source model.
"""


# ── Preflight Checks ────────────────────────────────────────────────────────

def check_disk_space(output_dir: Path, precision: str) -> None:
    req = DISK_REQUIREMENTS.get(precision, DISK_REQUIREMENTS["int4"])
    peak_gb = req["download_gb"] + req["conversion_gb"]
    free_gb = shutil.disk_usage(output_dir.parent).free / (1024 ** 3)
    print(f"  Disk: {free_gb:.1f} GB free, {peak_gb} GB needed (download + conversion peak)")
    if free_gb < peak_gb:
        print(f"  ⚠️  WARNING: Only {free_gb:.1f} GB free, but {peak_gb} GB may be needed during conversion.")
        print("     Continuing anyway — conversion may fail if space runs out.")


def check_ram() -> None:
    if not HAS_PSUTIL:
        print("  RAM: psutil not installed — skipping RAM check")
        return
    ram_gb = psutil.virtual_memory().total / (1024 ** 3)
    print(f"  RAM: {ram_gb:.1f} GB total")
    if ram_gb < 16:
        print("  ⚠️  WARNING: Less than 16 GB RAM detected. Conversion may fail for 14B model.")


def check_gpu() -> None:
    if HAS_TORCH and torch.cuda.is_available():
        name = torch.cuda.get_device_name(0)
        vram_gb = torch.cuda.get_device_properties(0).total_memory / (1024 ** 3)
        print(f"  GPU: {name} ({vram_gb:.1f} GB VRAM) ✅")
        return
    # Fallback: check nvidia-smi (GPU present but torch is CPU-only build)
    result = subprocess.run(
        ["nvidia-smi", "--query-gpu=name,memory.total", "--format=csv,noheader"],
        capture_output=True, text=True
    )
    if result.returncode == 0 and result.stdout.strip():
        gpu_info = result.stdout.strip().split("\n")[0]
        print(f"  GPU: {gpu_info} (torch CPU-only build; conversion uses ORT-GenAI which has GPU support)")
        print("       Tip: install torch+CUDA for faster GPU-accelerated quantization:")
        print("            pip install torch --index-url https://download.pytorch.org/whl/cu124")
    else:
        print("  GPU: No CUDA GPU detected. Conversion will run on CPU (slower but works).")


def check_hf_auth() -> None:
    result = subprocess.run(
        ["hf", "auth", "whoami"],
        capture_output=True, text=True
    )
    if result.returncode == 0:
        username = result.stdout.strip().split("\n")[0]
        print(f"  HuggingFace: authenticated as '{username}' ✅")
    else:
        print("  HuggingFace: NOT authenticated ⚠️")
        print("    Run `hf auth login` or set HF_TOKEN env var before uploading.")


def check_onnxruntime_genai() -> None:
    result = subprocess.run(
        [sys.executable, "-c", "import onnxruntime_genai; print(onnxruntime_genai.__version__)"],
        capture_output=True, text=True
    )
    if result.returncode == 0:
        print(f"  onnxruntime-genai: {result.stdout.strip()} ✅")
    else:
        print("  onnxruntime-genai: NOT installed ❌")
        print("    Run: pip install onnxruntime-genai>=0.14.1")
        sys.exit(1)


def run_preflight(output_dir: Path, precision: str, skip_upload: bool) -> None:
    print("\n── Preflight Checks ──────────────────────────────────────────────")
    check_onnxruntime_genai()
    check_ram()
    check_disk_space(output_dir, precision)
    check_gpu()
    if not skip_upload:
        check_hf_auth()
    print()


# ── Conversion ──────────────────────────────────────────────────────────────

def run_conversion(output_dir: Path, precision: str, cache_dir: Path) -> None:
    print("── Conversion ────────────────────────────────────────────────────")
    print(f"  Source model : {SOURCE_MODEL_ID}")
    print(f"  Output dir   : {output_dir}")
    print(f"  Precision    : {precision}")
    print(f"  Cache dir    : {cache_dir}")
    print()
    print("  This will download ~28 GB of model weights and may take 30–90 minutes.")
    print("  Do not interrupt the process once conversion starts.\n")

    output_dir.mkdir(parents=True, exist_ok=True)
    cache_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-m", SOURCE_MODEL_ID,
        "-o", str(output_dir),
        "-p", precision,
        "-e", "cpu",
        "--cache_dir", str(cache_dir),
    ]

    print(f"  Running: {' '.join(cmd)}\n")

    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"\n❌ Conversion failed (exit code {result.returncode})")
        sys.exit(result.returncode)

    print("\n✅ Conversion completed.")


# ── Output Validation ────────────────────────────────────────────────────────

def validate_output(output_dir: Path) -> None:
    print("\n── Output Validation ─────────────────────────────────────────────")
    all_ok = True
    for fname in REQUIRED_OUTPUT_FILES:
        path = output_dir / fname
        if path.exists():
            size_mb = path.stat().st_size / (1024 ** 2)
            print(f"  ✅ {fname} ({size_mb:.1f} MB)")
        else:
            print(f"  ❌ MISSING: {fname}")
            all_ok = False

    # Check for .onnx.data sidecar (large weight files)
    data_files = list(output_dir.glob("*.onnx.data"))
    for df in data_files:
        size_gb = df.stat().st_size / (1024 ** 3)
        print(f"  ✅ {df.name} ({size_gb:.2f} GB)")

    if not all_ok:
        print("\n❌ Validation failed — required output files are missing.")
        sys.exit(1)

    print("\n✅ All required output files present.")


# ── HuggingFace Upload ───────────────────────────────────────────────────────

def upload_to_huggingface(output_dir: Path) -> None:
    print(f"\n── Upload to HuggingFace ({TARGET_HF_REPO}) ──────────────────────")

    # Write model card
    readme_path = output_dir / "README.md"
    readme_path.write_text(MODEL_CARD, encoding="utf-8")
    print("  README.md written.")

    try:
        from huggingface_hub import HfApi, create_repo
    except ImportError:
        print("❌ huggingface-hub not installed. Run: pip install huggingface-hub[cli]>=0.24.0")
        sys.exit(1)

    api = HfApi()

    # Create repo if it doesn't exist
    try:
        create_repo(
            repo_id=TARGET_HF_REPO,
            repo_type="model",
            exist_ok=True,
            private=False,
        )
        print(f"  Repo {TARGET_HF_REPO} ready.")
    except Exception as e:
        print(f"  ⚠️  Could not create repo: {e}")

    # Upload
    print(f"  Uploading {output_dir} → {TARGET_HF_REPO} ...")
    api.upload_folder(
        folder_path=str(output_dir),
        repo_id=TARGET_HF_REPO,
        repo_type="model",
        commit_message="Add ONNX INT4 conversion of microsoft/MagenticBrain",
    )
    print(f"\n✅ Uploaded to https://huggingface.co/{TARGET_HF_REPO}")


# ── Main ─────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Convert microsoft/MagenticBrain to ONNX INT4 and publish to HuggingFace."
    )
    parser.add_argument(
        "--output-dir",
        default="./converted_models/magentic-brain-onnx-int4",
        help="Directory to write ONNX output files (default: ./converted_models/magentic-brain-onnx-int4)",
    )
    parser.add_argument(
        "--cache-dir",
        default="./cache_dir/magentic-brain",
        help="HuggingFace cache directory for downloaded model weights",
    )
    parser.add_argument(
        "--precision",
        choices=["int4", "int8", "fp16"],
        default="int4",
        help="Quantization precision (default: int4)",
    )
    parser.add_argument(
        "--skip-upload",
        action="store_true",
        help="Convert only — do not upload to HuggingFace",
    )
    parser.add_argument(
        "--skip-conversion",
        action="store_true",
        help="Skip conversion (re-upload existing output-dir only)",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    cache_dir  = Path(args.cache_dir)

    print("=" * 68)
    print("   MagenticBrain -> ONNX Conversion for ElBruno.LocalLLMs")
    print("=" * 68)

    run_preflight(output_dir, args.precision, args.skip_upload)

    if not args.skip_conversion:
        run_conversion(output_dir, args.precision, cache_dir)
        validate_output(output_dir)
    else:
        print("── Conversion skipped (--skip-conversion) ─────────────────────────")
        if output_dir.exists() and any(output_dir.iterdir()):
            validate_output(output_dir)
        else:
            print("   (No output directory to validate)")

    if not args.skip_upload:
        upload_to_huggingface(output_dir)
    else:
        print("\n── Upload skipped (--skip-upload) ──────────────────────────────────")
        print(f"   Output is ready at: {output_dir.resolve()}")

    print("\n🎉 Done! Update KnownModels.MagenticBrain to:")
    print(f'   HuggingFaceRepoId = "{TARGET_HF_REPO}"')
    print('   HasNativeOnnx = true')


if __name__ == "__main__":
    main()
