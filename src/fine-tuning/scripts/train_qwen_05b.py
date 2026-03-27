#!/usr/bin/env python3
"""
Fine-tune Qwen2.5-0.5B-Instruct for ElBruno.LocalLLMs using QLoRA.

Produces a fine-tuned model optimized for tool calling, RAG grounded
answering, and chat template adherence with the library's QwenFormatter.

Usage:
    python train_qwen_05b.py --output-dir ./output/qwen25-05b-finetuned
    python train_qwen_05b.py --data-path ../training-data/combined-train.json

Hardware Requirements:
    - GPU: RTX 4090 (24 GB VRAM) or A100 (40/80 GB)
    - RAM: 32 GB system memory
    - Disk: 50 GB free space
"""

import argparse
import json
import logging
import os
import sys
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)

MODEL_NAME = "Qwen/Qwen2.5-0.5B-Instruct"
MAX_SEQ_LENGTH = 2048

# QLoRA hyperparameters (optimized for Qwen2.5-0.5B)
LORA_R = 16
LORA_ALPHA = 32
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
        description="Fine-tune Qwen2.5-0.5B-Instruct with QLoRA"
    )
    parser.add_argument(
        "--output-dir",
        default="./output/qwen25-05b-finetuned",
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
        "--batch-size", type=int, default=4, help="Per-device batch size"
    )
    parser.add_argument(
        "--learning-rate", type=float, default=2e-4, help="Learning rate"
    )
    parser.add_argument(
        "--lora-r", type=int, default=LORA_R, help="LoRA rank"
    )
    parser.add_argument(
        "--resume-from", default=None, help="Resume training from checkpoint"
    )
    args = parser.parse_args()

    # Validate input files
    if not Path(args.data_path).exists():
        logger.error(f"Training data not found: {args.data_path}")
        logger.error("Run prepare_training_data.py first or check the path.")
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

    # Load base model with 4-bit quantization
    logger.info(f"Loading {MODEL_NAME} with 4-bit quantization...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME,
        max_seq_length=MAX_SEQ_LENGTH,
        dtype=None,
        load_in_4bit=True,
    )

    # Configure QLoRA adapters
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

    # Load and format datasets
    logger.info(f"Loading training data from {args.data_path}")
    train_dataset = load_dataset(
        "json", data_files=args.data_path, split="train"
    )
    train_dataset = train_dataset.map(
        lambda x: format_sharegpt_to_chatml(x, tokenizer),
        remove_columns=train_dataset.column_names,
    )
    logger.info(f"Training examples: {len(train_dataset)}")

    val_dataset = None
    if Path(args.val_path).exists():
        val_dataset = load_dataset(
            "json", data_files=args.val_path, split="train"
        )
        val_dataset = val_dataset.map(
            lambda x: format_sharegpt_to_chatml(x, tokenizer),
            remove_columns=val_dataset.column_names,
        )
        logger.info(f"Validation examples: {len(val_dataset)}")

    # Training arguments
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    training_args = TrainingArguments(
        output_dir=str(output_dir),
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        gradient_accumulation_steps=4,
        learning_rate=args.learning_rate,
        fp16=True,
        logging_steps=50,
        save_steps=500,
        save_total_limit=2,
        evaluation_strategy="steps" if val_dataset else "no",
        eval_steps=500 if val_dataset else None,
        warmup_steps=50,
        lr_scheduler_type="cosine",
        optim="paged_adamw_8bit",
        weight_decay=0.01,
        load_best_model_at_end=bool(val_dataset),
        metric_for_best_model="eval_loss" if val_dataset else None,
        report_to="none",
    )

    # Create trainer
    trainer = SFTTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
        dataset_text_field="text",
        max_seq_length=MAX_SEQ_LENGTH,
        args=training_args,
    )

    # Train
    logger.info("Starting QLoRA fine-tuning...")
    logger.info(f"  Model: {MODEL_NAME}")
    logger.info(f"  Epochs: {args.epochs}")
    logger.info(f"  Batch size: {args.batch_size} (effective: {args.batch_size * 4})")
    logger.info(f"  Learning rate: {args.learning_rate}")
    logger.info(f"  LoRA rank: {args.lora_r}")

    resume_from = args.resume_from
    trainer.train(resume_from_checkpoint=resume_from)

    # Save LoRA adapters
    logger.info(f"Saving LoRA adapters to {output_dir}")
    model.save_pretrained(str(output_dir))
    tokenizer.save_pretrained(str(output_dir))

    # Save training config for reproducibility
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
    config_path = output_dir / "training_config.json"
    with open(config_path, "w") as f:
        json.dump(config, f, indent=2)

    logger.info("Training complete!")
    logger.info(f"LoRA adapters saved to: {output_dir}")
    logger.info(f"Next step: python merge_lora.py --base-model {MODEL_NAME} --adapter-path {output_dir} --output-dir ./output/qwen25-05b-merged")


if __name__ == "__main__":
    main()
