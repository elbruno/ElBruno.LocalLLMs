#!/usr/bin/env python3
"""
Windows-compatible fine-tuning pipeline for Qwen2.5-0.5B-Instruct.

Uses standard transformers + peft + trl (NO Unsloth, NO bitsandbytes).
With 24 GB VRAM (e.g. NVIDIA A10) and a 0.5B model, FP16 LoRA is fine.

Full pipeline:
  1. Load training data (ShareGPT → ChatML)
  2. Fine-tune with LoRA (FP16)
  3. Merge LoRA adapter into base model
  4. Convert merged model to ONNX (via onnxruntime-genai builder)
  5. Validate ONNX model
  6. Upload to HuggingFace Hub

Usage:
    python train_windows.py --variant ToolCalling
    python train_windows.py --variant RAG --epochs 5 --skip-upload
    python train_windows.py --variant Instruct --skip-onnx --skip-upload
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import subprocess
import sys
import time
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ─── Constants ────────────────────────────────────────────────────────────────

BASE_MODEL = "Qwen/Qwen2.5-0.5B-Instruct"
MAX_SEQ_LENGTH = 2048

LORA_R = 16
LORA_ALPHA = 32
LORA_DROPOUT = 0.05
TARGET_MODULES = [
    "q_proj", "k_proj", "v_proj", "o_proj",
    "gate_proj", "up_proj", "down_proj",
]

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


# ─── Helpers ──────────────────────────────────────────────────────────────────

def _find_repo_root() -> Path:
    """Walk up from this script's directory to find the repo root."""
    current = Path(__file__).resolve().parent
    for _ in range(10):
        if (current / "src" / "fine-tuning" / "training-data").is_dir():
            return current
        current = current.parent
    return Path(__file__).resolve().parent


def _banner(step: int, total: int, title: str) -> None:
    log.info("")
    log.info("=" * 60)
    log.info("  Step %d/%d: %s", step, total, title)
    log.info("=" * 60)


def _check_cuda() -> None:
    """Verify CUDA is available."""
    import torch

    if not torch.cuda.is_available():
        log.warning("CUDA is NOT available. Training will use CPU (very slow).")
        log.warning("Ensure you have NVIDIA drivers + CUDA toolkit installed.")
    else:
        device_name = torch.cuda.get_device_name(0)
        vram_gb = torch.cuda.get_device_properties(0).total_mem / (1024**3)
        log.info("GPU: %s (%.1f GB VRAM)", device_name, vram_gb)


# ─── Step 1: Load & Format Data ──────────────────────────────────────────────

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
    text = "\n".join(text_parts)
    return {"text": text}


def load_data(data_dir: Path, variant: str) -> tuple:
    """Load training and validation datasets."""
    from datasets import load_dataset

    train_file = DATA_FILES[variant]
    train_path = data_dir / train_file
    if not train_path.exists():
        log.error("Training data not found: %s", train_path)
        log.error("Available files in %s:", data_dir)
        for f in sorted(data_dir.iterdir()):
            log.error("  %s", f.name)
        sys.exit(1)

    log.info("Loading training data: %s", train_path)
    train_dataset = load_dataset("json", data_files=str(train_path), split="train")
    train_dataset = train_dataset.map(
        format_sharegpt_to_chatml,
        remove_columns=train_dataset.column_names,
    )
    log.info("Training examples: %d", len(train_dataset))

    val_dataset = None
    val_path = data_dir / "validation.json"
    if val_path.exists():
        val_dataset = load_dataset("json", data_files=str(val_path), split="train")
        val_dataset = val_dataset.map(
            format_sharegpt_to_chatml,
            remove_columns=val_dataset.column_names,
        )
        log.info("Validation examples: %d", len(val_dataset))
    else:
        log.info("No validation.json found, skipping evaluation during training.")

    log.info("Sample (first 300 chars): %s...", train_dataset[0]["text"][:300])
    return train_dataset, val_dataset


# ─── Step 2: Train ───────────────────────────────────────────────────────────

def train_model(
    train_dataset,
    val_dataset,
    output_dir: Path,
    epochs: int,
    batch_size: int,
    learning_rate: float,
    grad_accum: int,
) -> tuple:
    """Fine-tune Qwen2.5-0.5B with LoRA (FP16, no quantization)."""
    import torch
    from peft import LoraConfig, TaskType, get_peft_model
    from transformers import AutoModelForCausalLM, AutoTokenizer, TrainingArguments
    from trl import SFTTrainer

    device = "cuda" if torch.cuda.is_available() else "cpu"

    # Load tokenizer
    log.info("Loading tokenizer: %s", BASE_MODEL)
    tokenizer = AutoTokenizer.from_pretrained(BASE_MODEL, trust_remote_code=True)
    if tokenizer.pad_token is None:
        tokenizer.pad_token = tokenizer.eos_token

    # Load model in FP16 — no quantization needed with 24 GB VRAM for a 0.5B model
    log.info("Loading model: %s (FP16, no quantization)", BASE_MODEL)
    model = AutoModelForCausalLM.from_pretrained(
        BASE_MODEL,
        torch_dtype=torch.float16,
        device_map="auto" if device == "cuda" else None,
        trust_remote_code=True,
    )
    log.info("Model loaded: %s parameters", f"{model.num_parameters():,}")

    # Apply LoRA
    log.info("Applying LoRA: rank=%d, alpha=%d", LORA_R, LORA_ALPHA)
    lora_config = LoraConfig(
        r=LORA_R,
        lora_alpha=LORA_ALPHA,
        lora_dropout=LORA_DROPOUT,
        target_modules=TARGET_MODULES,
        bias="none",
        task_type=TaskType.CAUSAL_LM,
    )
    model = get_peft_model(model, lora_config)
    model.print_trainable_parameters()

    # Training arguments — use adamw_torch (no bitsandbytes dependency)
    lora_output_dir = output_dir / "lora-adapter"
    lora_output_dir.mkdir(parents=True, exist_ok=True)

    training_args = TrainingArguments(
        output_dir=str(lora_output_dir),
        num_train_epochs=epochs,
        per_device_train_batch_size=batch_size,
        gradient_accumulation_steps=grad_accum,
        learning_rate=learning_rate,
        fp16=(device == "cuda"),
        logging_steps=25,
        save_steps=500,
        save_total_limit=2,
        eval_strategy="steps" if val_dataset else "no",
        eval_steps=500 if val_dataset else None,
        warmup_steps=50,
        lr_scheduler_type="cosine",
        optim="adamw_torch",
        weight_decay=0.01,
        load_best_model_at_end=bool(val_dataset),
        metric_for_best_model="eval_loss" if val_dataset else None,
        report_to="none",
        gradient_checkpointing=True,
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

    log.info("Starting training...")
    log.info("  Epochs: %d", epochs)
    log.info("  Batch size: %d (effective: %d)", batch_size, batch_size * grad_accum)
    log.info("  Learning rate: %s", learning_rate)
    log.info("  Device: %s", device)

    start = time.time()
    trainer.train()
    elapsed = time.time() - start
    log.info("Training completed in %.1f minutes.", elapsed / 60)

    # Save LoRA adapter
    log.info("Saving LoRA adapter to %s", lora_output_dir)
    model.save_pretrained(str(lora_output_dir))
    tokenizer.save_pretrained(str(lora_output_dir))

    # Save training config for reproducibility
    config = {
        "base_model": BASE_MODEL,
        "lora_r": LORA_R,
        "lora_alpha": LORA_ALPHA,
        "lora_dropout": LORA_DROPOUT,
        "target_modules": TARGET_MODULES,
        "epochs": epochs,
        "batch_size": batch_size,
        "learning_rate": learning_rate,
        "grad_accum": grad_accum,
        "max_seq_length": MAX_SEQ_LENGTH,
        "method": "transformers+peft (FP16 LoRA, no quantization)",
    }
    config_path = lora_output_dir / "training_config.json"
    with open(config_path, "w") as f:
        json.dump(config, f, indent=2)

    log.info("LoRA adapter saved.")
    return model, tokenizer


# ─── Step 3: Merge LoRA ──────────────────────────────────────────────────────

def merge_lora(output_dir: Path) -> Path:
    """Merge LoRA adapter into the base model."""
    import torch
    from peft import PeftModel
    from transformers import AutoModelForCausalLM, AutoTokenizer

    lora_dir = output_dir / "lora-adapter"
    merged_dir = output_dir / "merged-model"
    merged_dir.mkdir(parents=True, exist_ok=True)

    log.info("Loading tokenizer from adapter: %s", lora_dir)
    tokenizer = AutoTokenizer.from_pretrained(str(lora_dir), trust_remote_code=True)

    log.info("Loading base model: %s", BASE_MODEL)
    base_model = AutoModelForCausalLM.from_pretrained(
        BASE_MODEL,
        torch_dtype=torch.float16,
        device_map="cpu",
        trust_remote_code=True,
    )

    # Resize embeddings if needed
    if len(tokenizer) != base_model.config.vocab_size:
        log.info(
            "Resizing embeddings from %d to %d",
            base_model.config.vocab_size, len(tokenizer),
        )
        base_model.resize_token_embeddings(len(tokenizer))

    log.info("Loading LoRA adapter...")
    model = PeftModel.from_pretrained(base_model, str(lora_dir))

    log.info("Merging adapter weights...")
    merged = model.merge_and_unload()

    log.info("Saving merged model to %s", merged_dir)
    merged.save_pretrained(str(merged_dir), safe_serialization=True)
    tokenizer.save_pretrained(str(merged_dir))

    total_bytes = sum(f.stat().st_size for f in merged_dir.rglob("*") if f.is_file())
    log.info("Merged model saved (%.1f MB)", total_bytes / (1024 * 1024))

    return merged_dir


# ─── Step 4: ONNX Conversion ─────────────────────────────────────────────────

def convert_to_onnx(merged_dir: Path, output_dir: Path) -> Path:
    """Convert merged model to ONNX INT4 using onnxruntime-genai builder."""
    onnx_dir = output_dir / "onnx-model"
    onnx_dir.mkdir(parents=True, exist_ok=True)

    log.info("Converting to ONNX INT4 format...")
    log.info("  Input:  %s", merged_dir)
    log.info("  Output: %s", onnx_dir)
    log.info("This may take 5-10 minutes...")

    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-m", str(merged_dir),
        "-o", str(onnx_dir),
        "-p", "int4",
        "-e", "cpu",
        "--extra_options", "int4_accuracy_level=4",
    ]

    log.info("Running: %s", " ".join(cmd))
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
        log.error("ONNX conversion failed with exit code %d", returncode)
        sys.exit(returncode)

    # Verify expected files
    expected = ["model.onnx", "genai_config.json", "tokenizer.json", "tokenizer_config.json"]
    missing = [f for f in expected if not (onnx_dir / f).exists()]
    if missing:
        log.error("Missing expected ONNX output files: %s", missing)
        sys.exit(1)

    total_bytes = sum(f.stat().st_size for f in onnx_dir.rglob("*") if f.is_file())
    log.info("ONNX model saved (%.1f MB)", total_bytes / (1024 * 1024))
    for fpath in sorted(onnx_dir.iterdir()):
        if fpath.is_file():
            size_mb = fpath.stat().st_size / (1024 * 1024)
            log.info("  %s  %.1f MB", fpath.name, size_mb)

    return onnx_dir


# ─── Step 5: Validate ONNX ───────────────────────────────────────────────────

def validate_onnx(onnx_dir: Path, variant: str) -> None:
    """Run a quick validation of the ONNX model."""
    try:
        import onnxruntime_genai as og
    except ImportError:
        log.warning("onnxruntime-genai not installed. Skipping ONNX validation.")
        log.warning("Install with: pip install onnxruntime-genai")
        return

    log.info("Loading ONNX model from %s...", onnx_dir)
    onnx_model = og.Model(str(onnx_dir))
    onnx_tokenizer = og.Tokenizer(onnx_model)

    # Build test prompt based on variant
    if variant == "ToolCalling":
        test_prompt = (
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
        test_prompt = (
            "<|im_start|>system\n"
            "Answer based only on the provided context. Cite sources.\n"
            "<|im_end|>\n"
            "<|im_start|>user\n"
            "Context:\n[1] ONNX Runtime enables fast local inference for ML models.\n\n"
            "Question: What does ONNX Runtime do?<|im_end|>\n"
            "<|im_start|>assistant\n"
        )
    else:
        test_prompt = (
            "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
            "<|im_start|>user\nWhat is machine learning in one sentence?<|im_end|>\n"
            "<|im_start|>assistant\n"
        )

    log.info("Test prompt (%s):\n%s", variant, test_prompt)
    log.info("-" * 60)

    # Generate using correct onnxruntime-genai API
    input_tokens = onnx_tokenizer.encode(test_prompt)
    params = og.GeneratorParams(onnx_model)
    params.set_search_options(max_length=300, do_sample=False)

    generator = og.Generator(onnx_model, params)
    generator.append_tokens(input_tokens)
    while not generator.is_done():
        generator.generate_next_token()
    output_text = onnx_tokenizer.decode(generator.get_sequence(0))

    log.info("Model output:\n%s", output_text)
    log.info("-" * 60)

    # Sanity checks
    if variant == "ToolCalling":
        if "tool_call" in output_text or "get_weather" in output_text:
            log.info("✅ Model produces tool-calling output!")
        else:
            log.warning("⚠️  Output doesn't contain tool_call tags — may need more training.")
    elif variant == "RAG":
        if "ONNX" in output_text or "inference" in output_text.lower():
            log.info("✅ Model produces grounded output!")
        else:
            log.warning("⚠️  Output may not be well-grounded — check manually.")
    else:
        if len(output_text.strip()) > 10:
            log.info("✅ Model produces coherent output!")
        else:
            log.warning("⚠️  Output seems too short — check manually.")

    del generator, onnx_model, onnx_tokenizer
    log.info("Validation complete.")


# ─── Step 6: Upload to HuggingFace ───────────────────────────────────────────

def upload_to_hf(
    onnx_dir: Path,
    variant: str,
    hf_token: str,
    hf_username: str,
    repo_root: Path,
) -> None:
    """Upload the ONNX model to HuggingFace Hub."""
    from huggingface_hub import HfApi, create_repo

    repo_id = f"{hf_username}/Qwen2.5-0.5B-LocalLLMs-{variant}"
    capability = CAPABILITY_LABELS[variant]
    model_name = f"Qwen2.5-0.5B-LocalLLMs-{variant}"

    api = HfApi(token=hf_token)

    log.info("Creating/updating repo: %s", repo_id)
    create_repo(repo_id, repo_type="model", exist_ok=True, token=hf_token)
    log.info("Repository ready: https://huggingface.co/%s", repo_id)

    # Calculate total size
    total_mb = sum(
        f.stat().st_size / (1024 * 1024)
        for f in onnx_dir.rglob("*") if f.is_file()
    )

    # Generate model card (try template first)
    template_path = repo_root / "scripts" / "finetune" / "model-card-template.md"
    tags = [
        "qwen2.5", "onnx", "onnxruntime-genai", "int4", "tool-calling",
        "local-llm", "dotnet", "elbruno", "fine-tuned",
    ]

    if template_path.exists():
        model_card = template_path.read_text(encoding="utf-8")
        model_card = model_card.replace("{{MODEL_NAME}}", model_name)
        model_card = model_card.replace("{{REPO_ID}}", repo_id)
        model_card = model_card.replace("{{BASE_MODEL}}", "Qwen/Qwen2.5-0.5B-Instruct")
        model_card = model_card.replace("{{CAPABILITY}}", capability)
        model_card = model_card.replace("{{MODEL_SIZE}}", "0.5B")
        model_card = model_card.replace("{{SIZE_MB}}", f"{total_mb:.0f}")
        model_card = model_card.replace("{{TAGS}}", "\n".join(f"- {t}" for t in tags))
        log.info("Using model card template from repo.")
    else:
        model_card = (
            f"---\nlicense: apache-2.0\nlanguage:\n- en\ntags:\n"
            + "\n".join(f"- {t}" for t in tags)
            + f"\nbase_model: Qwen/Qwen2.5-0.5B-Instruct\n---\n\n"
            f"# {model_name}\n\n"
            f"Fine-tuned Qwen2.5-0.5B for **{capability}** "
            f"in [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs).\n\n"
            f"- **Format:** ONNX INT4\n"
            f"- **Size:** ~{total_mb:.0f} MB\n"
            f"- **Training:** LoRA rank 16, 3 epochs (transformers+peft)\n"
            f"- **License:** Apache 2.0\n"
        )
        log.info("Using inline model card (template not found).")

    readme_path = onnx_dir / "README.md"
    readme_existed = readme_path.exists()
    readme_path.write_text(model_card, encoding="utf-8")

    log.info("Uploading %.0f MB to %s...", total_mb, repo_id)
    try:
        api.upload_folder(
            folder_path=str(onnx_dir),
            repo_id=repo_id,
            repo_type="model",
            commit_message=f"Upload {capability} fine-tuned ONNX INT4 model",
        )
    except Exception as e:
        log.error("Upload failed: %s", e)
        if not readme_existed and readme_path.exists():
            readme_path.unlink()
        sys.exit(1)

    if not readme_existed and readme_path.exists():
        readme_path.unlink()

    log.info("✅ Model published: https://huggingface.co/%s", repo_id)


# ─── CLI ──────────────────────────────────────────────────────────────────────

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Windows-compatible fine-tuning pipeline for Qwen2.5-0.5B-Instruct. "
            "Uses transformers + peft + trl (no Unsloth, no bitsandbytes)."
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python train_windows.py --variant ToolCalling\n"
            "  python train_windows.py --variant RAG --epochs 5\n"
            "  python train_windows.py --variant Instruct --skip-upload --skip-onnx\n"
        ),
    )
    parser.add_argument(
        "--variant",
        default="ToolCalling",
        choices=["ToolCalling", "RAG", "Instruct"],
        help="Model variant to train (default: ToolCalling).",
    )
    parser.add_argument(
        "--hf-token",
        default=None,
        help="HuggingFace token (or set HF_TOKEN env var).",
    )
    parser.add_argument(
        "--hf-username",
        default="elbruno",
        help="HuggingFace username (default: elbruno).",
    )
    parser.add_argument(
        "--epochs",
        type=int,
        default=3,
        help="Number of training epochs (default: 3).",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=4,
        help="Per-device training batch size (default: 4).",
    )
    parser.add_argument(
        "--learning-rate",
        type=float,
        default=2e-4,
        help="Learning rate (default: 2e-4).",
    )
    parser.add_argument(
        "--grad-accum",
        type=int,
        default=4,
        help="Gradient accumulation steps (default: 4).",
    )
    parser.add_argument(
        "--output-dir",
        default="./output",
        help="Output directory for all artifacts (default: ./output).",
    )
    parser.add_argument(
        "--skip-upload",
        action="store_true",
        help="Skip uploading to HuggingFace Hub.",
    )
    parser.add_argument(
        "--skip-onnx",
        action="store_true",
        help="Skip ONNX conversion.",
    )
    parser.add_argument(
        "--skip-validation",
        action="store_true",
        help="Skip ONNX model validation.",
    )
    parser.add_argument(
        "--data-dir",
        default=None,
        help="Path to training data directory (default: auto-detect from repo root).",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    # Resolve data directory
    repo_root = _find_repo_root()
    if args.data_dir:
        data_dir = Path(args.data_dir)
    else:
        data_dir = repo_root / "src" / "fine-tuning" / "training-data"
    if not data_dir.is_dir():
        log.error("Training data directory not found: %s", data_dir)
        sys.exit(1)

    # Resolve HF token
    hf_token = args.hf_token or os.environ.get("HF_TOKEN", "")
    if not hf_token and not args.skip_upload:
        log.error(
            "No HuggingFace token provided. "
            "Use --hf-token, set HF_TOKEN env var, or pass --skip-upload."
        )
        sys.exit(1)

    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    total_steps = 3  # train + merge + (onnx/validate/upload counted below)
    if not args.skip_onnx:
        total_steps += 1
    if not args.skip_onnx and not args.skip_validation:
        total_steps += 1
    if not args.skip_upload:
        total_steps += 1

    log.info("=" * 60)
    log.info("  Windows Fine-Tuning Pipeline")
    log.info("  Variant: %s", args.variant)
    log.info("  Data: %s/%s", data_dir, DATA_FILES[args.variant])
    log.info("  Output: %s", output_dir)
    log.info("  Steps: %d", total_steps)
    log.info("=" * 60)

    step = 0

    # ── Step 1: Check environment ─────────────────────────────────────────
    step += 1
    _banner(step, total_steps, "Check Environment")
    _check_cuda()

    # ── Step 2: Load data ─────────────────────────────────────────────────
    step += 1
    _banner(step, total_steps, "Load Training Data")
    train_dataset, val_dataset = load_data(data_dir, args.variant)

    # ── Step 3: Train ─────────────────────────────────────────────────────
    step += 1
    _banner(step, total_steps, "Fine-Tune with LoRA (FP16)")
    _model, _tokenizer = train_model(
        train_dataset=train_dataset,
        val_dataset=val_dataset,
        output_dir=output_dir,
        epochs=args.epochs,
        batch_size=args.batch_size,
        learning_rate=args.learning_rate,
        grad_accum=args.grad_accum,
    )
    # Free training model from GPU memory before merge
    del _model, _tokenizer
    import torch
    if torch.cuda.is_available():
        torch.cuda.empty_cache()

    # ── Step 4: Merge LoRA ────────────────────────────────────────────────
    # (counted as part of step 3's continuation)
    _banner(step, total_steps, "Merge LoRA Adapter")
    merged_dir = merge_lora(output_dir)

    # ── Step 5: ONNX conversion ──────────────────────────────────────────
    onnx_dir = None
    if not args.skip_onnx:
        step += 1
        _banner(step, total_steps, "Convert to ONNX INT4")
        onnx_dir = convert_to_onnx(merged_dir, output_dir)

    # ── Step 6: Validate ONNX ─────────────────────────────────────────────
    if not args.skip_onnx and not args.skip_validation and onnx_dir:
        step += 1
        _banner(step, total_steps, "Validate ONNX Model")
        validate_onnx(onnx_dir, args.variant)

    # ── Step 7: Upload to HuggingFace ─────────────────────────────────────
    if not args.skip_upload:
        step += 1
        upload_dir = onnx_dir if onnx_dir else merged_dir
        _banner(step, total_steps, "Upload to HuggingFace")
        upload_to_hf(
            onnx_dir=upload_dir,
            variant=args.variant,
            hf_token=hf_token,
            hf_username=args.hf_username,
            repo_root=repo_root,
        )

    # ── Done ──────────────────────────────────────────────────────────────
    log.info("")
    log.info("=" * 60)
    log.info("  🎉 Pipeline complete!")
    log.info("  Output: %s", output_dir)
    if onnx_dir:
        log.info("  ONNX model: %s", onnx_dir)
    if not args.skip_upload:
        log.info("  HuggingFace: https://huggingface.co/%s/Qwen2.5-0.5B-LocalLLMs-%s",
                 args.hf_username, args.variant)
    log.info("=" * 60)


if __name__ == "__main__":
    main()
