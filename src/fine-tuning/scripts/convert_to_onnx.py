#!/usr/bin/env python3
"""
Convert a merged HuggingFace Qwen2.5 model to ONNX format using
onnxruntime-genai model builder, with INT4 quantization by default.

The output structure is ready for direct use with ElBruno.LocalLLMs:
    model.onnx, model.onnx.data, genai_config.json, tokenizer files.

Usage:
    # Default INT4 for CPU
    python convert_to_onnx.py \
        --input-dir ./output/qwen25-05b-merged \
        --output-dir ./output/qwen25-05b-onnx-int4

    # INT8 quantization
    python convert_to_onnx.py \
        --input-dir ./output/qwen25-05b-merged \
        --output-dir ./output/qwen25-05b-onnx-int8 \
        --precision int8

    # FP16 (no quantization)
    python convert_to_onnx.py \
        --input-dir ./output/qwen25-05b-merged \
        --output-dir ./output/qwen25-05b-onnx-fp16 \
        --precision fp16

    # For large models (14B+), use CUDA execution provider
    python convert_to_onnx.py \
        --input-dir ./output/qwen25-3b-merged \
        --output-dir ./output/qwen25-3b-onnx-int4 \
        --execution-provider cuda
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import shutil
import subprocess
import sys
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# Expected output files from onnxruntime_genai model builder
EXPECTED_FILES = [
    "model.onnx",
    "genai_config.json",
    "tokenizer.json",
    "tokenizer_config.json",
    "special_tokens_map.json",
]

# Files that are expected but may vary by model
OPTIONAL_FILES = [
    "model.onnx.data",    # Large weight shard (present for all real models)
    "merges.txt",          # BPE merges (model-dependent)
    "vocab.json",          # BPE vocab (model-dependent)
    "added_tokens.json",   # Extra tokens
    "chat_template.jinja", # Jinja chat template
]


def _check_dependencies() -> None:
    """Verify onnxruntime-genai is installed and the builder module is available."""
    try:
        import onnxruntime_genai  # noqa: F401
    except ImportError:
        log.error(
            "onnxruntime-genai is not installed.\n"
            "Install with: pip install onnxruntime-genai\n"
            "For CUDA support: pip install onnxruntime-genai-cuda"
        )
        sys.exit(1)

    # Check that the builder module exists
    result = subprocess.run(
        [sys.executable, "-m", "onnxruntime_genai.models.builder", "--help"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        log.error(
            "onnxruntime_genai.models.builder is not available.\n"
            "Ensure onnxruntime-genai >= 0.8 is installed."
        )
        sys.exit(1)
    log.info("onnxruntime-genai model builder is available.")


def _validate_input_dir(input_dir: Path) -> None:
    """Ensure the input directory contains a valid HuggingFace checkpoint."""
    if not input_dir.is_dir():
        log.error("Input directory does not exist: %s", input_dir)
        sys.exit(1)

    config_file = input_dir / "config.json"
    if not config_file.exists():
        log.error(
            "No config.json found in %s — is this a HuggingFace model directory?",
            input_dir,
        )
        sys.exit(1)

    # Check that tokenizer files exist
    tokenizer_file = input_dir / "tokenizer_config.json"
    if not tokenizer_file.exists():
        log.warning(
            "No tokenizer_config.json found in %s. "
            "The builder may fall back to downloading the tokenizer.",
            input_dir,
        )

    # Read model config and report
    with open(config_file) as f:
        config = json.load(f)
    arch = config.get("architectures", ["unknown"])[0]
    vocab_size = config.get("vocab_size", "unknown")
    hidden = config.get("hidden_size", "unknown")
    layers = config.get("num_hidden_layers", "unknown")
    log.info(
        "Input model: arch=%s, vocab=%s, hidden=%s, layers=%s",
        arch, vocab_size, hidden, layers,
    )


def _validate_tokenizer_compat(input_dir: Path) -> None:
    """
    Check that the tokenizer is compatible with QwenFormatter expectations.
    Specifically, verify that ChatML special tokens are present.
    """
    tokenizer_config = input_dir / "tokenizer_config.json"
    if not tokenizer_config.exists():
        log.warning("Cannot validate tokenizer — tokenizer_config.json not found.")
        return

    with open(tokenizer_config) as f:
        config = json.load(f)

    # Check for ChatML tokens in added_tokens or chat_template
    chat_template = config.get("chat_template", "")
    chatml_tokens = ["<|im_start|>", "<|im_end|>"]
    missing = [t for t in chatml_tokens if t not in str(config) and t not in chat_template]

    if missing:
        log.warning(
            "ChatML tokens %s not found in tokenizer config. "
            "QwenFormatter expects these tokens. The model may not produce "
            "correct chat formatting.",
            missing,
        )
    else:
        log.info("✅ ChatML tokens (<|im_start|>, <|im_end|>) found in tokenizer config.")

    # Check for tool_call support in chat template
    if "<tool_call>" in chat_template:
        log.info("✅ Chat template includes <tool_call> support.")
    else:
        log.info(
            "ℹ️  Chat template does not explicitly reference <tool_call>. "
            "Tool calling behavior depends on fine-tuning data."
        )


def build_conversion_command(
    input_dir: str,
    output_dir: str,
    precision: str,
    execution_provider: str,
    int4_accuracy_level: int,
    extra_options: list[str] | None,
) -> list[str]:
    """Build the onnxruntime_genai model builder command."""
    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-m", input_dir,
        "-o", output_dir,
        "-p", precision,
        "-e", execution_provider,
    ]

    # INT4-specific: set accuracy level (4 = highest quality)
    if precision == "int4":
        cmd.extend(["--extra_options", f"int4_accuracy_level={int4_accuracy_level}"])

    # Append any additional user-specified extra_options
    if extra_options:
        for opt in extra_options:
            cmd.extend(["--extra_options", opt])

    return cmd


def run_conversion(cmd: list[str]) -> None:
    """Execute the model builder subprocess with real-time output."""
    log.info("Running: %s", " ".join(cmd))
    log.info("This may take several minutes depending on model size...")

    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )

    assert process.stdout is not None
    for line in process.stdout:
        stripped = line.rstrip()
        if stripped:
            log.info("  [builder] %s", stripped)

    returncode = process.wait()
    if returncode != 0:
        log.error("Model builder failed with exit code %d", returncode)
        sys.exit(returncode)

    log.info("Model builder completed successfully.")


def validate_output(output_dir: Path) -> None:
    """Verify the output directory contains all expected files."""
    log.info("Validating output directory: %s", output_dir)
    missing = []
    for fname in EXPECTED_FILES:
        if not (output_dir / fname).exists():
            missing.append(fname)

    if missing:
        log.error("Missing expected output files: %s", missing)
        sys.exit(1)

    # Report output contents and sizes
    total_bytes = 0
    for fpath in sorted(output_dir.iterdir()):
        if fpath.is_file():
            size = fpath.stat().st_size
            total_bytes += size
            size_mb = size / (1024 * 1024)
            log.info("  • %-30s  %8.1f MB", fpath.name, size_mb)

    total_mb = total_bytes / (1024 * 1024)
    log.info("Total output size: %.1f MB", total_mb)

    # Validate genai_config.json
    genai_config = output_dir / "genai_config.json"
    with open(genai_config) as f:
        config = json.load(f)

    model_type = config.get("model", {}).get("type", "unknown")
    log.info("genai_config model type: %s", model_type)

    if "decoder" not in config.get("model", {}):
        log.warning("genai_config.json missing 'decoder' section — may not work with GenAI runtime.")
    else:
        log.info("✅ genai_config.json looks valid.")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Convert a merged HuggingFace model to ONNX format using "
            "onnxruntime-genai model builder."
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  # INT4 (default, smallest, recommended)\n"
            "  python convert_to_onnx.py \\\n"
            "      --input-dir ./output/qwen25-05b-merged \\\n"
            "      --output-dir ./output/qwen25-05b-onnx-int4\n\n"
            "  # INT8 (better quality, larger)\n"
            "  python convert_to_onnx.py \\\n"
            "      --input-dir ./output/qwen25-05b-merged \\\n"
            "      --output-dir ./output/qwen25-05b-onnx-int8 \\\n"
            "      --precision int8\n\n"
            "  # FP16 (full precision, largest)\n"
            "  python convert_to_onnx.py \\\n"
            "      --input-dir ./output/qwen25-05b-merged \\\n"
            "      --output-dir ./output/qwen25-05b-onnx-fp16 \\\n"
            "      --precision fp16\n\n"
            "  # Large model with CUDA EP (uses streaming quantization)\n"
            "  python convert_to_onnx.py \\\n"
            "      --input-dir ./output/qwen25-3b-merged \\\n"
            "      --output-dir ./output/qwen25-3b-onnx-int4 \\\n"
            "      --execution-provider cuda"
        ),
    )
    parser.add_argument(
        "--input-dir",
        required=True,
        help="Path to the merged HuggingFace model directory "
             "(output of merge_lora.py or a HuggingFace checkpoint).",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        help="Directory where ONNX model files will be written.",
    )
    parser.add_argument(
        "--precision",
        default="int4",
        choices=["int4", "int8", "fp16", "fp32"],
        help="Quantization precision (default: int4). "
             "INT4 gives smallest size with minimal quality loss.",
    )
    parser.add_argument(
        "--execution-provider",
        default="cpu",
        choices=["cpu", "cuda"],
        help="Execution provider for the builder (default: cpu). "
             "Use 'cuda' for 14B+ models to avoid OOM during quantization.",
    )
    parser.add_argument(
        "--int4-accuracy-level",
        type=int,
        default=4,
        choices=[0, 1, 2, 3, 4],
        help="INT4 quantization accuracy level 0-4 (default: 4, highest quality). "
             "Only used when --precision is int4.",
    )
    parser.add_argument(
        "--extra-options",
        nargs="*",
        default=None,
        help="Additional key=value options to pass to the builder via --extra_options.",
    )
    parser.add_argument(
        "--skip-validation",
        action="store_true",
        help="Skip output validation checks.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    _check_dependencies()

    input_dir = Path(args.input_dir).resolve()
    output_dir = Path(args.output_dir).resolve()

    _validate_input_dir(input_dir)
    _validate_tokenizer_compat(input_dir)

    # Create output directory
    output_dir.mkdir(parents=True, exist_ok=True)

    # Build and run the conversion command
    cmd = build_conversion_command(
        input_dir=str(input_dir),
        output_dir=str(output_dir),
        precision=args.precision,
        execution_provider=args.execution_provider,
        int4_accuracy_level=args.int4_accuracy_level,
        extra_options=args.extra_options,
    )
    run_conversion(cmd)

    # Validate output
    if not args.skip_validation:
        validate_output(output_dir)

    log.info("✅ ONNX conversion complete: %s", output_dir)
    log.info(
        "Next step: python validate_onnx.py --model-dir %s",
        output_dir,
    )


if __name__ == "__main__":
    main()
