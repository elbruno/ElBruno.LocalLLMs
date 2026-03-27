#!/usr/bin/env python3
"""
Standalone CLI training script for ElBruno.LocalLLMs fine-tuned models.

Runs the full fine-tuning pipeline on any machine with a GPU:
  Load data → Format → Train (QLoRA) → Merge → ONNX INT4 → Validate → Upload

This script replicates the Colab notebook (train_and_publish.ipynb) as a
single command-line tool.

Usage:
    # Basic — train ToolCalling variant and upload
    python train.py --variant ToolCalling --hf-token hf_xxxx

    # Skip upload (local training only)
    python train.py --variant RAG --skip-upload

    # Custom epochs, output dir, skip ONNX
    python train.py --variant Instruct --epochs 5 --output-dir ./my-output --skip-onnx

    # Use HF_TOKEN env var instead of --hf-token
    export HF_TOKEN=hf_xxxx
    python train.py --variant ToolCalling
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import traceback
from pathlib import Path

# ── Constants ──────────────────────────────────────────────────────────────

BASE_MODEL = "unsloth/Qwen2.5-0.5B-Instruct"
BASE_MODEL_CLEAN = "Qwen/Qwen2.5-0.5B-Instruct"

DATA_FILES = {
    "ToolCalling": "tool-calling-train.json",
    "RAG": "rag-grounded-train.json",
    "Instruct": "combined-train.json",
}

CAPABILITY_LABELS = {
    "ToolCalling": "tool calling",
    "RAG": "RAG grounded answering",
    "Instruct": "instruction following",
}

# QLoRA hyperparameters (matching the notebook / train_qwen_05b.py)
MAX_SEQ_LENGTH = 2048
LORA_R = 16
LORA_ALPHA = 32
LORA_DROPOUT = 0.05
LEARNING_RATE = 2e-4
BATCH_SIZE = 4
GRAD_ACCUM = 4

TARGET_MODULES = [
    "q_proj", "k_proj", "v_proj", "o_proj",
    "gate_proj", "up_proj", "down_proj",
]


# ── Helpers ────────────────────────────────────────────────────────────────

def step(num: int, total: int, msg: str) -> None:
    """Print a step marker."""
    print(f"\n[{num}/{total}] {msg}")


def resolve_repo_root() -> Path:
    """Walk up from this script to find the repository root (contains src/fine-tuning/training-data/)."""
    candidate = Path(__file__).resolve().parent
    for _ in range(5):
        if (candidate / "src" / "fine-tuning" / "training-data").is_dir():
            return candidate
        candidate = candidate.parent
    # Fallback: assume CWD
    return Path.cwd()


def format_sharegpt_to_chatml(example: dict) -> dict:
    """Convert ShareGPT format to ChatML string for training."""
    text_parts = []
    for turn in example["conversations"]:
        role = turn["from"]
        content = turn["value"]
        if role == "system":
            text_parts.append(f"<|im_start|>system\n{content}<|im_end|>")
        elif role == "human":
            text_parts.append(f"<|im_start|>user\n{content}<|im_end|>")
        elif role == "gpt":
            text_parts.append(f"<|im_start|>assistant\n{content}<|im_end|>")
    return {"text": "\n".join(text_parts)}


def build_test_prompt(variant: str) -> str:
    """Return a validation prompt matching the model variant."""
    if variant == "ToolCalling":
        return (
            "<|im_start|>system\n"
            "You are a helpful assistant with access to the following tools:\n\n"
            '[{"type":"function","function":{"name":"get_weather","description":"Get current weather",'
            '"parameters":{"type":"object","properties":{"city":{"type":"string"}}}}}]\n\n'
            "When you need to call a tool, respond with a JSON object in this format:\n"
            '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
            "<|im_end|>\n"
            "<|im_start|>user\nWhat is the weather in Tokyo?<|im_end|>\n"
            "<|im_start|>assistant\n"
        )
    elif variant == "RAG":
        return (
            "<|im_start|>system\n"
            "Answer based only on the provided context. Cite sources.\n"
            "<|im_end|>\n"
            "<|im_start|>user\n"
            "Context:\n[1] ONNX Runtime enables fast local inference for ML models.\n\n"
            "Question: What does ONNX Runtime do?<|im_end|>\n"
            "<|im_start|>assistant\n"
        )
    else:  # Instruct
        return (
            "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
            "<|im_start|>user\nWhat is machine learning in one sentence?<|im_end|>\n"
            "<|im_start|>assistant\n"
        )


def generate_model_card(
    variant: str,
    repo_id: str,
    onnx_dir: Path,
    template_path: Path | None,
) -> str:
    """Generate a README.md model card for the HuggingFace repo."""
    model_name = f"Qwen2.5-0.5B-LocalLLMs-{variant}"
    capability = CAPABILITY_LABELS[variant]

    total_mb = sum(
        f.stat().st_size / (1024 * 1024)
        for f in onnx_dir.iterdir()
        if f.is_file()
    )

    tags = [
        "qwen2.5", "onnx", "onnxruntime-genai", "int4", "tool-calling",
        "local-llm", "dotnet", "elbruno", "fine-tuned",
    ]

    if template_path and template_path.exists():
        card = template_path.read_text(encoding="utf-8")
        card = card.replace("{{MODEL_NAME}}", model_name)
        card = card.replace("{{REPO_ID}}", repo_id)
        card = card.replace("{{BASE_MODEL}}", BASE_MODEL_CLEAN)
        card = card.replace("{{CAPABILITY}}", capability)
        card = card.replace("{{MODEL_SIZE}}", "0.5B")
        card = card.replace("{{SIZE_MB}}", f"{total_mb:.0f}")
        card = card.replace("{{TAGS}}", "\n".join(f"- {t}" for t in tags))
        return card

    return f"""---
license: apache-2.0
language:
- en
tags:
{chr(10).join(f'- {t}' for t in tags)}
base_model: {BASE_MODEL_CLEAN}
---

# {model_name}

Fine-tuned Qwen2.5-0.5B optimized for **{capability}** in [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs).

- **Format:** ONNX INT4 (ONNX Runtime GenAI)
- **Size:** ~{total_mb:.0f} MB
- **Training:** QLoRA rank {LORA_R}, 3 epochs
- **License:** Apache 2.0
"""


# ── Pipeline steps ─────────────────────────────────────────────────────────

def step_load_data(variant: str, repo_root: Path):
    """Load training and (optional) validation data."""
    train_file = DATA_FILES[variant]
    train_path = repo_root / "src" / "fine-tuning" / "training-data" / train_file
    if not train_path.exists():
        raise FileNotFoundError(
            f"Training file not found: {train_path}\n"
            f"Expected one of: {list(DATA_FILES.values())}\n"
            f"Looked in: {repo_root / 'src' / 'fine-tuning' / 'training-data'}"
        )

    with open(train_path, encoding="utf-8") as f:
        data = json.load(f)
    print(f"  Loaded {len(data)} training examples from {train_file}")
    print(f"  Example keys: {list(data[0].keys())}")
    print(f"  First conversation has {len(data[0]['conversations'])} turns")

    val_path = repo_root / "src" / "fine-tuning" / "training-data" / "validation.json"
    return train_path, val_path if val_path.exists() else None


def step_format_dataset(train_path: Path, val_path: Path | None):
    """Format raw JSON into ChatML text dataset."""
    from datasets import load_dataset

    train_dataset = load_dataset("json", data_files=str(train_path), split="train")
    train_dataset = train_dataset.map(
        format_sharegpt_to_chatml,
        remove_columns=train_dataset.column_names,
    )
    print(f"  Training examples: {len(train_dataset)}")

    val_dataset = None
    if val_path:
        val_dataset = load_dataset("json", data_files=str(val_path), split="train")
        val_dataset = val_dataset.map(
            format_sharegpt_to_chatml,
            remove_columns=val_dataset.column_names,
        )
        print(f"  Validation examples: {len(val_dataset)}")

    print(f"  Sample (first 300 chars): {train_dataset[0]['text'][:300]}...")
    return train_dataset, val_dataset


def step_load_model():
    """Load base model with QLoRA 4-bit quantization via Unsloth."""
    from unsloth import FastLanguageModel

    print(f"  Base model: {BASE_MODEL}")
    print(f"  QLoRA: rank={LORA_R}, alpha={LORA_ALPHA}, dropout={LORA_DROPOUT}")

    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=BASE_MODEL,
        max_seq_length=MAX_SEQ_LENGTH,
        dtype=None,
        load_in_4bit=True,
    )
    print(f"  Model loaded: {model.config._name_or_path}")

    model = FastLanguageModel.get_peft_model(
        model,
        r=LORA_R,
        lora_alpha=LORA_ALPHA,
        lora_dropout=LORA_DROPOUT,
        target_modules=TARGET_MODULES,
        bias="none",
        use_gradient_checkpointing="unsloth",
    )
    print(f"  QLoRA adapters configured")
    return model, tokenizer


def step_train(model, tokenizer, train_dataset, val_dataset, epochs: int, output_dir: Path):
    """Run SFTTrainer with the configured hyperparameters."""
    from transformers import TrainingArguments
    from trl import SFTTrainer

    lora_dir = output_dir / "lora-adapter"
    lora_dir.mkdir(parents=True, exist_ok=True)

    training_args = TrainingArguments(
        output_dir=str(lora_dir),
        num_train_epochs=epochs,
        per_device_train_batch_size=BATCH_SIZE,
        gradient_accumulation_steps=GRAD_ACCUM,
        learning_rate=LEARNING_RATE,
        fp16=True,
        logging_steps=50,
        save_steps=500,
        save_total_limit=2,
        eval_strategy="steps" if val_dataset else "no",
        eval_steps=500 if val_dataset else None,
        warmup_steps=50,
        lr_scheduler_type="cosine",
        optim="paged_adamw_8bit",
        weight_decay=0.01,
        load_best_model_at_end=bool(val_dataset),
        metric_for_best_model="eval_loss" if val_dataset else None,
        report_to="none",
    )

    trainer = SFTTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
        dataset_text_field="text",
        max_seq_length=MAX_SEQ_LENGTH,
        args=training_args,
    )

    print(f"  Epochs: {epochs}")
    print(f"  Batch size: {BATCH_SIZE} (effective: {BATCH_SIZE * GRAD_ACCUM})")
    print(f"  Learning rate: {LEARNING_RATE}")
    print(f"  Output: {lora_dir}")

    trainer.train()

    model.save_pretrained(str(lora_dir))
    tokenizer.save_pretrained(str(lora_dir))
    print(f"  LoRA adapter saved to {lora_dir}")
    return lora_dir


def step_merge(model, tokenizer, output_dir: Path):
    """Merge LoRA adapters into the base model (FP16)."""
    merged_dir = output_dir / "merged-model"
    merged_dir.mkdir(parents=True, exist_ok=True)

    model.save_pretrained_merged(
        str(merged_dir),
        tokenizer,
        save_method="merged_16bit",
    )

    print(f"  Merged model saved to {merged_dir}")
    for f in sorted(merged_dir.iterdir()):
        if f.is_file():
            size_mb = f.stat().st_size / (1024 * 1024)
            print(f"    {f.name:40s} {size_mb:8.1f} MB")
    return merged_dir


def step_convert_onnx(merged_dir: Path, output_dir: Path):
    """Convert the merged model to ONNX INT4 using onnxruntime-genai builder."""
    onnx_dir = output_dir / "onnx-int4"
    onnx_dir.mkdir(parents=True, exist_ok=True)

    # Ensure onnx-ir is installed (required by builder)
    subprocess.run(
        [sys.executable, "-m", "pip", "install", "-q", "onnx", "onnx-ir"],
        check=False,
    )

    print(f"  Input:  {merged_dir}")
    print(f"  Output: {onnx_dir}")
    print("  This takes ~5-10 minutes...")

    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-m", str(merged_dir),
        "-o", str(onnx_dir),
        "-p", "int4",
        "-e", "cpu",
        "--extra_options", "int4_accuracy_level=4",
    ]

    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.stdout:
        print(result.stdout)
    if result.returncode != 0:
        print("  STDERR:", result.stderr)
        raise RuntimeError(
            f"ONNX conversion failed (exit code {result.returncode}).\n"
            f"Command: {' '.join(cmd)}"
        )

    # Verify expected files
    expected = ["model.onnx", "genai_config.json", "tokenizer.json", "tokenizer_config.json"]
    for fname in expected:
        if not (onnx_dir / fname).exists():
            raise FileNotFoundError(f"Missing expected ONNX file: {fname}")

    total_mb = 0
    for f in sorted(onnx_dir.iterdir()):
        if f.is_file():
            size_mb = f.stat().st_size / (1024 * 1024)
            total_mb += size_mb
            print(f"    {f.name:40s} {size_mb:8.1f} MB")
    print(f"    {'TOTAL':40s} {total_mb:8.1f} MB")

    return onnx_dir


def step_validate(onnx_dir: Path, variant: str):
    """Validate the ONNX model by running a test prompt."""
    try:
        import onnxruntime_genai as og
    except ImportError:
        print("  ⚠️  onnxruntime-genai not available — skipping validation.")
        print(f"     ONNX files are in: {onnx_dir}")
        print("     Validate locally with: python validate_onnx.py --model-dir", onnx_dir)
        return

    print("  Loading ONNX model...")
    onnx_model = og.Model(str(onnx_dir))
    onnx_tokenizer = og.Tokenizer(onnx_model)

    test_prompt = build_test_prompt(variant)
    print(f"  Test prompt ({variant}):")
    for line in test_prompt.split("\n")[:3]:
        print(f"    {line}")
    print("    ...")

    input_tokens = onnx_tokenizer.encode(test_prompt)
    params = og.GeneratorParams(onnx_model)
    params.set_search_options(max_length=300, do_sample=False)

    generator = og.Generator(onnx_model, params)
    generator.append_tokens(input_tokens)
    while not generator.is_done():
        generator.generate_next_token()
    output_text = onnx_tokenizer.decode(generator.get_sequence(0))

    print(f"\n  Model output:\n  {output_text[:500]}")
    print("  " + "─" * 58)

    # Basic sanity checks
    if variant == "ToolCalling":
        if "tool_call" in output_text or "get_weather" in output_text:
            print("  ✅ Model produces tool-calling output!")
        else:
            print("  ⚠️  Output doesn't contain tool_call tags — may need more training.")
    elif variant == "RAG":
        if "ONNX" in output_text or "inference" in output_text.lower():
            print("  ✅ Model produces grounded output!")
        else:
            print("  ⚠️  Output may not be well-grounded — check manually.")
    else:
        if len(output_text.strip()) > 10:
            print("  ✅ Model produces coherent output!")
        else:
            print("  ⚠️  Output seems too short — check manually.")

    del generator, onnx_model, onnx_tokenizer
    print("  Validation complete.")


def step_upload(onnx_dir: Path, variant: str, hf_token: str, hf_username: str):
    """Upload the ONNX model to HuggingFace Hub."""
    from huggingface_hub import HfApi, create_repo

    repo_id = f"{hf_username}/Qwen2.5-0.5B-LocalLLMs-{variant}"
    api = HfApi(token=hf_token)

    print(f"  Repo: {repo_id}")
    create_repo(repo_id, repo_type="model", exist_ok=True, token=hf_token)

    # Generate model card
    template_path = Path(__file__).resolve().parent / "model-card-template.md"
    model_card = generate_model_card(variant, repo_id, onnx_dir, template_path)
    readme_path = onnx_dir / "README.md"
    readme_path.write_text(model_card, encoding="utf-8")

    total_mb = sum(
        f.stat().st_size / (1024 * 1024) for f in onnx_dir.iterdir() if f.is_file()
    )
    print(f"  Uploading {total_mb:.0f} MB...")

    api.upload_folder(
        folder_path=str(onnx_dir),
        repo_id=repo_id,
        repo_type="model",
        commit_message=f"Upload {CAPABILITY_LABELS[variant]} fine-tuned ONNX INT4 model",
    )

    url = f"https://huggingface.co/{repo_id}"
    print(f"  Published: {url}")
    return url


# ── Main ───────────────────────────────────────────────────────────────────

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Fine-tune Qwen2.5-0.5B for ElBruno.LocalLLMs and optionally convert to ONNX + upload.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python train.py --variant ToolCalling --hf-token hf_xxxx
  python train.py --variant RAG --skip-upload --epochs 5
  python train.py --variant Instruct --skip-onnx --skip-upload --output-dir ./my-output

Environment variables:
  HF_TOKEN   HuggingFace token (alternative to --hf-token)
""",
    )
    parser.add_argument(
        "--variant",
        choices=["ToolCalling", "RAG", "Instruct"],
        default="ToolCalling",
        help="Model variant to train (default: ToolCalling)",
    )
    parser.add_argument(
        "--hf-token",
        default=None,
        help="HuggingFace token with write access (or set HF_TOKEN env var)",
    )
    parser.add_argument(
        "--hf-username",
        default="elbruno",
        help="HuggingFace username for the upload repo (default: elbruno)",
    )
    parser.add_argument(
        "--epochs",
        type=int,
        default=3,
        help="Number of training epochs (default: 3)",
    )
    parser.add_argument(
        "--output-dir",
        default="./output",
        help="Base output directory (default: ./output)",
    )
    parser.add_argument(
        "--skip-upload",
        action="store_true",
        help="Skip uploading to HuggingFace",
    )
    parser.add_argument(
        "--skip-onnx",
        action="store_true",
        help="Skip ONNX INT4 conversion",
    )
    parser.add_argument(
        "--skip-validation",
        action="store_true",
        help="Skip ONNX model validation",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    hf_token = args.hf_token or os.environ.get("HF_TOKEN", "")

    # Pre-flight checks
    if not args.skip_upload and not hf_token:
        print("❌ HF_TOKEN required for upload. Use --hf-token or set HF_TOKEN env var.")
        print("   Or add --skip-upload to train without uploading.")
        return 1

    repo_root = resolve_repo_root()
    variant = args.variant

    # Calculate total steps
    total = 5  # load, format, load model, train, merge
    if not args.skip_onnx:
        total += 1
    if not args.skip_onnx and not args.skip_validation:
        total += 1
    if not args.skip_upload and not args.skip_onnx:
        total += 1

    print("=" * 62)
    print(f"  ElBruno.LocalLLMs Fine-Tuning Pipeline")
    print(f"  Variant:    {variant} ({CAPABILITY_LABELS[variant]})")
    print(f"  Base model: {BASE_MODEL}")
    print(f"  Epochs:     {args.epochs}")
    print(f"  Output:     {output_dir}")
    print(f"  ONNX:       {'skip' if args.skip_onnx else 'yes'}")
    print(f"  Validation: {'skip' if args.skip_validation else 'yes'}")
    print(f"  Upload:     {'skip' if args.skip_upload else args.hf_username}")
    print("=" * 62)

    current = 0

    # Step 1: Load data
    current += 1
    step(current, total, "Loading training data...")
    try:
        train_path, val_path = step_load_data(variant, repo_root)
    except FileNotFoundError as e:
        print(f"❌ {e}")
        return 1

    # Step 2: Format dataset
    current += 1
    step(current, total, "Formatting dataset...")
    try:
        train_dataset, val_dataset = step_format_dataset(train_path, val_path)
    except Exception as e:
        print(f"❌ Failed to format dataset: {e}")
        traceback.print_exc()
        return 1

    # Step 3: Load model
    current += 1
    step(current, total, "Loading base model (QLoRA 4-bit)...")
    try:
        model, tokenizer = step_load_model()
    except Exception as e:
        print(f"❌ Failed to load model: {e}")
        traceback.print_exc()
        return 1

    # Step 4: Train
    current += 1
    step(current, total, f"Training ({args.epochs} epochs)...")
    try:
        lora_dir = step_train(model, tokenizer, train_dataset, val_dataset, args.epochs, output_dir)
    except Exception as e:
        print(f"❌ Training failed: {e}")
        traceback.print_exc()
        # Save what we can
        emergency = output_dir / "emergency-save"
        emergency.mkdir(parents=True, exist_ok=True)
        try:
            model.save_pretrained(str(emergency))
            tokenizer.save_pretrained(str(emergency))
            print(f"  ⚠️  Partial model saved to {emergency}")
        except Exception:
            pass
        return 1

    # Step 5: Merge LoRA
    current += 1
    step(current, total, "Merging LoRA adapter...")
    try:
        merged_dir = step_merge(model, tokenizer, output_dir)
    except Exception as e:
        print(f"❌ Merge failed: {e}")
        traceback.print_exc()
        return 1

    # Step 6: ONNX conversion (optional)
    onnx_dir = None
    if not args.skip_onnx:
        current += 1
        step(current, total, "Converting to ONNX INT4...")
        try:
            onnx_dir = step_convert_onnx(merged_dir, output_dir)
        except Exception as e:
            print(f"❌ ONNX conversion failed: {e}")
            traceback.print_exc()
            print("  Merged model is still available at:", merged_dir)
            return 1

    # Step 7: Validate (optional)
    if not args.skip_onnx and not args.skip_validation:
        current += 1
        step(current, total, "Validating ONNX model...")
        try:
            step_validate(onnx_dir, variant)
        except Exception as e:
            print(f"⚠️  Validation failed (non-fatal): {e}")
            traceback.print_exc()

    # Step 8: Upload (optional)
    if not args.skip_upload and not args.skip_onnx:
        current += 1
        step(current, total, "Uploading to HuggingFace...")
        try:
            url = step_upload(onnx_dir, variant, hf_token, args.hf_username)
        except Exception as e:
            print(f"❌ Upload failed: {e}")
            traceback.print_exc()
            print(f"  ONNX model is still available at: {onnx_dir}")
            return 1

    # Done!
    print("\n" + "=" * 62)
    if not args.skip_upload and not args.skip_onnx:
        repo_id = f"{args.hf_username}/Qwen2.5-0.5B-LocalLLMs-{variant}"
        print(f"✅ Done! Model available at: https://huggingface.co/{repo_id}")
    elif onnx_dir:
        print(f"✅ Done! ONNX model saved to: {onnx_dir}")
    else:
        print(f"✅ Done! Merged model saved to: {merged_dir}")
    print("=" * 62)

    return 0


if __name__ == "__main__":
    sys.exit(main())
