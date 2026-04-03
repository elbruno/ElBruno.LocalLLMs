#!/usr/bin/env python3
"""
Convert Google Gemma 4 models to ONNX format for ElBruno.LocalLLMs.

Gemma 4 includes four model sizes:
- E2B: 2.3B effective (5.1B total) with Per-Layer Embeddings (PLE)
- E4B: 4.5B effective (8B total) with Per-Layer Embeddings (PLE)
- 26B: 3.8B active / 25.2B total, Mixture of Experts (MoE)
- 31B: 30.7B dense

Usage:
    python convert_gemma4.py --model-size e2b --output-dir ./models/gemma4-e2b
    python convert_gemma4.py --model-size 26b --output-dir ./models/gemma4-26b --quantize int8

Requirements:
    pip install onnxruntime-genai huggingface-hub transformers torch
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path


# HuggingFace model IDs for each Gemma 4 size
GEMMA4_MODELS = {
    "e2b": {
        "hf_id": "google/gemma-4-E2B-it",
        "name": "Gemma 4 E2B IT",
        "params": "2.3B effective (5.1B total)",
        "architecture": "Dense with Per-Layer Embeddings (PLE)",
        "min_ram_gb": 12,
        "min_disk_gb": 30,
    },
    "e4b": {
        "hf_id": "google/gemma-4-E4B-it",
        "name": "Gemma 4 E4B IT",
        "params": "4.5B effective (8B total)",
        "architecture": "Dense with Per-Layer Embeddings (PLE)",
        "min_ram_gb": 20,
        "min_disk_gb": 50,
    },
    "26b": {
        "hf_id": "google/gemma-4-26B-A4B-it",
        "name": "Gemma 4 26B A4B IT",
        "params": "3.8B active / 25.2B total",
        "architecture": "Mixture of Experts (MoE, 8 active / 128 total + 1 shared)",
        "min_ram_gb": 64,
        "min_disk_gb": 150,
    },
    "31b": {
        "hf_id": "google/gemma-4-31B-it",
        "name": "Gemma 4 31B IT",
        "params": "30.7B",
        "architecture": "Dense",
        "min_ram_gb": 80,
        "min_disk_gb": 180,
    },
}


def parse_args():
    parser = argparse.ArgumentParser(
        description="Convert Google Gemma 4 models to ONNX for ElBruno.LocalLLMs"
    )
    parser.add_argument(
        "--model-size",
        required=True,
        choices=["e2b", "e4b", "26b", "31b"],
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
        "--skip-validation",
        action="store_true",
        help="Skip output file validation (not recommended)",
    )
    return parser.parse_args()


def check_dependencies():
    """Verify required Python packages and tools are installed."""
    print("🔍 Checking dependencies...")
    missing = []
    
    # Check Python packages
    try:
        import onnxruntime_genai  # noqa: F401
        print("  ✓ onnxruntime-genai")
    except ImportError:
        missing.append("onnxruntime-genai")
        print("  ✗ onnxruntime-genai (missing)")
    
    try:
        import transformers  # noqa: F401
        print("  ✓ transformers")
    except ImportError:
        missing.append("transformers")
        print("  ✗ transformers (missing)")
    
    try:
        import torch  # noqa: F401
        print("  ✓ torch")
    except ImportError:
        missing.append("torch")
        print("  ✗ torch (missing)")
    
    try:
        import huggingface_hub  # noqa: F401
        print("  ✓ huggingface-hub")
    except ImportError:
        missing.append("huggingface-hub")
        print("  ✗ huggingface-hub (missing)")

    if missing:
        print(f"\n❌ ERROR: Missing dependencies: {', '.join(missing)}")
        print("Install them with:")
        print("  pip install onnxruntime-genai huggingface-hub transformers torch")
        sys.exit(1)
    
    print("✅ All dependencies installed\n")


def check_disk_space(output_dir: str, required_gb: int):
    """Check if sufficient disk space is available."""
    output_path = Path(output_dir).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    stat = shutil.disk_usage(output_path.parent)
    available_gb = stat.free / (1024 ** 3)
    
    print(f"💾 Disk space check:")
    print(f"  Required: ~{required_gb} GB")
    print(f"  Available: {available_gb:.1f} GB")
    
    if available_gb < required_gb:
        print(f"\n⚠️  WARNING: Low disk space!")
        print(f"  You may run out of space during conversion.")
        response = input("  Continue anyway? [y/N]: ")
        if response.lower() != 'y':
            print("Aborted.")
            sys.exit(0)
    else:
        print("  ✓ Sufficient disk space\n")


def check_ram(required_gb: int):
    """Check if sufficient RAM is available (best-effort)."""
    try:
        import psutil
        available_gb = psutil.virtual_memory().available / (1024 ** 3)
        total_gb = psutil.virtual_memory().total / (1024 ** 3)
        
        print(f"🧠 RAM check:")
        print(f"  Required: ~{required_gb} GB")
        print(f"  Total: {total_gb:.1f} GB")
        print(f"  Available: {available_gb:.1f} GB")
        
        if available_gb < required_gb * 0.8:  # Allow 20% margin
            print(f"\n⚠️  WARNING: Low RAM!")
            print(f"  Conversion may fail or swap heavily.")
            response = input("  Continue anyway? [y/N]: ")
            if response.lower() != 'y':
                print("Aborted.")
                sys.exit(0)
        else:
            print("  ✓ Sufficient RAM\n")
    except ImportError:
        print("🧠 RAM check: (install psutil for RAM checks)\n")


def convert_model(model_size: str, output_dir: str, precision: str):
    """
    Convert Gemma 4 model using onnxruntime_genai.models.builder.
    
    This generates genai_config.json and proper tokenizer setup for C# compatibility.
    """
    model_info = GEMMA4_MODELS[model_size]
    hf_id = model_info["hf_id"]
    
    print(f"🚀 Converting {model_info['name']}")
    print(f"   HuggingFace: {hf_id}")
    print(f"   Architecture: {model_info['architecture']}")
    print(f"   Parameters: {model_info['params']}")
    print(f"   Precision: {precision}")
    print(f"   Output: {output_dir}\n")
    
    # Build the command for onnxruntime_genai model builder
    cmd = [
        sys.executable,
        "-m",
        "onnxruntime_genai.models.builder",
        "-m", hf_id,
        "-o", output_dir,
        "-p", precision,
        "-e", "cpu",  # Target CPU execution provider
        "--extra_options", "trust_remote_code=True",  # Required for Gemma 4
    ]
    
    print(f"📋 Running command:")
    print(f"   {' '.join(cmd)}\n")
    
    try:
        # Run the conversion
        result = subprocess.run(
            cmd,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )
        print(result.stdout)
        print(f"\n✅ Conversion complete!")
        
    except subprocess.CalledProcessError as e:
        print(f"\n❌ Conversion failed!")
        print(f"Exit code: {e.returncode}")
        print(f"Output:\n{e.stdout}")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Unexpected error during conversion:")
        print(f"{type(e).__name__}: {e}")
        sys.exit(1)


def validate_output(output_dir: str, model_size: str):
    """Validate that the conversion produced the required files."""
    print(f"\n🔍 Validating output files...")
    
    output_path = Path(output_dir)
    required_files = [
        "genai_config.json",
        "tokenizer_config.json",
    ]
    
    # Check for at least one model file
    model_files = list(output_path.glob("*.onnx")) + list(output_path.glob("*.onnx.data"))
    
    errors = []
    
    for filename in required_files:
        filepath = output_path / filename
        if filepath.exists():
            print(f"  ✓ {filename}")
        else:
            print(f"  ✗ {filename} (missing)")
            errors.append(filename)
    
    if model_files:
        print(f"  ✓ Model files ({len(model_files)} found)")
        for mf in model_files[:3]:  # Show first 3
            print(f"    - {mf.name}")
        if len(model_files) > 3:
            print(f"    ... and {len(model_files) - 3} more")
    else:
        print(f"  ✗ No .onnx model files found")
        errors.append("model.onnx")
    
    if errors:
        print(f"\n⚠️  WARNING: Missing required files: {', '.join(errors)}")
        print(f"The converted model may not work correctly with ElBruno.LocalLLMs.")
        return False
    else:
        print(f"\n✅ All required files present!")
        return True


def print_usage_instructions(output_dir: str, model_size: str):
    """Print instructions for using the converted model."""
    model_info = GEMMA4_MODELS[model_size]
    
    print(f"\n{'=' * 70}")
    print(f"✅ CONVERSION COMPLETE: {model_info['name']}")
    print(f"{'=' * 70}")
    print(f"\n📂 Model files: {output_dir}")
    print(f"\n📖 Usage in C#:")
    print(f"""
using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{{
    ModelPath = @"{output_dir}",
    MaxTokens = 1024,
    Temperature = 0.7f
}});

var response = await client.CompleteAsync("Hello, how are you?");
Console.WriteLine(response);
""")
    print(f"\n💡 Tips:")
    print(f"  • Context length: {model_info.get('context', '128K')} tokens")
    print(f"  • Architecture: {model_info['architecture']}")
    if model_size == "26b":
        print(f"  • This is a MoE model - only ~3.8B params are active during inference")
    print(f"  • Recommended RAM: {model_info['min_ram_gb']}+ GB for inference")
    print(f"\n{'=' * 70}\n")


def main():
    args = parse_args()
    
    # Determine precision (--precision overrides --quantize)
    if args.precision:
        precision = args.precision
    else:
        precision = args.quantize
    
    model_info = GEMMA4_MODELS[args.model_size]
    
    print("=" * 70)
    print("🤖 Gemma 4 ONNX Conversion")
    print("=" * 70)
    print()
    
    # Pre-flight checks
    check_dependencies()
    check_ram(model_info["min_ram_gb"])
    check_disk_space(args.output_dir, model_info["min_disk_gb"])
    
    # Create output directory
    output_path = Path(args.output_dir)
    output_path.mkdir(parents=True, exist_ok=True)
    
    # Convert the model
    convert_model(args.model_size, args.output_dir, precision)
    
    # Validate output
    if not args.skip_validation:
        validate_output(args.output_dir, args.model_size)
    
    # Print usage instructions
    print_usage_instructions(args.output_dir, args.model_size)


if __name__ == "__main__":
    main()
