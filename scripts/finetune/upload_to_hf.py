#!/usr/bin/env python3
"""
Upload a fine-tuned ONNX model to HuggingFace Hub.

Creates or updates a HuggingFace repository, uploads all ONNX model files,
generates a model card from template, and sets appropriate tags.

Usage:
    # Using environment variable for token
    export HF_TOKEN=hf_xxxxxxxxxxxxxxxxxxxxx

    python upload_to_hf.py \
        --model-dir ./output/qwen25-05b-onnx-int4 \
        --repo-id elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling

    # With custom model card
    python upload_to_hf.py \
        --model-dir ./output/qwen25-05b-onnx-int4 \
        --repo-id elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling \
        --model-card ./scripts/finetune/model-card-template.md

    # Explicit token and custom tags
    python upload_to_hf.py \
        --model-dir ./output/qwen25-05b-onnx-int4 \
        --repo-id elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling \
        --token hf_xxxxxxxxxxxxxxxxxxxxx \
        --tags qwen2.5 onnx int4 tool-calling dotnet
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from pathlib import Path
from string import Template

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# Default tags applied to all uploads
DEFAULT_TAGS = [
    "onnx",
    "qwen2.5",
    "tool-calling",
    "local-llm",
    "dotnet",
    "elbruno",
    "onnxruntime-genai",
    "int4",
]


def _check_dependencies() -> None:
    """Verify huggingface_hub is installed."""
    try:
        import huggingface_hub  # noqa: F401
    except ImportError:
        log.error(
            "huggingface_hub is not installed.\n"
            "Install with: pip install huggingface_hub"
        )
        sys.exit(1)


def _resolve_token(token: str | None) -> str:
    """Resolve the HuggingFace token from argument or environment."""
    if token:
        return token

    env_token = os.environ.get("HF_TOKEN") or os.environ.get("HUGGING_FACE_HUB_TOKEN")
    if env_token:
        log.info("Using HuggingFace token from environment variable.")
        return env_token

    # Try the cached login token
    try:
        from huggingface_hub import HfFolder
        cached = HfFolder.get_token()
        if cached:
            log.info("Using cached HuggingFace login token.")
            return cached
    except Exception:
        pass

    log.error(
        "No HuggingFace token found.\n"
        "Provide via --token, HF_TOKEN env var, or run 'huggingface-cli login'."
    )
    sys.exit(1)


def _validate_model_dir(model_dir: Path) -> dict:
    """Validate the model directory and return model metadata."""
    if not model_dir.is_dir():
        log.error("Model directory does not exist: %s", model_dir)
        sys.exit(1)

    required = ["model.onnx", "genai_config.json"]
    for fname in required:
        if not (model_dir / fname).exists():
            log.error("Missing required file: %s", fname)
            sys.exit(1)

    # Read genai_config for metadata
    with open(model_dir / "genai_config.json") as f:
        genai_config = json.load(f)

    # Calculate total size
    total_bytes = sum(f.stat().st_size for f in model_dir.rglob("*") if f.is_file())
    total_mb = total_bytes / (1024 * 1024)

    file_count = sum(1 for f in model_dir.rglob("*") if f.is_file())
    log.info("Model directory: %d files, %.1f MB total", file_count, total_mb)

    return {
        "genai_config": genai_config,
        "total_mb": total_mb,
        "file_count": file_count,
    }


def _generate_model_card(
    template_path: Path | None,
    repo_id: str,
    model_meta: dict,
    base_model: str,
    capability: str,
    model_size: str,
    tags: list[str],
) -> str:
    """Generate a model card from template or default."""
    if template_path and template_path.exists():
        log.info("Using model card template: %s", template_path)
        raw = template_path.read_text(encoding="utf-8")

        # Use simple string replacement for template variables
        card = raw.replace("{{MODEL_NAME}}", repo_id.split("/")[-1])
        card = card.replace("{{REPO_ID}}", repo_id)
        card = card.replace("{{BASE_MODEL}}", base_model)
        card = card.replace("{{CAPABILITY}}", capability)
        card = card.replace("{{MODEL_SIZE}}", model_size)
        card = card.replace("{{SIZE_MB}}", f"{model_meta['total_mb']:.0f}")
        card = card.replace("{{TAGS}}", "\n".join(f"- {t}" for t in tags))

        return card

    # Generate a sensible default model card
    model_name = repo_id.split("/")[-1]
    tags_yaml = "\n".join(f"- {t}" for t in tags)

    return f"""---
license: apache-2.0
language:
- en
tags:
{tags_yaml}
base_model: {base_model}
model-index:
- name: {model_name}
  results: []
---

# {model_name}

Fine-tuned version of [{base_model}](https://huggingface.co/{base_model}) optimized for **{capability}** in [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs).

## Model Details

- **Base Model:** {base_model}
- **Fine-Tuning Method:** QLoRA (rank 16)
- **Format:** ONNX INT4 (ready for ONNX Runtime GenAI)
- **Size:** ~{model_meta['total_mb']:.0f} MB
- **License:** Apache 2.0

## Key Features

✅ **No Python needed** — Download and use directly in .NET
✅ **Optimized for ElBruno.LocalLLMs** — Matches QwenFormatter template exactly
✅ **Runs on CPU** — No GPU required (though faster with GPU)
✅ **Tiny model** — {model_size} parameters fit on edge devices

## Usage with ElBruno.LocalLLMs

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var options = new LocalLLMsOptions
{{
    Model = new ModelDefinition
    {{
        Id = "{model_name.lower()}",
        HuggingFaceRepoId = "{repo_id}",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        SupportsToolCalling = true
    }}
}};

using var client = await LocalChatClient.CreateAsync(options);
var response = await client.GetResponseAsync("Hello!");
```

## Limitations

- Small model with limited reasoning compared to larger models (7B+)
- English only — not trained on multilingual data
- Best with 1–3 tools; may struggle with 10+ complex tools

## Acknowledgments

- **Base Model:** [Qwen Team](https://github.com/QwenLM/Qwen2.5)
- **Training Framework:** [Unsloth](https://github.com/unslothai/unsloth)
- **ONNX Conversion:** [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai)
"""


def create_and_upload(
    model_dir: Path,
    repo_id: str,
    token: str,
    model_card_path: Path | None,
    tags: list[str],
    base_model: str,
    capability: str,
    model_size: str,
    private: bool = False,
) -> None:
    """Create/update a HuggingFace repo and upload all model files."""
    from huggingface_hub import HfApi, create_repo

    api = HfApi(token=token)

    # ── 1. Create repository ──────────────────────────────────────────────
    log.info("Creating/updating repository: %s", repo_id)
    try:
        create_repo(
            repo_id,
            repo_type="model",
            exist_ok=True,
            private=private,
            token=token,
        )
        log.info("Repository ready: https://huggingface.co/%s", repo_id)
    except Exception as e:
        log.error("Failed to create repository: %s", e)
        sys.exit(1)

    # ── 2. Validate model directory ───────────────────────────────────────
    model_meta = _validate_model_dir(model_dir)

    # ── 3. Generate model card ────────────────────────────────────────────
    model_card = _generate_model_card(
        template_path=model_card_path,
        repo_id=repo_id,
        model_meta=model_meta,
        base_model=base_model,
        capability=capability,
        model_size=model_size,
        tags=tags,
    )

    # Write model card to the model directory as README.md
    readme_path = model_dir / "README.md"
    readme_existed = readme_path.exists()
    readme_path.write_text(model_card, encoding="utf-8")
    log.info("Model card written to %s", readme_path)

    # ── 4. Upload all files ───────────────────────────────────────────────
    log.info("Uploading model files from %s ...", model_dir)
    try:
        api.upload_folder(
            folder_path=str(model_dir),
            repo_id=repo_id,
            repo_type="model",
            commit_message=f"Upload {capability} fine-tuned ONNX model",
        )
    except Exception as e:
        log.error("Upload failed: %s", e)
        # Clean up the README we wrote if it didn't exist before
        if not readme_existed and readme_path.exists():
            readme_path.unlink()
        sys.exit(1)

    # ── 5. Set repository tags ────────────────────────────────────────────
    log.info("Setting repository tags: %s", tags)
    try:
        api.update_repo_settings(
            repo_id=repo_id,
            repo_type="model",
        )
    except Exception as e:
        log.warning("Could not update repo settings (non-fatal): %s", e)

    log.info("✅ Model published: https://huggingface.co/%s", repo_id)

    # Clean up the README we wrote in the local directory
    if not readme_existed and readme_path.exists():
        readme_path.unlink()
        log.info("Cleaned up temporary README.md from model directory.")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Upload fine-tuned ONNX model to HuggingFace Hub.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python upload_to_hf.py \\\n"
            "      --model-dir ./output/qwen25-05b-onnx-int4 \\\n"
            "      --repo-id elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling\n\n"
            "  python upload_to_hf.py \\\n"
            "      --model-dir ./output/qwen25-15b-onnx-int4 \\\n"
            "      --repo-id elbruno/Qwen2.5-1.5B-LocalLLMs-RAG \\\n"
            "      --capability RAG \\\n"
            "      --base-model Qwen/Qwen2.5-1.5B-Instruct \\\n"
            "      --model-size 1.5B \\\n"
            "      --model-card ./scripts/finetune/model-card-template.md"
        ),
    )
    parser.add_argument(
        "--model-dir",
        required=True,
        help="Path to the ONNX model directory to upload.",
    )
    parser.add_argument(
        "--repo-id",
        required=True,
        help="HuggingFace repository ID (e.g., elbruno/Qwen2.5-0.5B-LocalLLMs-ToolCalling).",
    )
    parser.add_argument(
        "--token",
        default=None,
        help="HuggingFace API token. Falls back to HF_TOKEN env var or cached login.",
    )
    parser.add_argument(
        "--model-card",
        default=None,
        help="Path to a model card template (.md). If not provided, a default card is generated.",
    )
    parser.add_argument(
        "--tags",
        nargs="*",
        default=None,
        help="Custom tags for the repository. Defaults to a standard set "
             "(onnx, qwen2.5, tool-calling, etc.).",
    )
    parser.add_argument(
        "--base-model",
        default="Qwen/Qwen2.5-0.5B-Instruct",
        help="Base model HuggingFace ID (default: Qwen/Qwen2.5-0.5B-Instruct).",
    )
    parser.add_argument(
        "--capability",
        default="ToolCalling",
        choices=["ToolCalling", "RAG", "Instruct"],
        help="Fine-tuning capability (default: ToolCalling).",
    )
    parser.add_argument(
        "--model-size",
        default="0.5B",
        help="Model parameter count for display (default: 0.5B).",
    )
    parser.add_argument(
        "--private",
        action="store_true",
        help="Create the repository as private.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    _check_dependencies()

    token = _resolve_token(args.token)
    model_dir = Path(args.model_dir)
    model_card_path = Path(args.model_card) if args.model_card else None
    tags = args.tags if args.tags else DEFAULT_TAGS

    create_and_upload(
        model_dir=model_dir,
        repo_id=args.repo_id,
        token=token,
        model_card_path=model_card_path,
        tags=tags,
        base_model=args.base_model,
        capability=args.capability,
        model_size=args.model_size,
        private=args.private,
    )


if __name__ == "__main__":
    main()
