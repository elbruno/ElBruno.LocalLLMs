#!/usr/bin/env python3
"""
Fine-tune Qwen2.5-1.5B-Instruct for ElBruno.LocalLLMs using QLoRA.

Larger model with better baseline capabilities. Uses higher LoRA rank
and lower learning rate than the 0.5B variant for better quality.

Usage:
    python train_qwen_15b.py --output-dir ./output/qwen25-15b-finetuned

Hardware Requirements:
    - GPU: RTX 4090 (24 GB VRAM) — tight fit, or A100 (40/80 GB)
    - RAM: 32 GB system memory
    - Disk: 80 GB free space
"""

import argparse
import json
import logging
import sys
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)

MODEL_NAME = "Qwen/Qwen2.5-1.5B-Instruct"
MAX_SEQ_LENGTH = 2048

# QLoRA hyperparameters (optimized for Qwen2.5-1.5B)
LORA_R = 32
LORA_ALPHA = 64
LORA_DROPOUT = 0.05
TARGET_MODULES = [
    "q_proj", "k_proj", "v_proj", "o_proj",
    "gate_proj", "up_proj", "down_proj",
]


def format_sharegpt_to_chatml(example: dict, tokenizer) -> dict:
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


def main():
    parser = argparse.ArgumentParser(
        description="Fine-tune Qwen2.5-1.5B-Instruct with QLoRA"
    )
    parser.add_argument(
        "--output-dir",
        default="./output/qwen25-15b-finetuned",
        help="Directory to save the fine-tuned LoRA adapters",
    )
    parser.add_argument(
        "--data-path",
        default="../training-data/combined-train.json",
        help="Path to training data (ShareGPT JSON format)",
    )
    parser.add_argument(
        "--val-path",
        default="../training-data/validation.json",
        help="Path to validation data (ShareGPT JSON format)",
    )
    parser.add_argument(
        "--epochs", type=int, default=3, help="Number of training epochs"
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=2,
        help="Per-device batch size (smaller for 1.5B)",
    )
    parser.add_argument(
        "--learning-rate",
        type=float,
        default=1e-4,
        help="Learning rate (lower for larger model)",
    )
    parser.add_argument(
        "--lora-r", type=int, default=LORA_R, help="LoRA rank"
    )
    parser.add_argument(
        "--resume-from", default=None, help="Resume training from checkpoint"
    )
    args = parser.parse_args()

    if not Path(args.data_path).exists():
        logger.error(f"Training data not found: {args.data_path}")
        sys.exit(1)

    try:
        from unsloth import FastLanguageModel
    except ImportError:
        logger.error(
            "Unsloth not installed. Run: pip install -r requirements.txt"
        )
        sys.exit(1)

    from datasets import load_dataset
    from transformers import TrainingArguments
    from trl import SFTTrainer

    logger.info(f"Loading {MODEL_NAME} with 4-bit quantization...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME,
        max_seq_length=MAX_SEQ_LENGTH,
        dtype=None,
        load_in_4bit=True,
    )

    logger.info(
        f"Configuring QLoRA: rank={args.lora_r}, alpha={LORA_ALPHA}"
    )
    model = FastLanguageModel.get_peft_model(
        model,
        r=args.lora_r,
        lora_alpha=LORA_ALPHA,
        lora_dropout=LORA_DROPOUT,
        target_modules=TARGET_MODULES,
        bias="none",
        use_gradient_checkpointing="unsloth",
    )

    logger.info(f"Loading training data from {args.data_path}")
    train_dataset = load_dataset(
        "json", data_files=args.data_path, split="train"
    )
    train_dataset = train_dataset.map(
        lambda x: format_sharegpt_to_chatml(x, tokenizer),
        remove_columns=train_dataset.column_names,
    )

    val_dataset = None
    if Path(args.val_path).exists():
        val_dataset = load_dataset(
            "json", data_files=args.val_path, split="train"
        )
        val_dataset = val_dataset.map(
            lambda x: format_sharegpt_to_chatml(x, tokenizer),
            remove_columns=val_dataset.column_names,
        )

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    training_args = TrainingArguments(
        output_dir=str(output_dir),
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        gradient_accumulation_steps=8,
        learning_rate=args.learning_rate,
        fp16=True,
        logging_steps=25,
        save_steps=250,
        save_total_limit=2,
        evaluation_strategy="steps" if val_dataset else "no",
        eval_steps=250 if val_dataset else None,
        warmup_steps=100,
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

    logger.info("Starting QLoRA fine-tuning (1.5B model)...")
    logger.info(f"  Model: {MODEL_NAME}")
    logger.info(f"  Epochs: {args.epochs}")
    logger.info(f"  Batch size: {args.batch_size} (effective: {args.batch_size * 8})")
    logger.info(f"  LoRA rank: {args.lora_r}")

    trainer.train(resume_from_checkpoint=args.resume_from)

    logger.info(f"Saving LoRA adapters to {output_dir}")
    model.save_pretrained(str(output_dir))
    tokenizer.save_pretrained(str(output_dir))

    config = {
        "base_model": MODEL_NAME,
        "lora_r": args.lora_r,
        "lora_alpha": LORA_ALPHA,
        "lora_dropout": LORA_DROPOUT,
        "target_modules": TARGET_MODULES,
        "epochs": args.epochs,
        "batch_size": args.batch_size,
        "learning_rate": args.learning_rate,
        "max_seq_length": MAX_SEQ_LENGTH,
        "training_data": args.data_path,
    }
    with open(output_dir / "training_config.json", "w") as f:
        json.dump(config, f, indent=2)

    logger.info("Training complete!")
    logger.info(f"Next: python merge_lora.py --base-model {MODEL_NAME} --adapter-path {output_dir} --output-dir ./output/qwen25-15b-merged")


if __name__ == "__main__":
    main()
