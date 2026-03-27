#!/usr/bin/env python3
"""
Merge LoRA adapters into a base Qwen2.5 model to produce a full-precision
HuggingFace checkpoint ready for ONNX conversion.

Supports Qwen2.5-0.5B, 1.5B, and 3B Instruct variants.

Usage:
    python merge_lora.py \
        --base-model Qwen/Qwen2.5-0.5B-Instruct \
        --adapter-path ./output/qwen25-05b-finetuned \
        --output-dir  ./output/qwen25-05b-merged

    # Quick smoke test after merge
    python merge_lora.py \
        --base-model Qwen/Qwen2.5-0.5B-Instruct \
        --adapter-path ./output/qwen25-05b-finetuned \
        --output-dir  ./output/qwen25-05b-merged \
        --verify
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import shutil
import sys
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

VERIFY_PROMPT = (
    "<|im_start|>system\n"
    "You are a helpful assistant with access to the following tools:\n\n"
    '[{"type":"function","function":{"name":"get_weather","description":"Get current weather for a city",'
    '"parameters":{"type":"object","properties":{"city":{"type":"string"}}}}}]\n\n'
    "When you need to call a tool, respond with a JSON object in this format:\n"
    '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
    "<|im_end|>\n"
    "<|im_start|>user\nWhat is the weather in Tokyo?<|im_end|>\n"
    "<|im_start|>assistant\n"
)


def _check_dependencies() -> None:
    """Verify required packages are importable."""
    missing: list[str] = []
    for pkg in ("torch", "transformers", "peft"):
        try:
            __import__(pkg)
        except ImportError:
            missing.append(pkg)
    if missing:
        log.error("Missing dependencies: %s", ", ".join(missing))
        log.error("Install with: pip install torch transformers peft accelerate")
        sys.exit(1)


def _verify_merged_model(model_dir: str, device: str = "cpu") -> None:
    """Generate a short completion to confirm the merged model works."""
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer

    log.info("Running verification on merged model at %s ...", model_dir)
    tokenizer = AutoTokenizer.from_pretrained(model_dir, trust_remote_code=True)
    model = AutoModelForCausalLM.from_pretrained(
        model_dir,
        torch_dtype=torch.float16,
        device_map=device,
        trust_remote_code=True,
    )
    model.eval()

    input_ids = tokenizer(VERIFY_PROMPT, return_tensors="pt").input_ids.to(device)
    with torch.no_grad():
        output_ids = model.generate(
            input_ids,
            max_new_tokens=150,
            do_sample=False,
            temperature=1.0,
        )
    generated = tokenizer.decode(output_ids[0][input_ids.shape[1]:], skip_special_tokens=False)
    log.info("--- Verification output ---\n%s\n--- end ---", generated)

    # Basic sanity checks
    if "<tool_call>" in generated or "get_weather" in generated:
        log.info("✅ Merged model produces tool-calling output.")
    else:
        log.warning(
            "⚠️  Merged model did NOT produce <tool_call> tags. "
            "This may be expected for non-tool-calling fine-tunes, "
            "but warrants manual inspection."
        )


# ---------------------------------------------------------------------------
# Core merge logic
# ---------------------------------------------------------------------------

def merge_lora(
    base_model: str,
    adapter_path: str,
    output_dir: str,
    device: str = "cpu",
) -> None:
    """
    Load a base HuggingFace model, apply LoRA adapter weights, merge them
    into a single dense checkpoint, and save in HuggingFace format.
    """
    import torch
    from peft import PeftModel
    from transformers import AutoModelForCausalLM, AutoTokenizer

    output = Path(output_dir)
    output.mkdir(parents=True, exist_ok=True)

    # ── 1. Load tokenizer ──────────────────────────────────────────────────
    log.info("Loading tokenizer from adapter path: %s", adapter_path)
    # Prefer the tokenizer saved with the adapter (it may have extra tokens).
    # Fall back to the base model tokenizer if the adapter dir doesn't have one.
    adapter_tokenizer_path = Path(adapter_path)
    if (adapter_tokenizer_path / "tokenizer_config.json").exists():
        tokenizer = AutoTokenizer.from_pretrained(adapter_path, trust_remote_code=True)
        log.info("Loaded tokenizer from adapter directory.")
    else:
        tokenizer = AutoTokenizer.from_pretrained(base_model, trust_remote_code=True)
        log.info("Loaded tokenizer from base model (adapter dir has no tokenizer).")

    # ── 2. Load base model in full precision ───────────────────────────────
    log.info("Loading base model: %s (this may download ~1-6 GB) ...", base_model)
    base = AutoModelForCausalLM.from_pretrained(
        base_model,
        torch_dtype=torch.float16,
        device_map=device,
        trust_remote_code=True,
    )
    log.info("Base model loaded (%s parameters).", f"{base.num_parameters():,}")

    # ── 3. Resize embeddings if adapter added special tokens ───────────────
    if len(tokenizer) != base.config.vocab_size:
        log.info(
            "Resizing embeddings from %d to %d to match tokenizer.",
            base.config.vocab_size,
            len(tokenizer),
        )
        base.resize_token_embeddings(len(tokenizer))

    # ── 4. Load LoRA adapter ──────────────────────────────────────────────
    log.info("Loading LoRA adapter from: %s", adapter_path)
    model = PeftModel.from_pretrained(base, adapter_path)
    log.info("LoRA adapter loaded.")

    # ── 5. Merge adapter weights into base model ──────────────────────────
    log.info("Merging adapter weights into base model ...")
    merged = model.merge_and_unload()
    log.info("Merge complete.")

    # ── 6. Save merged model ──────────────────────────────────────────────
    log.info("Saving merged model to: %s", output_dir)
    merged.save_pretrained(output_dir, safe_serialization=True)
    tokenizer.save_pretrained(output_dir)

    # Copy adapter_config for reference (not required, but useful metadata)
    adapter_cfg = Path(adapter_path) / "adapter_config.json"
    if adapter_cfg.exists():
        dest = output / "lora_adapter_config_reference.json"
        shutil.copy2(adapter_cfg, dest)
        log.info("Copied adapter config as reference → %s", dest.name)

    # ── 7. Report output ──────────────────────────────────────────────────
    total_bytes = sum(f.stat().st_size for f in output.rglob("*") if f.is_file())
    total_mb = total_bytes / (1024 * 1024)
    file_list = sorted(f.name for f in output.iterdir() if f.is_file())
    log.info("Output files (%s, %.1f MB total):", output_dir, total_mb)
    for name in file_list:
        log.info("  • %s", name)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Merge LoRA adapters into a base Qwen2.5 model.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python merge_lora.py \\\n"
            "      --base-model Qwen/Qwen2.5-0.5B-Instruct \\\n"
            "      --adapter-path ./output/qwen25-05b-finetuned \\\n"
            "      --output-dir  ./output/qwen25-05b-merged\n"
            "\n"
            "  python merge_lora.py \\\n"
            "      --base-model Qwen/Qwen2.5-1.5B-Instruct \\\n"
            "      --adapter-path ./output/qwen25-15b-finetuned \\\n"
            "      --output-dir  ./output/qwen25-15b-merged \\\n"
            "      --verify --device cuda"
        ),
    )
    parser.add_argument(
        "--base-model",
        required=True,
        help="HuggingFace model ID or local path of the base model "
             "(e.g. Qwen/Qwen2.5-0.5B-Instruct).",
    )
    parser.add_argument(
        "--adapter-path",
        required=True,
        help="Path to the LoRA adapter directory (must contain adapter_model.safetensors "
             "and adapter_config.json).",
    )
    parser.add_argument(
        "--output-dir",
        required=True,
        help="Directory where the merged HuggingFace model will be saved.",
    )
    parser.add_argument(
        "--device",
        default="cpu",
        choices=["cpu", "cuda", "auto"],
        help="Device to load the model on for merging (default: cpu).",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="Run a quick generation test after merging to verify the model works.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    _check_dependencies()

    # Validate adapter path
    adapter = Path(args.adapter_path)
    if not adapter.is_dir():
        log.error("Adapter path does not exist: %s", args.adapter_path)
        sys.exit(1)
    if not (adapter / "adapter_config.json").exists():
        log.error("No adapter_config.json found in %s — is this a LoRA checkpoint?", args.adapter_path)
        sys.exit(1)

    merge_lora(
        base_model=args.base_model,
        adapter_path=args.adapter_path,
        output_dir=args.output_dir,
        device=args.device,
    )

    if args.verify:
        _verify_merged_model(args.output_dir, device=args.device)

    log.info("✅ LoRA merge complete. Output: %s", args.output_dir)
    log.info("Next step: python convert_to_onnx.py --input-dir %s --output-dir <onnx-output>", args.output_dir)


if __name__ == "__main__":
    main()
