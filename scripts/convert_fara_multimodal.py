#!/usr/bin/env python3
"""
Full multimodal ONNX export for microsoft/Fara1.5-9B.

Fara is a VisionGenAI model (Qwen3.5-VL fine-tune) that requires three ONNX
components to work with ORT-GenAI's multimodal processor:

  qwen3vl-vision.onnx    — Fara's vision encoder (FP32)
  qwen3vl-embedding.onnx — token embedding + vision token injection (FP32)
  model.onnx             — text decoder (INT4, already in elbruno/Fara1.5-9B-onnx)

The onnxruntime-genai built-in builder can only produce the text decoder for
Fara (Qwen3_5 architecture), so this script fills the two gaps using
torch.onnx.export, then wires genai_config.json to use model.type=qwen3_vl
so the ORT-GenAI runtime uses the full VLM pipeline.

Usage:
    # Export vision + embedding, patch genai_config, then upload:
    python convert_fara_multimodal.py --fara-dir ./fara-pytorch --onnx-dir ./fara-onnx-multimodal

    # Reuse existing decoder in published HF repo and only build the two missing files:
    python convert_fara_multimodal.py --fara-dir ./fara-pytorch --onnx-dir ./fara-onnx-multimodal --skip-upload

Requirements:
    pip install -r scripts/requirements.txt
    pip install torch>=2.1.0 transformers>=5.14.0
"""

import argparse
import json
import os
import shutil
import sys
from pathlib import Path

import torch
import torch.nn as nn

TARGET_HF_REPO = "elbruno/Fara1.5-9B-onnx"
SOURCE_MODEL_ID = "microsoft/Fara1.5-9B"

# Fara's image placeholder token id
FARA_IMAGE_TOKEN_ID = 248056

# vision_processor.json preprocessing pipeline
# Same normalization as Qwen3VL (mean=0.5, std=0.5)
VISION_PROCESSOR_CONFIG = {
    "processor": {
        "name": "qwen3_vl_vision_processor",
        "transforms": [
            {"operation": {"name": "decode_image", "type": "DecodeImage", "attrs": {"color_space": "RGB"}}},
            {"operation": {"name": "rescale", "type": "Rescale"}},
            {
                "operation": {
                    "name": "normalize",
                    "type": "Normalize",
                    "attrs": {"mean": [0.5, 0.5, 0.5], "std": [0.5, 0.5, 0.5]},
                }
            },
        ],
    }
}

# genai_config.json vision + embedding sections to add/replace
VISION_SECTION = {
    "filename": "qwen3vl-vision.onnx",
    "config_filename": "vision_processor.json",
    "inputs": {"pixel_values": "pixel_values", "image_grid_thw": "image_grid_thw"},
    "outputs": {"image_features": "pooled_embeds"},
    "spatial_merge_size": 2,
}

EMBEDDING_SECTION = {
    "filename": "qwen3vl-embedding.onnx",
    "inputs": {"input_ids": "input_ids", "image_features": "vision_hidden_states"},
    "outputs": {"inputs_embeds": "inputs_embeds"},
}


# ── Wrappers ──────────────────────────────────────────────────────────────────


class VisionEncoderWrapper(nn.Module):
    """Exports Fara's vision encoder as a standalone ONNX graph.

    Input:  pixel_values  [num_patches, patch_dim]
            image_grid_thw [num_images, 3]  (temporal, height, width)
    Output: pooled_embeds  [sequence, out_hidden_size]
    """

    def __init__(self, visual_model):
        super().__init__()
        self.visual = visual_model

    def forward(self, pixel_values, image_grid_thw):
        outputs = self.visual(pixel_values, grid_thw=image_grid_thw, return_dict=True)
        if hasattr(outputs, "pooler_output"):
            return outputs.pooler_output
        if isinstance(outputs, dict):
            return outputs["pooler_output"]
        return outputs[0]


class EmbeddingInjectorWrapper(nn.Module):
    """Exports Fara's token embedding + vision feature injection.

    Replaces <|image_pad|> token positions with vision features from the
    vision encoder, then returns full inputs_embeds for the decoder.

    Input:  input_ids          [batch, sequence]
            vision_hidden_states [vision_seq, hidden_size]
    Output: inputs_embeds      [batch, sequence, hidden_size]
    """

    def __init__(self, embed_tokens, image_token_id):
        super().__init__()
        self.embed_tokens = embed_tokens
        self.image_token_id = image_token_id

    def forward(self, input_ids, vision_hidden_states):
        inputs_embeds = self.embed_tokens(input_ids)
        B, N, C = inputs_embeds.shape
        inputs_embeds = inputs_embeds.reshape(B * N, C)

        vision_mask = (input_ids.view(-1) == self.image_token_id).unsqueeze(-1).expand(-1, C)
        inputs_embeds = inputs_embeds.masked_scatter(vision_mask, vision_hidden_states.reshape(-1))

        return inputs_embeds.reshape(B, N, C)


# ── Export functions ───────────────────────────────────────────────────────────


def export_vision_encoder(fara_model, output_dir: Path) -> None:
    """Export vision encoder to qwen3vl-vision.onnx."""
    print("\n[2/4] Exporting vision encoder → qwen3vl-vision.onnx ...")

    visual_model = fara_model.model.visual
    wrapper = VisionEncoderWrapper(visual_model)

    # Representative single-image dummy tensors
    # num_patches = 24*24 = 576 tokens; patch_dim = in_channels * temporal_patch * patch^2 = 3*2*16*16 = 1536
    num_patches = 576
    patch_dim = 1536  # 3 * 2 * 16 * 16
    pixel_values = torch.randn(num_patches, patch_dim, dtype=torch.float32)
    image_grid_thw = torch.tensor([[1, 24, 24]], dtype=torch.int64)

    output_path = output_dir / "qwen3vl-vision.onnx"

    with torch.no_grad():
        torch.onnx.export(
            wrapper,
            (pixel_values, image_grid_thw),
            str(output_path),
            input_names=["pixel_values", "image_grid_thw"],
            output_names=["pooled_embeds"],
            dynamic_axes={
                "pixel_values": {0: "num_patches"},
                "image_grid_thw": {0: "num_images"},
                "pooled_embeds": {0: "sequence"},
            },
            opset_version=18,
        )

    size_mb = output_path.stat().st_size / (1024 ** 2)
    print(f"  OK  qwen3vl-vision.onnx  ({size_mb:.1f} MB)")


def export_embedding_injector(fara_model, output_dir: Path, hidden_size: int) -> None:
    """Export token embedding + vision injection to qwen3vl-embedding.onnx."""
    print("\n[3/4] Exporting embedding injector → qwen3vl-embedding.onnx ...")

    embed_tokens = fara_model.model.language_model.embed_tokens
    wrapper = EmbeddingInjectorWrapper(embed_tokens, FARA_IMAGE_TOKEN_ID)

    # Dummy: batch=1, sequence=200 with 144 image token positions
    input_ids = torch.randint(0, 1000, (1, 200), dtype=torch.long)
    input_ids[0, 28:172] = FARA_IMAGE_TOKEN_ID  # 144 image tokens
    vision_hidden_states = torch.randn(144, hidden_size, dtype=torch.float32)

    output_path = output_dir / "qwen3vl-embedding.onnx"

    with torch.no_grad():
        torch.onnx.export(
            wrapper,
            (input_ids, vision_hidden_states),
            str(output_path),
            input_names=["input_ids", "vision_hidden_states"],
            output_names=["inputs_embeds"],
            dynamic_axes={
                "input_ids": {0: "batch", 1: "sequence"},
                "vision_hidden_states": {0: "vision_sequence"},
                "inputs_embeds": {0: "batch", 1: "sequence"},
            },
            opset_version=18,
        )

    size_mb = output_path.stat().st_size / (1024 ** 2)
    print(f"  OK  qwen3vl-embedding.onnx  ({size_mb:.1f} MB)")


def create_vision_processor_json(output_dir: Path) -> None:
    """Write vision_processor.json for ORT-GenAI multimodal preprocessing."""
    path = output_dir / "vision_processor.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(VISION_PROCESSOR_CONFIG, f, indent=2)
    print("  OK  vision_processor.json")


def patch_genai_config(output_dir: Path) -> None:
    """Update genai_config.json: model.type → qwen3_vl, add vision+embedding sections."""
    config_path = output_dir / "genai_config.json"
    if not config_path.exists():
        print(f"  SKIP  genai_config.json not found in {output_dir}")
        return

    with open(config_path, encoding="utf-8") as f:
        config = json.load(f)

    config["model"]["type"] = "qwen3_vl"
    config["model"]["vision"] = VISION_SECTION
    config["model"]["embedding"] = EMBEDDING_SECTION

    # Cap context length if needed
    ctx = config.get("model", {}).get("context_length", 0)
    if ctx == 0 or ctx > 32768:
        config["model"]["context_length"] = 32768
    search_max = config.get("search", {}).get("max_length", 0)
    if search_max == 0 or search_max > 32768:
        config.setdefault("search", {})["max_length"] = 32768

    with open(config_path, "w", encoding="utf-8") as f:
        json.dump(config, f, indent=2)
    print("  OK  genai_config.json  (model.type=qwen3_vl, vision/embedding sections added)")


def validate_output(output_dir: Path) -> bool:
    """Verify all required multimodal ONNX files are present."""
    required = [
        "model.onnx",
        "qwen3vl-vision.onnx",
        "qwen3vl-embedding.onnx",
        "genai_config.json",
        "vision_processor.json",
        "tokenizer.json",
        "processor_config.json",
    ]
    all_ok = True
    print("\n-- Validation --------------------------------------------------------")
    for fname in required:
        p = output_dir / fname
        if p.exists():
            size_mb = p.stat().st_size / (1024 ** 2)
            print(f"  OK  {fname}  ({size_mb:.1f} MB)")
        else:
            print(f"  MISSING  {fname}")
            all_ok = False

    # Confirm genai_config has correct model.type
    config_path = output_dir / "genai_config.json"
    if config_path.exists():
        with open(config_path, encoding="utf-8") as f:
            config = json.load(f)
        model_type = config.get("model", {}).get("type", "")
        if model_type == "qwen3_vl":
            print("  OK  genai_config.json model.type = 'qwen3_vl'")
        else:
            print(f"  FAIL  genai_config.json model.type = '{model_type}' (expected 'qwen3_vl')")
            all_ok = False

    return all_ok


def upload_to_hf(output_dir: Path) -> None:
    """Upload complete multimodal package to elbruno/Fara1.5-9B-onnx."""
    print(f"\n-- Uploading to {TARGET_HF_REPO} ...")
    try:
        from huggingface_hub import HfApi, create_repo

        api = HfApi()
        create_repo(TARGET_HF_REPO, repo_type="model", exist_ok=True)
        api.upload_folder(
            folder_path=str(output_dir),
            repo_id=TARGET_HF_REPO,
            repo_type="model",
            commit_message="Multimodal Fara ONNX: add qwen3vl-vision.onnx, qwen3vl-embedding.onnx, vision_processor.json; set model.type=qwen3_vl",
        )
        print(f"  OK  uploaded to https://huggingface.co/{TARGET_HF_REPO}")
    except Exception as e:
        print(f"  ERROR: {e}")
        sys.exit(1)


# ── Main ──────────────────────────────────────────────────────────────────────


def main():
    parser = argparse.ArgumentParser(
        description="Export Fara1.5-9B multimodal ONNX package (vision encoder + embedding injector)."
    )
    parser.add_argument("--fara-dir", default="./cache_dir/fara-work/fara-pytorch",
                        help="Local PyTorch weights directory (auto-downloaded if absent)")
    parser.add_argument("--onnx-dir", default="./cache_dir/fara-work/fara-onnx-multimodal",
                        help="Output directory for the complete ONNX package")
    parser.add_argument("--reuse-decoder", default=None,
                        help="Optional: path to existing decoder-only ONNX dir to copy model.onnx + config from")
    parser.add_argument("--skip-upload", action="store_true",
                        help="Skip Hugging Face upload; just build locally")
    parser.add_argument("--precision", default="int4", choices=["int4", "int8", "fp32"],
                        help="Precision hint (informational; decoder is already exported)")
    args = parser.parse_args()

    fara_dir = Path(args.fara_dir)
    onnx_dir = Path(args.onnx_dir)
    onnx_dir.mkdir(parents=True, exist_ok=True)

    print("=" * 72)
    print("Fara1.5-9B Multimodal ONNX Export")
    print("=" * 72)
    print(f"  PyTorch source : {fara_dir}")
    print(f"  ONNX output    : {onnx_dir}")
    if args.reuse_decoder:
        print(f"  Reuse decoder  : {args.reuse_decoder}")

    # ── Step 1: Download Fara PyTorch weights if needed ──────────────────────
    if not fara_dir.exists() or not list(fara_dir.glob("*.safetensors")):
        print(f"\n[1/4] Downloading {SOURCE_MODEL_ID} (~18 GB) ...")
        try:
            from huggingface_hub import snapshot_download
            snapshot_download(repo_id=SOURCE_MODEL_ID, local_dir=str(fara_dir))
        except Exception as e:
            print(f"  ERROR: {e}")
            sys.exit(1)
    else:
        n = len(list(fara_dir.glob("*.safetensors")))
        print(f"\n[1/4] PyTorch weights present ({n} safetensors shards) — skipping download.")

    # ── Optionally copy decoder files from an existing ONNX dir ──────────────
    if args.reuse_decoder:
        src = Path(args.reuse_decoder)
        copy_files = ["model.onnx", "model.onnx.data", "genai_config.json",
                      "tokenizer.json", "tokenizer_config.json", "processor_config.json",
                      "preprocessor_config.json", "video_preprocessor_config.json",
                      "config.json", "chat_template.jinja"]
        print(f"\n  Copying decoder files from {src} ...")
        for fname in copy_files:
            s = src / fname
            if s.exists():
                shutil.copy2(s, onnx_dir / fname)
                print(f"  Copied {fname}")

    # ── Step 2: Load Fara model (weights NOT loaded for tensor export) ────────
    print("\n[2/4] Loading Fara1.5-9B PyTorch model (this takes a while — ~18 GB weights) ...")
    try:
        from transformers import Qwen3_5ForConditionalGeneration

        fara_model = Qwen3_5ForConditionalGeneration.from_pretrained(
            str(fara_dir),
            torch_dtype=torch.float32,
            attn_implementation="eager",
        ).to("cpu")
        fara_model.eval()
        print("  OK  model loaded")
    except Exception as e:
        print(f"  ERROR loading model: {e}")
        sys.exit(1)

    # ── Step 3: Export vision encoder ────────────────────────────────────────
    export_vision_encoder(fara_model, onnx_dir)

    # ── Step 4: Export embedding injector ────────────────────────────────────
    hidden_size = fara_model.config.text_config.hidden_size  # 4096
    export_embedding_injector(fara_model, onnx_dir, hidden_size)

    # Free model memory after export
    del fara_model
    import gc
    gc.collect()

    # ── Step 5: Wire genai_config.json ────────────────────────────────────────
    print("\n[4/4] Creating configuration files ...")
    patch_genai_config(onnx_dir)
    create_vision_processor_json(onnx_dir)

    # ── Step 6: Validate ─────────────────────────────────────────────────────
    ok = validate_output(onnx_dir)
    if not ok:
        print("\n  ERROR: Validation failed — check missing files above.")
        sys.exit(1)

    print("\n  All validation checks passed.")

    # ── Step 7: Upload ────────────────────────────────────────────────────────
    if not args.skip_upload:
        upload_to_hf(onnx_dir)

    print("\n" + "=" * 72)
    print("Export complete!")
    print("=" * 72)
    print(f"\nOutput: {onnx_dir}")
    print()
    print("ONNX files:")
    print("  qwen3vl-vision.onnx      (FP32, vision encoder)")
    print("  qwen3vl-embedding.onnx   (FP32, embedding injector)")
    print(f"  model.onnx               ({args.precision.upper()}, text decoder)")
    print()
    print("To test with ElBruno.LocalLLMs:")
    print("  dotnet run --project src/samples/FaraVisionAgent -- --model-path " + str(onnx_dir))


if __name__ == "__main__":
    main()
