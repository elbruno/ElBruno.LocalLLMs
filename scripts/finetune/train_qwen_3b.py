#!/usr/bin/env python3
"""
Fine-tune Qwen2.5-3B-Instruct for ElBruno.LocalLLMs using QLoRA.

Largest model in the fine-tuning pipeline. Requires careful memory
management — may need cloud A100 GPU for comfortable training.

Usage:
    python train_qwen_3b.py --output-dir ./output/qwen25-3b-finetuned

Hardware Requirements:
    - GPU: RTX 4090 (24 GB, very tight) or A100 (40/80 GB recommended)
    - RAM: 64 GB system memory recommended
    - Disk: 100 GB free space
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

MODEL_NAME = "Qwen/Qwen2.5-3B-Instruct"
MAX_SEQ_LENGTH = 2048

# QLoRA hyperparameters (optimized for Qwen2.5-3B)
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
        description="Fine-tune Qwen2.5-3B-Instruct with QLoRA"
    )
    parser.add_argument(
        "--output-dir",
        default="./output/qwen25-3b-finetuned",
        help="Directory to save the fine-tuned LoRA adapters",
    )
    parser.add_argument(
        "--data-path",
        default="./training-data/combined-train.json",
        help="Path to training data (ShareGPT JSON format)",
    )
    parser.add_argument(
        "--val-path",
        default="./training-data/validation.json",
        help="Path to validation data (ShareGPT JSON format)",
    )
    parser.add_argument(
        "--epochs", type=int, default=3, help="Number of training epochs"
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=1,
        help="Per-device batch size (minimal for 3B on 24GB)",
    )
    parser.add_argument(
        "--learning-rate",
        type=float,
        default=1e-4,
        help="Learning rate",
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

    # 3B needs aggressive gradient accumulation to compensate
    # for the small batch size
    training_args = TrainingArguments(
        output_dir=str(output_dir),
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        gradient_accumulation_steps=16,
        learning_rate=args.learning_rate,
        fp16=True,
        logging_steps=10,
        save_steps=200,
        save_total_limit=2,
        evaluation_strategy="steps" if val_dataset else "no",
        eval_steps=200 if val_dataset else None,
        warmup_steps=100,
        lr_scheduler_type="cosine",
        optim="paged_adamw_8bit",
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

    logger.info("Starting QLoRA fine-tuning (3B model)...")
    logger.info(f"  Model: {MODEL_NAME}")
    logger.info(f"  Epochs: {args.epochs}")
    logger.info(f"  Batch size: {args.batch_size} (effective: {args.batch_size * 16})")
    logger.info(f"  LoRA rank: {args.lora_r}")
    logger.info("  NOTE: 3B on RTX 4090 is tight. If OOM, use cloud A100.")

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
    logger.info(f"Next: python merge_lora.py --base-model {MODEL_NAME} --adapter-path {output_dir} --output-dir ./output/qwen25-3b-merged")


if __name__ == "__main__":
    main()
