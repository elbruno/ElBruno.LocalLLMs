#!/usr/bin/env python3
"""
Convert microsoft/Fara1.5-9B to ONNX INT4 for ElBruno.LocalLLMs.

Fara1.5-9B is a 9B-parameter computer-use agent fine-tuned from Qwen3.5-9B.
Its config.json declares model_type="qwen3_5", which onnxruntime-genai v0.14.1+
handles natively via its built-in model builder (auto-detected from config.json).

After conversion, the output is uploaded to elbruno/Fara1.5-9B-onnx on HuggingFace.

Usage:
    python convert_fara.py
    python convert_fara.py --output-dir ./my-output --skip-upload
    python convert_fara.py --precision int8 --skip-upload
    python convert_fara.py --skip-conversion --skip-upload  # re-validate existing output

Requirements:
    pip install onnxruntime-genai>=0.14.1 huggingface-hub[cli]>=0.24.0 transformers>=5.2.0 psutil>=5.9.0
"""

import argparse
import json
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

# -- Constants --

SOURCE_MODEL_ID  = "microsoft/Fara1.5-9B"
TARGET_HF_REPO   = "elbruno/Fara1.5-9B-onnx"

# Fara's official context is 262K — too large for ONNX static allocation.
# Cap at 32K which is sufficient for multi-screenshot agentic trajectories.
MAX_CONTEXT_LENGTH = 32768

DISK_REQUIREMENTS = {
    "int4": {"download_gb": 18, "conversion_gb": 40, "output_gb": 6},
    "int8": {"download_gb": 18, "conversion_gb": 40, "output_gb": 10},
    "fp32": {"download_gb": 18, "conversion_gb": 40, "output_gb": 18},
}

REQUIRED_OUTPUT_FILES = [
    "model.onnx",
    "genai_config.json",
    "tokenizer.json",
    "tokenizer_config.json",
    "processor_config.json",
]

# Processor files to copy from the source PyTorch repo to the ONNX output.
# MultiModalProcessor in ORT-GenAI requires these to process vision inputs.
PROCESSOR_FILES = [
    "processor_config.json",
    "preprocessor_config.json",
    "video_preprocessor_config.json",
]

MODEL_CARD = """\
---
license: mit
base_model: microsoft/Fara1.5-9B
tags:
  - onnx
  - onnxruntime-genai
  - qwen3_5
  - computer-use
  - multimodal
  - vision-language-model
  - text-generation
---

# Fara1.5-9B ONNX (INT4)

This repository contains an ONNX INT4 conversion of
[microsoft/Fara1.5-9B](https://huggingface.co/microsoft/Fara1.5-9B)
for use with [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai)
and the [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs) library.

## Model Description

Fara1.5-9B is a multimodal computer use agent (CUA) fine-tuned from Qwen3.5-9B
by Microsoft Research AI Frontiers. It observes browser screenshots and emits
structured tool calls (click, type, scroll, navigate) for autonomous web tasks.

## Conversion Details

| Field | Value |
|---|---|
| Source | `microsoft/Fara1.5-9B` |
| Architecture | qwen3_5 / Qwen3VL (VisionGenAI) |
| Precision | INT4 |
| Context length | 32,768 tokens (capped from official 262K for ONNX compatibility) |
| Builder | onnxruntime-genai built-in model builder v0.14.1+ |

## Usage with ElBruno.LocalLLMs

```csharp
// Fara1.5-9B is a VisionGenAI model — use LocalVisionChatClient
await using var client = new LocalVisionChatClient(new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true   // downloads automatically on first run
});
```

## License

MIT — inherited from the source model.
"""


# -- Preflight Checks --

def check_disk_space(output_dir: Path, precision: str) -> None:
    req = DISK_REQUIREMENTS.get(precision, DISK_REQUIREMENTS["int4"])
    peak_gb = req["download_gb"] + req["conversion_gb"]
    free_gb = shutil.disk_usage(output_dir.parent).free / (1024 ** 3)
    print(f"  Disk: {free_gb:.1f} GB free, {peak_gb} GB needed (download + conversion peak)")
    if free_gb < peak_gb:
        print(f"  WARNING:  WARNING: Only {free_gb:.1f} GB free, but {peak_gb} GB may be needed.")
        print("     Continuing — conversion may fail if disk space runs out.")


def check_ram() -> None:
    if not HAS_PSUTIL:
        print("  RAM: psutil not installed — skipping RAM check")
        return
    ram_gb = psutil.virtual_memory().total / (1024 ** 3)
    print(f"  RAM: {ram_gb:.1f} GB total")
    if ram_gb < 32:
        print("  WARNING:  WARNING: Less than 32 GB RAM detected. VLM conversion may be tight.")


def check_gpu() -> None:
    result = subprocess.run(
        ["nvidia-smi", "--query-gpu=name,memory.total", "--format=csv,noheader"],
        capture_output=True, text=True
    )
    if result.returncode == 0 and result.stdout.strip():
        gpu_info = result.stdout.strip().split("\n")[0]
        print(f"  GPU: {gpu_info} OK")
        print("       (ORT-GenAI builder handles CPU-based quantization)")
    else:
        print("  GPU: No CUDA GPU detected — conversion runs on CPU.")


def check_hf_auth() -> None:
    result = subprocess.run(
        ["hf", "auth", "whoami"],
        capture_output=True, text=True
    )
    if result.returncode == 0:
        username = result.stdout.strip().split("\n")[0]
        print(f"  HuggingFace: authenticated as '{username}' OK")
    else:
        print("  HuggingFace: NOT authenticated WARNING:")
        print("    Run `hf auth login` or set HF_TOKEN env var.")


def check_onnxruntime_genai() -> None:
    result = subprocess.run(
        [sys.executable, "-c", "import onnxruntime_genai; print(onnxruntime_genai.__version__)"],
        capture_output=True, text=True
    )
    if result.returncode == 0:
        print(f"  onnxruntime-genai: {result.stdout.strip()} OK")
    else:
        print("  onnxruntime-genai: NOT installed ERROR:")
        print("    Run: pip install onnxruntime-genai>=0.14.1")
        sys.exit(1)


def detect_builtin_fara_support() -> bool:
    """
    Returns True always — Fara uses qwen3_5 architecture which onnxruntime-genai
    v0.14.1+ handles natively via auto-detection from config.json.
    """
    return True


def run_preflight(output_dir: Path, precision: str, skip_upload: bool) -> None:
    print("\n-- Preflight Checks --------------------------------------------------")
    check_onnxruntime_genai()
    check_ram()
    check_disk_space(output_dir, precision)
    check_gpu()
    if not skip_upload:
        check_hf_auth()
    print()


# -- Conversion --

def download_fara_model(work_dir: Path, cache_dir: Path) -> Path:
    """Download Fara PyTorch weights if not already cached. Returns the local path."""
    fara_pytorch_dir = work_dir / "fara-pytorch"
    fara_pytorch_dir.mkdir(parents=True, exist_ok=True)

    # Check if already downloaded (has at least one safetensors file)
    existing = list(fara_pytorch_dir.glob("*.safetensors"))
    if existing:
        print(f"  Fara PyTorch weights already present ({len(existing)} shards) -- skipping download.")
        return fara_pytorch_dir

    print(f"\n  Downloading {SOURCE_MODEL_ID} -> {fara_pytorch_dir}")
    print("  (This downloads ~18 GB -- may take a while on first run)")
    try:
        from huggingface_hub import snapshot_download
        snapshot_download(
            repo_id=SOURCE_MODEL_ID,
            local_dir=str(fara_pytorch_dir),
            cache_dir=str(cache_dir),
        )
    except Exception as e:
        print(f"  ERROR: Failed to download {SOURCE_MODEL_ID}: {e}")
        sys.exit(1)
    return fara_pytorch_dir


def run_conversion(output_dir: Path, precision: str, cache_dir: Path, work_dir: Path) -> None:
    print("-- Conversion --------------------------------------------------------")
    print(f"  Source model : {SOURCE_MODEL_ID}")
    print(f"  Output dir   : {output_dir}")
    print(f"  Precision    : {precision}")
    print()

    output_dir.mkdir(parents=True, exist_ok=True)
    cache_dir.mkdir(parents=True, exist_ok=True)
    work_dir.mkdir(parents=True, exist_ok=True)

    # Download weights (skips if already present)
    fara_pytorch_dir = download_fara_model(work_dir, cache_dir)

    # Use ORT-GenAI built-in builder with local input path (-i).
    # Fara's config.json declares model_type="qwen3_5" which the builder
    # auto-detects — no need to specify a model type flag.
    print(f"\n  Running onnxruntime_genai.models.builder (precision={precision})...")
    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-i", str(fara_pytorch_dir.resolve()),
        "-o", str(output_dir.resolve()),
        "-p", precision,
        "-e", "cpu",
        "--extra_options", "int4_algo_config=k_quant_linear",
    ]
    print(f"  Command: {' '.join(cmd)}\n")
    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"\n  ERROR: Conversion failed (exit code {result.returncode})")
        sys.exit(result.returncode)

    print("\n  Conversion completed.")

    # Copy processor files from source so MultiModalProcessor can initialize.
    # ORT-GenAI's MultiModalProcessor requires processor_config.json to preprocess
    # images/video for Qwen3VL-style VLMs (Fara uses Qwen3VLProcessor).
    copy_processor_files(fara_pytorch_dir, output_dir)


def copy_processor_files(source_dir: Path, output_dir: Path) -> None:
    """Copy processor config files from PyTorch source to ONNX output directory."""
    print("\n-- Copying Processor Files -------------------------------------------")
    for fname in PROCESSOR_FILES:
        src = source_dir / fname
        dst = output_dir / fname
        if src.exists():
            shutil.copy2(src, dst)
            print(f"  Copied {fname}")
        else:
            print(f"  SKIP {fname} (not in source)")



# -- Context Length Patch --

def patch_context_length(output_dir: Path) -> None:
    """
    Cap context_length to 32,768 in genai_config.json.
    Fara's official 262K context is too large for ONNX static allocation.
    """
    config_path = output_dir / "genai_config.json"
    if not config_path.exists():
        print("  WARNING:  genai_config.json not found — skipping context length patch.")
        return

    with open(config_path, encoding="utf-8") as f:
        config = json.load(f)

    patched = False

    # Patch model.context_length
    model_cfg = config.get("model", {})
    current_ctx = model_cfg.get("context_length", None)
    if current_ctx is None or current_ctx > MAX_CONTEXT_LENGTH:
        config.setdefault("model", {})["context_length"] = MAX_CONTEXT_LENGTH
        patched = True
        print(f"  Patched model.context_length: {current_ctx} → {MAX_CONTEXT_LENGTH}")

    # Patch search.max_length
    search_cfg = config.get("search", {})
    current_max = search_cfg.get("max_length", None)
    if current_max is None or current_max > MAX_CONTEXT_LENGTH:
        config.setdefault("search", {})["max_length"] = MAX_CONTEXT_LENGTH
        patched = True
        print(f"  Patched search.max_length: {current_max} → {MAX_CONTEXT_LENGTH}")

    if patched:
        with open(config_path, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2)
        print("  genai_config.json updated. OK")
    else:
        print(f"  genai_config.json context_length already ≤ {MAX_CONTEXT_LENGTH} — no patch needed.")


# -- Output Validation --

def validate_output(output_dir: Path) -> None:
    print("\n-- Output Validation -------------------------------------------------")
    all_ok = True

    for fname in REQUIRED_OUTPUT_FILES:
        path = output_dir / fname
        if path.exists():
            size_mb = path.stat().st_size / (1024 ** 2)
            print(f"  OK {fname} ({size_mb:.1f} MB)")
        else:
            print(f"  MISSING: {fname}")
            all_ok = False

    # List any additional ONNX files (e.g. vision/embedding encoders for VLMs)
    for onnx_file in sorted(output_dir.glob("*.onnx")):
        if onnx_file.name != "model.onnx":
            size_mb = onnx_file.stat().st_size / (1024 ** 2)
            print(f"  OK {onnx_file.name} ({size_mb:.1f} MB)  [additional component]")

    # Check .onnx.data sidecar files
    for df in sorted(output_dir.glob("*.onnx.data")):
        size_gb = df.stat().st_size / (1024 ** 3)
        print(f"  OK {df.name} ({size_gb:.2f} GB)")

    if not all_ok:
        print("\n  ERROR: Validation failed -- required output files are missing.")
        sys.exit(1)

    print("\n  Output validation passed.")

    # Patch context length after validation
    print("\n-- Context Length Patch ----------------------------------------------")
    patch_context_length(output_dir)


# -- HuggingFace Upload --

def upload_to_huggingface(output_dir: Path) -> None:
    print(f"\n-- Upload to HuggingFace ({TARGET_HF_REPO}) --")

    readme_path = output_dir / "README.md"
    readme_path.write_text(MODEL_CARD, encoding="utf-8")
    print("  README.md written.")

    try:
        from huggingface_hub import HfApi, create_repo
    except ImportError:
        print("ERROR: huggingface-hub not installed. Run: pip install huggingface-hub[cli]>=0.24.0")
        sys.exit(1)

    api = HfApi()

    try:
        create_repo(
            repo_id=TARGET_HF_REPO,
            repo_type="model",
            exist_ok=True,
            private=False,
        )
        print(f"  Repo {TARGET_HF_REPO} ready.")
    except Exception as e:
        print(f"  WARNING:  Could not create repo: {e}")

    print(f"  Uploading {output_dir} → {TARGET_HF_REPO} ...")
    api.upload_folder(
        folder_path=str(output_dir),
        repo_id=TARGET_HF_REPO,
        repo_type="model",
        commit_message="Add ONNX INT4 conversion of microsoft/Fara1.5-9B",
    )
    print(f"\nOK Uploaded to https://huggingface.co/{TARGET_HF_REPO}")


# -- Main --

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Convert microsoft/Fara1.5-9B to ONNX INT4 and publish to HuggingFace."
    )
    parser.add_argument(
        "--output-dir",
        default="./converted_models/fara-onnx-int4",
        help="Output directory for ONNX files (default: ./converted_models/fara-onnx-int4)",
    )
    parser.add_argument(
        "--cache-dir",
        default="./cache_dir/fara",
        help="HuggingFace cache directory for model weights",
    )
    parser.add_argument(
        "--work-dir",
        default="./cache_dir/fara-work",
        help="Working directory for intermediate files (builder scripts, PyTorch weights)",
    )
    parser.add_argument(
        "--precision",
        choices=["int4", "int8", "fp32"],
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
        help="Skip conversion (re-upload or re-validate existing output-dir only)",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    cache_dir  = Path(args.cache_dir)
    work_dir   = Path(args.work_dir)

    print("=" * 68)
    print("   Fara1.5-9B -> ONNX Conversion for ElBruno.LocalLLMs")
    print("=" * 68)

    run_preflight(output_dir, args.precision, args.skip_upload)

    if not args.skip_conversion:
        run_conversion(output_dir, args.precision, cache_dir, work_dir)
        validate_output(output_dir)
    else:
        print("-- Conversion skipped (--skip-conversion) --")
        if output_dir.exists() and any(output_dir.iterdir()):
            validate_output(output_dir)
        else:
            print("   (No output directory to validate)")

    if not args.skip_upload:
        upload_to_huggingface(output_dir)
    else:
        print("\n-- Upload skipped (--skip-upload) --")
        print(f"   Output is ready at: {output_dir.resolve()}")

    print("\n Done! Update KnownModels.Fara15_9B to:")
    print(f'   HuggingFaceRepoId = "{TARGET_HF_REPO}"')
    print('   HasNativeOnnx = true')


if __name__ == "__main__":
    main()

