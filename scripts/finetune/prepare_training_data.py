#!/usr/bin/env python3
"""
Prepare training data for ElBruno.LocalLLMs fine-tuning.

Downloads source datasets from HuggingFace, converts them to the
ShareGPT format matching QwenFormatter's expected output, merges
all categories, and creates train/validation splits.

Usage:
    python prepare_training_data.py --output-dir ./training-data
    python prepare_training_data.py --skip-download --output-dir ./training-data

Sources:
    - Glaive Function Calling v2 (tool calling)
    - Stanford Alpaca (instruction following)
    - Custom examples (in training-data/*.json)
"""

import argparse
import json
import logging
import random
import re
import sys
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)


def convert_glaive_to_sharegpt(example: dict) -> dict | None:
    """Convert a Glaive function calling example to ShareGPT format.

    Glaive format uses 'system', 'chat' fields.
    We convert to ShareGPT conversations with proper <tool_call> tags.
    """
    try:
        conversations = []
        system_msg = example.get("system", "You are a helpful assistant.")
        conversations.append({"from": "system", "value": system_msg})

        chat = example.get("chat", "")
        if not chat:
            return None

        # Parse the chat field (alternating USER/ASSISTANT/FUNCTION RESPONSE)
        turns = re.split(
            r"(USER:|ASSISTANT:|FUNCTION RESPONSE:)", chat
        )
        turns = [t.strip() for t in turns if t.strip()]

        i = 0
        while i < len(turns):
            if turns[i] == "USER:" and i + 1 < len(turns):
                conversations.append(
                    {"from": "human", "value": turns[i + 1]}
                )
                i += 2
            elif turns[i] == "ASSISTANT:" and i + 1 < len(turns):
                content = turns[i + 1]
                # Convert function calls to <tool_call> format
                fc_match = re.search(
                    r'<functioncall>\s*(\{.*?\})', content, re.DOTALL
                )
                if fc_match:
                    try:
                        fc_json = json.loads(fc_match.group(1))
                        tool_call = {
                            "name": fc_json.get("name", ""),
                            "arguments": fc_json.get("arguments", {}),
                        }
                        content = (
                            f"<tool_call>\n"
                            f"{json.dumps(tool_call)}\n"
                            f"</tool_call>"
                        )
                    except json.JSONDecodeError:
                        i += 2
                        continue
                conversations.append({"from": "gpt", "value": content})
                i += 2
            elif turns[i] == "FUNCTION RESPONSE:" and i + 1 < len(turns):
                conversations.append(
                    {"from": "human", "value": f"Tool result: {turns[i + 1]}"}
                )
                i += 2
            else:
                i += 1

        if len(conversations) < 3:
            return None

        return {"conversations": conversations}
    except Exception:
        return None


def convert_alpaca_to_sharegpt(example: dict) -> dict | None:
    """Convert an Alpaca format example to ShareGPT format."""
    try:
        instruction = example.get("instruction", "")
        input_text = example.get("input", "")
        output = example.get("output", "")

        if not instruction or not output:
            return None
        if len(output) > 2000:
            return None

        conversations = [
            {"from": "system", "value": "You are a helpful assistant."},
        ]

        user_msg = instruction
        if input_text:
            user_msg = f"{instruction}\n\n{input_text}"

        conversations.append({"from": "human", "value": user_msg})
        conversations.append({"from": "gpt", "value": output})

        return {"conversations": conversations}
    except Exception:
        return None


def download_and_convert_glaive(max_examples: int = 1500) -> list[dict]:
    """Download Glaive function calling v2 and convert to ShareGPT."""
    try:
        from datasets import load_dataset

        logger.info("Downloading Glaive Function Calling v2...")
        ds = load_dataset(
            "glaiveai/glaive-function-calling-v2", split="train"
        )

        converted = []
        for example in ds:
            result = convert_glaive_to_sharegpt(example)
            if result:
                converted.append(result)
            if len(converted) >= max_examples:
                break

        logger.info(f"Converted {len(converted)} Glaive examples")
        return converted
    except Exception as e:
        logger.warning(f"Failed to download Glaive dataset: {e}")
        logger.warning("Continuing with custom examples only.")
        return []


def download_and_convert_alpaca(max_examples: int = 1000) -> list[dict]:
    """Download Stanford Alpaca and convert to ShareGPT."""
    try:
        from datasets import load_dataset

        logger.info("Downloading Stanford Alpaca dataset...")
        ds = load_dataset("tatsu-lab/alpaca", split="train")

        converted = []
        for example in ds:
            result = convert_alpaca_to_sharegpt(example)
            if result:
                converted.append(result)
            if len(converted) >= max_examples:
                break

        logger.info(f"Converted {len(converted)} Alpaca examples")
        return converted
    except Exception as e:
        logger.warning(f"Failed to download Alpaca dataset: {e}")
        logger.warning("Continuing with custom examples only.")
        return []


def load_custom_examples(data_dir: Path) -> dict[str, list]:
    """Load custom training examples from JSON files."""
    categories = {}
    for name in [
        "tool-calling-train",
        "rag-grounded-train",
        "chat-template-train",
    ]:
        path = data_dir / f"{name}.json"
        if path.exists():
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            categories[name] = data
            logger.info(f"Loaded {len(data)} custom examples from {name}")
        else:
            logger.warning(f"Custom data not found: {path}")
            categories[name] = []
    return categories


def validate_example(example: dict) -> bool:
    """Validate a training example has correct ShareGPT structure."""
    if "conversations" not in example:
        return False
    convs = example["conversations"]
    if not isinstance(convs, list) or len(convs) < 2:
        return False
    for turn in convs:
        if "from" not in turn or "value" not in turn:
            return False
        if turn["from"] not in ("system", "human", "gpt"):
            return False
    return True


def main():
    parser = argparse.ArgumentParser(
        description="Prepare training data for fine-tuning"
    )
    parser.add_argument(
        "--output-dir",
        default="./training-data",
        help="Directory to write output files",
    )
    parser.add_argument(
        "--skip-download",
        action="store_true",
        help="Skip downloading external datasets (use custom only)",
    )
    parser.add_argument(
        "--glaive-max",
        type=int,
        default=1500,
        help="Max Glaive examples to convert",
    )
    parser.add_argument(
        "--alpaca-max",
        type=int,
        default=1000,
        help="Max Alpaca examples to convert",
    )
    parser.add_argument(
        "--seed", type=int, default=42, help="Random seed for shuffling"
    )
    parser.add_argument(
        "--val-ratio",
        type=float,
        default=0.1,
        help="Validation split ratio",
    )
    args = parser.parse_args()

    random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Load custom examples
    custom = load_custom_examples(output_dir)

    # Download external datasets
    external_tool = []
    external_chat = []
    if not args.skip_download:
        external_tool = download_and_convert_glaive(args.glaive_max)
        external_chat = download_and_convert_alpaca(args.alpaca_max)

    # Merge by category
    tool_calling = custom.get("tool-calling-train", []) + external_tool
    rag = custom.get("rag-grounded-train", [])
    chat = custom.get("chat-template-train", []) + external_chat

    # Validate all examples
    for name, data in [
        ("tool-calling", tool_calling),
        ("rag", rag),
        ("chat", chat),
    ]:
        valid = [ex for ex in data if validate_example(ex)]
        invalid_count = len(data) - len(valid)
        if invalid_count > 0:
            logger.warning(
                f"{name}: {invalid_count} invalid examples removed"
            )
        if name == "tool-calling":
            tool_calling = valid
        elif name == "rag":
            rag = valid
        else:
            chat = valid

    # Combine all
    combined = tool_calling + rag + chat
    random.shuffle(combined)

    # Split train/validation
    split_idx = int(len(combined) * (1 - args.val_ratio))
    train_data = combined[:split_idx]
    val_data = combined[split_idx:]

    # Write output files
    def write_json(path: Path, data: list):
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

    write_json(output_dir / "combined-train.json", train_data)
    write_json(output_dir / "validation.json", val_data)

    # Summary
    logger.info("=" * 50)
    logger.info("Training Data Summary")
    logger.info("=" * 50)
    logger.info(f"Tool calling:  {len(tool_calling)} examples")
    logger.info(f"RAG:           {len(rag)} examples")
    logger.info(f"Chat:          {len(chat)} examples")
    logger.info(f"Total:         {len(combined)} examples")
    logger.info(f"Train split:   {len(train_data)} examples")
    logger.info(f"Val split:     {len(val_data)} examples")
    logger.info(f"Output dir:    {output_dir}")
    logger.info("=" * 50)


if __name__ == "__main__":
    main()
