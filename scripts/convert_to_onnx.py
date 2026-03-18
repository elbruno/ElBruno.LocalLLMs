#!/usr/bin/env python3
"""
Convert HuggingFace models to ONNX format for use with ElBruno.LocalLLMs.

Usage:
    python convert_to_onnx.py --model-id <huggingface-repo-id> --output-dir <path>

Examples:
    python convert_to_onnx.py --model-id Qwen/Qwen2.5-0.5B-Instruct --output-dir ./models/qwen2.5-0.5b
    python convert_to_onnx.py --model-id meta-llama/Llama-3.2-3B-Instruct --output-dir ./models/llama-3.2-3b --quantize int4
"""

import argparse
import os
import sys
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(
        description="Convert HuggingFace models to ONNX for ElBruno.LocalLLMs"
    )
    parser.add_argument(
        "--model-id",
        required=True,
        help="HuggingFace model repository ID (e.g., Qwen/Qwen2.5-0.5B-Instruct)",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        help="Output directory for converted ONNX model files",
    )
    parser.add_argument(
        "--quantize",
        choices=["none", "int4", "int8"],
        default="int4",
        help="Quantization level (default: int4)",
    )
    parser.add_argument(
        "--task",
        default="text-generation-with-past",
        help="Model task for optimum export (default: text-generation-with-past)",
    )
    parser.add_argument(
        "--trust-remote-code",
        action="store_true",
        help="Trust remote code from HuggingFace (required for some models)",
    )
    return parser.parse_args()


def check_dependencies():
    """Verify required Python packages are installed."""
    missing = []
    try:
        import optimum  # noqa: F401
    except ImportError:
        missing.append("optimum[onnxruntime]")
    try:
        import onnxruntime  # noqa: F401
    except ImportError:
        missing.append("onnxruntime")
    try:
        import transformers  # noqa: F401
    except ImportError:
        missing.append("transformers")

    if missing:
        print(f"ERROR: Missing dependencies: {', '.join(missing)}")
        print("Install them with: pip install -r requirements.txt")
        sys.exit(1)


def convert_model(model_id: str, output_dir: str, task: str, trust_remote_code: bool):
    """Export model from HuggingFace to ONNX using optimum."""
    from optimum.exporters.onnx import main_export

    print(f"Exporting {model_id} to ONNX...")
    main_export(
        model_name_or_path=model_id,
        output=output_dir,
        task=task,
        trust_remote_code=trust_remote_code,
    )
    print(f"ONNX export complete: {output_dir}")


def quantize_model(output_dir: str, quantize: str):
    """Apply quantization to the exported ONNX model."""
    if quantize == "none":
        print("Skipping quantization.")
        return

    from optimum.onnxruntime import ORTQuantizer
    from optimum.onnxruntime.configuration import AutoQuantizationConfig

    model_path = Path(output_dir)
    onnx_files = list(model_path.glob("*.onnx"))

    if not onnx_files:
        print("WARNING: No ONNX files found for quantization.")
        return

    if quantize == "int4":
        qconfig = AutoQuantizationConfig.avx512_vnni(
            is_static=False, per_channel=False
        )
    elif quantize == "int8":
        qconfig = AutoQuantizationConfig.avx512_vnni(is_static=False)
    else:
        return

    quantized_dir = model_path / f"quantized-{quantize}"
    quantized_dir.mkdir(exist_ok=True)

    for onnx_file in onnx_files:
        print(f"Quantizing {onnx_file.name} to {quantize}...")
        quantizer = ORTQuantizer.from_pretrained(model_path, file_name=onnx_file.name)
        quantizer.quantize(save_dir=quantized_dir, quantization_config=qconfig)

    print(f"Quantized model saved to: {quantized_dir}")


def main():
    args = parse_args()
    check_dependencies()

    os.makedirs(args.output_dir, exist_ok=True)

    convert_model(args.model_id, args.output_dir, args.task, args.trust_remote_code)
    quantize_model(args.output_dir, args.quantize)

    print("\nConversion complete!")
    print(f"Model files: {args.output_dir}")
    print("Use this path as ModelPath in LocalLLMsOptions.")


if __name__ == "__main__":
    main()
