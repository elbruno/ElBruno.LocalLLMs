#!/usr/bin/env python3
"""
Convert Google Gemma 4 models to ONNX format for ElBruno.LocalLLMs.

Supports optional public upload to Hugging Face after local validation.

Examples:
    python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b
    python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b --cache-dir ./hf-cache
    python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b --skip-upload
    python scripts/convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b --skip-conversion
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

REQUIRED_OUTPUT_FILES = [
    "genai_config.json",
    "tokenizer.json",
    "tokenizer_config.json",
]

GEMMA4_MODELS = {
    "e2b": {
        "source_hf": "google/gemma-4-E2B-it",
        "target_hf": "elbruno/Gemma-4-E2B-IT-onnx",
        "known_model": "KnownModels.Gemma4E2BIT",
        "name": "Gemma-4-E2B-IT",
        "params": "2.3B effective (5.1B total)",
        "architecture": "Dense with Per-Layer Embeddings (PLE)",
        "context": "128K",
        "min_ram_gb": 12,
        "min_disk_gb": 30,
        "recommended_inference_ram_gb": 6,
    },
    "e4b": {
        "source_hf": "google/gemma-4-E4B-it",
        "target_hf": "elbruno/Gemma-4-E4B-IT-onnx",
        "known_model": "KnownModels.Gemma4E4BIT",
        "name": "Gemma-4-E4B-IT",
        "params": "4.5B effective (8B total)",
        "architecture": "Dense with Per-Layer Embeddings (PLE)",
        "context": "128K",
        "min_ram_gb": 20,
        "min_disk_gb": 50,
        "recommended_inference_ram_gb": 10,
    },
    "12b": {
        "source_hf": "google/gemma-4-12B-it",
        "target_hf": "elbruno/Gemma-4-12B-IT-onnx",
        "known_model": "KnownModels.Gemma4_12BIT",
        "name": "Gemma-4-12B-IT",
        "params": "12B",
        "architecture": "Dense (Unified)",
        "context": "256K",
        "min_ram_gb": 32,
        "min_disk_gb": 90,
        "recommended_inference_ram_gb": 16,
    },
    "26b": {
        "source_hf": "google/gemma-4-26B-A4B-it",
        "target_hf": "elbruno/Gemma-4-26B-A4B-IT-onnx",
        "known_model": "KnownModels.Gemma4_26BA4BIT",
        "name": "Gemma-4-26B-A4B-IT",
        "params": "3.8B active / 25.2B total",
        "architecture": "Mixture of Experts (8 active / 128 total + 1 shared)",
        "context": "256K",
        "min_ram_gb": 64,
        "min_disk_gb": 150,
        "recommended_inference_ram_gb": 28,
    },
    "31b": {
        "source_hf": "google/gemma-4-31B-it",
        "target_hf": "elbruno/Gemma-4-31B-IT-onnx",
        "known_model": "KnownModels.Gemma4_31BIT",
        "name": "Gemma-4-31B-IT",
        "params": "30.7B",
        "architecture": "Dense",
        "context": "256K",
        "min_ram_gb": 80,
        "min_disk_gb": 180,
        "recommended_inference_ram_gb": 32,
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert Google Gemma 4 models to ONNX for ElBruno.LocalLLMs"
    )
    parser.add_argument(
        "--model-size",
        required=True,
        choices=list(GEMMA4_MODELS.keys()),
        help="Gemma 4 model size to convert",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        help="Output directory for converted ONNX model files",
    )
    parser.add_argument(
        "--quantize",
        choices=["int4", "int8", "fp16"],
        default="int4",
        help="Quantization level (default: int4)",
    )
    parser.add_argument(
        "--precision",
        choices=["int4", "int8", "fp16", "fp32"],
        default=None,
        help="Precision for model builder (overrides --quantize if both specified)",
    )
    parser.add_argument(
        "--cache-dir",
        default=None,
        help="Optional Hugging Face cache directory for model downloads",
    )
    parser.add_argument(
        "--skip-validation",
        action="store_true",
        help="Skip output file validation (not recommended)",
    )
    parser.add_argument(
        "--skip-upload",
        action="store_true",
        help="Convert only; do not upload to Hugging Face",
    )
    parser.add_argument(
        "--skip-conversion",
        action="store_true",
        help="Skip conversion and only validate/upload an existing output directory",
    )
    parser.add_argument(
        "--private",
        action="store_true",
        help="Create the target Hugging Face repo as private instead of public",
    )
    return parser.parse_args()


def check_dependencies() -> None:
    print("Checking dependencies...")
    missing: list[str] = []
    ort_version: str | None = None

    for module_name in ("onnxruntime_genai", "transformers", "torch", "huggingface_hub"):
        try:
            module = __import__(module_name)
            if module_name == "onnxruntime_genai":
                ort_version = getattr(module, "__version__", "unknown")
                print(f"  OK {module_name} {ort_version}")
            else:
                print(f"  OK {module_name}")
        except ImportError:
            missing.append(module_name)
            print(f"  MISSING {module_name}")

    if missing:
        print(f"\nERROR: Missing dependencies: {', '.join(missing)}")
        print("Install them with:")
        print("  pip install onnxruntime-genai huggingface-hub transformers torch")
        sys.exit(1)

    if ort_version is not None and tuple(int(part) for part in ort_version.split(".")[:3]) < (0, 15, 1):
        print(f"\nERROR: onnxruntime_genai {ort_version} is too old for the repo's Gemma 4 flow.")
        print("Upgrade Python onnxruntime-genai to 0.15.1+ before attempting Gemma 4 conversion.")
        sys.exit(1)

    print("All required dependencies are installed.\n")


def check_disk_space(output_dir: Path, required_gb: int) -> None:
    output_dir.parent.mkdir(parents=True, exist_ok=True)
    stat = shutil.disk_usage(output_dir.parent)
    available_gb = stat.free / (1024 ** 3)

    print("Disk space check:")
    print(f"  Required : ~{required_gb} GB")
    print(f"  Available: {available_gb:.1f} GB")

    if available_gb < required_gb:
        print("\nWARNING: Available disk space is below the recommended threshold.")
        print("Conversion may fail if temporary files exceed the estimate.")
    else:
        print("  OK sufficient disk space\n")


def check_ram(required_gb: int) -> None:
    try:
        import psutil
    except ImportError:
        print("RAM check skipped (install psutil for best-effort RAM checks).\n")
        return

    available_gb = psutil.virtual_memory().available / (1024 ** 3)
    total_gb = psutil.virtual_memory().total / (1024 ** 3)

    print("RAM check:")
    print(f"  Required : ~{required_gb} GB")
    print(f"  Total    : {total_gb:.1f} GB")
    print(f"  Available: {available_gb:.1f} GB")

    if available_gb < required_gb * 0.8:
        print("\nWARNING: Available RAM is below the recommended threshold.")
        print("The machine may swap heavily or fail during conversion.\n")
    else:
        print("  OK sufficient available RAM\n")


def check_hf_auth() -> str:
    token = os.getenv("HF_TOKEN")
    if not token:
        print("ERROR: HF_TOKEN is not set. Upload requires a Hugging Face token.")
        sys.exit(1)

    try:
        from huggingface_hub import HfApi
        info = HfApi(token=token).whoami()
    except Exception as exc:
        print(f"ERROR: Hugging Face authentication failed: {exc}")
        sys.exit(1)

    username = info.get("name") or info.get("fullname") or "unknown-user"
    print(f"Hugging Face authentication OK: {username}\n")
    return token


def run_preflight(output_dir: Path, model_size: str, skip_upload: bool) -> str | None:
    model_info = GEMMA4_MODELS[model_size]
    check_dependencies()
    check_ram(model_info["min_ram_gb"])
    check_disk_space(output_dir, model_info["min_disk_gb"])
    if skip_upload:
        return None
    return check_hf_auth()


def convert_model(model_size: str, output_dir: Path, precision: str, cache_dir: Path | None) -> None:
    model_info = GEMMA4_MODELS[model_size]

    print("Starting conversion...")
    print(f"  Source model : {model_info['source_hf']}")
    print(f"  Output dir   : {output_dir}")
    print(f"  Precision    : {precision}")
    if cache_dir is not None:
        print(f"  Cache dir    : {cache_dir}")
    print()

    output_dir.mkdir(parents=True, exist_ok=True)
    if cache_dir is not None:
        cache_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        sys.executable,
        "-m",
        "onnxruntime_genai.models.builder",
        "-m", model_info["source_hf"],
        "-o", str(output_dir),
        "-p", precision,
        "-e", "cpu",
        "--extra_options", "trust_remote_code=True",
    ]

    if cache_dir is not None:
        cmd.extend(["--cache_dir", str(cache_dir)])

    print("Running command:")
    print("  " + " ".join(cmd))
    print()

    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"ERROR: Conversion failed with exit code {result.returncode}.")
        sys.exit(result.returncode)

    print("Conversion completed successfully.\n")


def validate_output(output_dir: Path) -> None:
    print("Validating output...")
    errors: list[str] = []

    for filename in REQUIRED_OUTPUT_FILES:
        path = output_dir / filename
        if path.exists():
            size_mb = path.stat().st_size / (1024 ** 2)
            print(f"  OK {filename} ({size_mb:.1f} MB)")
        else:
            print(f"  MISSING {filename}")
            errors.append(filename)

    model_files = sorted(output_dir.glob("*.onnx"))
    data_files = sorted(output_dir.glob("*.onnx.data"))

    if model_files:
        for path in model_files:
            size_mb = path.stat().st_size / (1024 ** 2)
            print(f"  OK {path.name} ({size_mb:.1f} MB)")
    else:
        print("  MISSING ONNX model files")
        errors.append("*.onnx")

    for path in data_files:
        size_gb = path.stat().st_size / (1024 ** 3)
        print(f"  OK {path.name} ({size_gb:.2f} GB)")

    if errors:
        print(f"\nERROR: Validation failed. Missing: {', '.join(errors)}")
        sys.exit(1)

    print("Output validation passed.\n")


def build_model_card(model_size: str, precision: str) -> str:
    model_info = GEMMA4_MODELS[model_size]
    return f"""# {model_info['name']} ONNX

Public ONNX Runtime GenAI export of `{model_info['source_hf']}` for use with **ElBruno.LocalLLMs**.

## Details

- Source model: `{model_info['source_hf']}`
- Conversion pipeline: `python scripts/convert_gemma4.py --model-size {model_size} --precision {precision}`
- Precision: `{precision}`
- Architecture: {model_info['architecture']}
- Parameters: {model_info['params']}
- Context length: {model_info['context']}
- Recommended inference RAM: ~{model_info['recommended_inference_ram_gb']} GB

## Usage

```csharp
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{{
    Model = {model_info['known_model']},
    EnsureModelDownloaded = true
}});
```

> Note: this package is intended for ONNX Runtime GenAI / ElBruno.LocalLLMs scenarios.
"""


def upload_to_huggingface(output_dir: Path, model_size: str, precision: str, token: str, private: bool) -> None:
    model_info = GEMMA4_MODELS[model_size]
    target_repo = model_info["target_hf"]

    print(f"Preparing Hugging Face upload to {target_repo}...")

    readme_path = output_dir / "README.md"
    readme_path.write_text(build_model_card(model_size, precision), encoding="utf-8")
    print("  README.md written")

    try:
        from huggingface_hub import HfApi, create_repo
    except ImportError:
        print("ERROR: huggingface-hub is not installed.")
        sys.exit(1)

    api = HfApi(token=token)

    create_repo(
        repo_id=target_repo,
        repo_type="model",
        exist_ok=True,
        private=private,
        token=token,
    )
    print(f"  Repo ready ({'private' if private else 'public'})")

    api.upload_folder(
        folder_path=str(output_dir),
        repo_id=target_repo,
        repo_type="model",
        commit_message=f"Add {model_info['name']} ONNX export ({precision})",
    )

    print(f"Upload complete: https://huggingface.co/{target_repo}\n")


def print_usage_instructions(output_dir: Path, model_size: str) -> None:
    model_info = GEMMA4_MODELS[model_size]

    print("=" * 72)
    print(f"DONE: {model_info['name']}")
    print("=" * 72)
    print(f"Output: {output_dir}")
    print()
    print("C# usage:")
    print(f"""
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{{
    ModelPath = @\"{output_dir}\",
    MaxTokens = 1024,
    Temperature = 0.7f
}});
""")
    print()


def main() -> None:
    args = parse_args()
    precision = args.precision or args.quantize
    output_dir = Path(args.output_dir).resolve()
    cache_dir = Path(args.cache_dir).resolve() if args.cache_dir else None

    print("=" * 72)
    print("Gemma 4 ONNX Conversion")
    print("=" * 72)
    print()

    token = run_preflight(output_dir, args.model_size, args.skip_upload)

    if not args.skip_conversion:
        convert_model(args.model_size, output_dir, precision, cache_dir)
    else:
        print("Skipping conversion as requested (--skip-conversion).\n")

    if not args.skip_validation:
        validate_output(output_dir)
    else:
        print("Skipping output validation as requested (--skip-validation).\n")

    if not args.skip_upload:
        upload_to_huggingface(output_dir, args.model_size, precision, token, args.private)
    else:
        print("Skipping Hugging Face upload as requested (--skip-upload).\n")

    print_usage_instructions(output_dir, args.model_size)


if __name__ == "__main__":
    main()
