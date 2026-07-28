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

# ORT-GenAI multimodal image processor for Qwen3-VL/Fara.
# This must match the runtime's expected patching pipeline so pixel_values are
# emitted as [num_images, num_patches, patch_dim] with patch_dim=1536.
VISION_PROCESSOR_CONFIG = {
    "processor": {
        "name": "qwen3_vl_image_processor",
        "transforms": [
            {"operation": {"name": "decode_image", "type": "DecodeImage", "attrs": {"color_space": "RGB"}}},
            {"operation": {"name": "convert_to_rgb", "type": "ConvertRGB"}},
            {
                "operation": {
                    "name": "resize",
                    "type": "Resize",
                    "attrs": {
                        "width": 540,
                        "height": 360,
                        "smart_resize": 1,
                        "min_pixels": 3136,
                        "max_pixels": 12845056,
                        "patch_size": 16,
                        "merge_size": 2,
                    },
                }
            },
            {
                "operation": {
                    "name": "rescale",
                    "type": "Rescale",
                    "attrs": {"rescale_factor": 0.00392156862745098},
                }
            },
            {
                "operation": {
                    "name": "normalize",
                    "type": "Normalize",
                    "attrs": {"mean": [0.5, 0.5, 0.5], "std": [0.5, 0.5, 0.5], "qwen3_vl": 1},
                }
            },
            {
                "operation": {
                    "name": "patch_image",
                    "type": "PatchImage",
                    "attrs": {"patch_size": 16, "temporal_patch_size": 2, "merge_size": 2},
                }
            },
        ],
    }
}

# genai_config.json vision + embedding sections to add/replace
VISION_SECTION = {
    "filename": "qwen3vl-vision.onnx",
    "config_filename": "processor_config.json",
    "inputs": {"pixel_values": "pixel_values", "image_grid_thw": "image_grid_thw"},
    "outputs": {"image_features": "pooled_embeds"},
    "spatial_merge_size": 2,
}

EMBEDDING_SECTION = {
    "filename": "qwen3vl-embedding.onnx",
    "inputs": {"input_ids": "input_ids", "image_features": "vision_hidden_states"},
    "outputs": {"inputs_embeds": "inputs_embeds"},
}


def patch_qwen35_vision_for_onnx_export() -> None:
    """Monkey-patch tracing-safe Qwen3.5 vision forward paths for ONNX export."""
    from transformers.models.qwen3_5 import modeling_qwen3_5 as q35

    if getattr(q35.Qwen3_5VisionModel, "_elbruno_onnx_patch", False):
        return

    original_attention_forward = q35.Qwen3_5VisionAttention.forward
    original_vision_forward = q35.Qwen3_5VisionModel.forward

    def patched_attention_forward(self, hidden_states, cu_seqlens, position_embeddings=None, **kwargs):
        seq_length = hidden_states.shape[0]
        query_states, key_states, value_states = (
            self.qkv(hidden_states).reshape(seq_length, 3, self.num_heads, -1).permute(1, 0, 2, 3).unbind(0)
        )

        cos, sin = position_embeddings
        query_states, key_states = q35.apply_rotary_pos_emb_vision(query_states, key_states, cos, sin)

        query_states = query_states.transpose(0, 1).unsqueeze(0)
        key_states = key_states.transpose(0, 1).unsqueeze(0)
        value_states = value_states.transpose(0, 1).unsqueeze(0)

        attention_interface = q35.ALL_ATTENTION_FUNCTIONS.get_interface(
            self.config._attn_implementation, q35.eager_attention_forward
        )

        if q35.is_flash_attention_requested(self.config):
            max_seqlen = (cu_seqlens[1:] - cu_seqlens[:-1]).max()
            attn_output, _ = attention_interface(
                self,
                query_states,
                key_states,
                value_states,
                attention_mask=None,
                scaling=self.scaling,
                dropout=0.0 if not self.training else self.attention_dropout,
                cu_seq_lens_q=cu_seqlens,
                cu_seq_lens_k=cu_seqlens,
                max_length_q=max_seqlen,
                max_length_k=max_seqlen,
                is_causal=False,
                **kwargs,
            )
        elif torch.jit.is_tracing():
            attn_output, _ = attention_interface(
                self,
                query_states,
                key_states,
                value_states,
                attention_mask=None,
                scaling=self.scaling,
                dropout=0.0 if not self.training else self.attention_dropout,
                is_causal=False,
                **kwargs,
            )
        else:
            lengths = cu_seqlens[1:] - cu_seqlens[:-1]
            splits = [torch.split(tensor, lengths.tolist(), dim=2) for tensor in (query_states, key_states, value_states)]
            attn_outputs = [
                attention_interface(
                    self,
                    q,
                    k,
                    v,
                    attention_mask=None,
                    scaling=self.scaling,
                    dropout=0.0 if not self.training else self.attention_dropout,
                    is_causal=False,
                    **kwargs,
                )[0]
                for q, k, v in zip(*splits)
            ]
            attn_output = torch.cat(attn_outputs, dim=1)

        attn_output = attn_output.reshape(seq_length, -1).contiguous()
        return self.proj(attn_output)

    def patched_vision_forward(self, hidden_states, grid_thw, **kwargs):
        if not torch.jit.is_tracing():
            return original_vision_forward(self, hidden_states, grid_thw, **kwargs)

        hidden_states = self.patch_embed(hidden_states)
        seq_len, _ = hidden_states.size()

        grid_t = grid_thw[:, 0]
        grid_h = grid_thw[:, 1]
        grid_w = grid_thw[:, 2]
        tokens_per_item = grid_t * grid_h * grid_w
        token_offsets = torch.cumsum(tokens_per_item, dim=0) - tokens_per_item
        image_ids = torch.bucketize(torch.arange(seq_len, device=hidden_states.device), token_offsets[1:], right=False)

        current_grid_h = grid_h[image_ids]
        current_grid_w = grid_w[image_ids]
        current_spatial = current_grid_h * current_grid_w
        local_index = torch.arange(seq_len, device=hidden_states.device) - token_offsets[image_ids]
        spatial_index = torch.remainder(local_index, current_spatial)

        row_ids = torch.div(spatial_index, current_grid_w, rounding_mode="floor")
        col_ids = torch.remainder(spatial_index, current_grid_w)
        max_grid = max(1, self.num_grid_per_side)
        position_ids = row_ids * max_grid + col_ids
        position_ids = torch.remainder(position_ids, self.pos_embed.num_embeddings)

        pos_embeds = self.pos_embed(position_ids)
        hidden_states = hidden_states + pos_embeds.to(hidden_states.dtype)

        rotary_pos_emb = self.rotary_pos_emb(torch.stack((row_ids, col_ids), dim=-1))
        rotary_pos_emb = rotary_pos_emb.reshape(seq_len, -1)
        emb = torch.cat((rotary_pos_emb, rotary_pos_emb), dim=-1)
        position_embeddings = (emb.cos(), emb.sin())

        cu_seqlens = torch.cumsum(tokens_per_item, dim=0, dtype=grid_thw.dtype)
        cu_seqlens = torch.nn.functional.pad(cu_seqlens, (1, 0), value=0)

        hidden_states = hidden_states.reshape(seq_len, -1)
        for blk in self.blocks:
            hidden_states = blk(
                hidden_states,
                cu_seqlens=cu_seqlens,
                position_embeddings=position_embeddings,
                **kwargs,
            )

        merged_hidden_states = self.merger(hidden_states)
        return q35.BaseModelOutputWithPooling(
            last_hidden_state=hidden_states,
            pooler_output=merged_hidden_states,
        )

    q35.Qwen3_5VisionAttention.forward = patched_attention_forward
    q35.Qwen3_5VisionModel.forward = patched_vision_forward
    q35.Qwen3_5VisionModel._elbruno_onnx_patch = True
    q35.Qwen3_5VisionAttention._elbruno_onnx_patch = True


# ── Wrappers ──────────────────────────────────────────────────────────────────


class VisionEncoderWrapper(nn.Module):
    """Exports Fara's vision encoder as a standalone ONNX graph.

    Input:  pixel_values   [num_patches, patch_dim]
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
            dynamo=False,  # use legacy TorchScript trace; avoids data-dependent shape issues in vision encoder
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
            dynamo=False,  # use legacy TorchScript trace
        )

    size_mb = output_path.stat().st_size / (1024 ** 2)
    print(f"  OK  qwen3vl-embedding.onnx  ({size_mb:.1f} MB)")


def create_vision_processor_json(output_dir: Path) -> None:
    """Write ORT-GenAI processor_config.json for multimodal preprocessing."""
    path = output_dir / "processor_config.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(VISION_PROCESSOR_CONFIG, f, indent=2)
    print("  OK  processor_config.json")


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

    # model.onnx may rely on external data, typically model.onnx.data.
    try:
        import onnx

        model_path = output_dir / "model.onnx"
        if model_path.exists():
            model = onnx.load(str(model_path), load_external_data=False)
            external_locations = set()
            for init in model.graph.initializer:
                if init.HasField("data_location") and int(init.data_location) == 1:
                    for entry in init.external_data:
                        if entry.key == "location":
                            external_locations.add(entry.value)
            for location in sorted(external_locations):
                external_path = output_dir / location
                if external_path.exists():
                    size_gb = external_path.stat().st_size / (1024 ** 3)
                    print(f"  OK  {location}  ({size_gb:.2f} GB external data)")
                else:
                    print(f"  MISSING  {location}  (required external data for model.onnx)")
                    all_ok = False

        vision_model_path = output_dir / "qwen3vl-vision.onnx"
        if vision_model_path.exists():
            vision_model = onnx.load(str(vision_model_path), load_external_data=False)
            vision_inputs = [value.name for value in vision_model.graph.input]
            if set(vision_inputs) == {"pixel_values", "image_grid_thw"}:
                print("  OK  qwen3vl-vision.onnx inputs = pixel_values,image_grid_thw")
            else:
                print(f"  FAIL  qwen3vl-vision.onnx inputs = {vision_inputs} (expected pixel_values,image_grid_thw)")
                all_ok = False
    except Exception as e:
        print(f"  WARN  could not inspect ONNX metadata: {e}")

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
            commit_message="Multimodal Fara ONNX: add qwen3vl-vision.onnx, qwen3vl-embedding.onnx, processor_config.json; set model.type=qwen3_vl",
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

        patch_qwen35_vision_for_onnx_export()

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
