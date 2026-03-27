#!/usr/bin/env python3
"""
Validate the fine-tuning notebook's Python APIs and data without a GPU.

Checks imports, API signatures, training data structure, and notebook integrity.
Exit code 0 = all checks passed, 1 = one or more failures.
"""

import inspect
import io
import json
import os
import sys
from pathlib import Path

# Ensure UTF-8 output on Windows consoles
if sys.stdout.encoding != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent.parent.parent
NOTEBOOK_PATH = SCRIPT_DIR / "train_and_publish.ipynb"
TRAINING_DATA_DIR = SCRIPT_DIR.parent / "training-data"
EXPECTED_CELL_COUNT = 21

passed = 0
failed = 0


def ok(label: str) -> None:
    global passed
    passed += 1
    print(f"  ✅ {label}")


def fail(label: str, detail: str = "") -> None:
    global failed
    failed += 1
    msg = f"  ❌ {label}"
    if detail:
        msg += f" — {detail}"
    print(msg)


# ── 1. Validate imports ─────────────────────────────────────────────────────
print("\n🔍 1. Validating imports …")

REQUIRED_PACKAGES = {
    "transformers": "transformers",
    "datasets": "datasets",
    "trl": "trl",
    "peft": "peft",
    "onnxruntime_genai": "onnxruntime-genai",
    "onnx": "onnx",
    "onnx_ir": "onnx-ir",
    "huggingface_hub": "huggingface-hub",
}

OPTIONAL_PACKAGES = {
    "unsloth": "unsloth (needs CUDA — optional)",
}

for module, display in REQUIRED_PACKAGES.items():
    try:
        __import__(module)
        ok(f"import {display}")
    except Exception as exc:
        fail(f"import {display}", str(exc))

for module, display in OPTIONAL_PACKAGES.items():
    try:
        __import__(module)
        ok(f"import {display}")
    except Exception:
        print(f"  ⚠️  import {display} — skipped (optional)")


# ── 2. Validate TrainingArguments API ────────────────────────────────────────
print("\n🔍 2. Validating TrainingArguments API …")

try:
    from transformers import TrainingArguments

    sig = inspect.signature(TrainingArguments.__init__)
    if "eval_strategy" in sig.parameters:
        ok("TrainingArguments has 'eval_strategy' parameter")
    else:
        fail(
            "TrainingArguments missing 'eval_strategy'",
            "May be using deprecated 'evaluation_strategy'",
        )

    if "evaluation_strategy" in sig.parameters:
        print("  ⚠️  'evaluation_strategy' still present (deprecated alias)")
except Exception as exc:
    fail("TrainingArguments inspection", str(exc))


# ── 3. Validate onnxruntime-genai API ────────────────────────────────────────
print("\n🔍 3. Validating onnxruntime-genai API …")

try:
    import onnxruntime_genai as og

    for cls_name in ("GeneratorParams", "Generator", "Tokenizer", "Model"):
        cls = getattr(og, cls_name, None)
        if cls is not None:
            ok(f"og.{cls_name} exists")
            if callable(cls):
                ok(f"og.{cls_name} is callable")
            else:
                fail(f"og.{cls_name} is not callable")
        else:
            fail(f"og.{cls_name} missing")

    generator_cls = getattr(og, "Generator", None)
    if generator_cls is not None:
        EXPECTED_METHODS = [
            "append_tokens",
            "generate_next_token",
            "get_sequence",
            "is_done",
        ]
        for method in EXPECTED_METHODS:
            if hasattr(generator_cls, method):
                ok(f"Generator.{method} exists")
            else:
                fail(f"Generator.{method} missing")

        REMOVED_METHODS = ["compute_logits"]
        for method in REMOVED_METHODS:
            if hasattr(generator_cls, method):
                print(f"  ⚠️  Generator.{method} still present (removed in newer versions)")
            else:
                ok(f"Generator.{method} correctly absent (removed API)")

    params_cls = getattr(og, "GeneratorParams", None)
    if params_cls is not None:
        if hasattr(params_cls, "input_ids"):
            print("  ⚠️  GeneratorParams.input_ids still present (removed in newer versions)")
        else:
            ok("GeneratorParams.input_ids correctly absent (removed API)")

except ImportError as exc:
    fail("import onnxruntime_genai", str(exc))
except Exception as exc:
    fail("onnxruntime-genai API check", str(exc))


# ── 4. Validate training data ────────────────────────────────────────────────
print("\n🔍 4. Validating training data …")

json_files = sorted(TRAINING_DATA_DIR.glob("*.json"))
if not json_files:
    fail("No .json files found", str(TRAINING_DATA_DIR))
else:
    ok(f"Found {len(json_files)} JSON file(s) in training-data/")

for jf in json_files:
    label = jf.relative_to(REPO_ROOT)
    try:
        data = json.loads(jf.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(f"{label} — invalid JSON", str(exc))
        continue

    if not isinstance(data, list):
        fail(f"{label} — expected list, got {type(data).__name__}")
        continue

    ok(f"{label} — valid JSON list ({len(data)} entries)")

    # Check conversation structure on first few entries
    sample = data[:5]
    all_valid = True
    for i, entry in enumerate(sample):
        if "conversations" not in entry:
            fail(f"{label}[{i}] — missing 'conversations' key")
            all_valid = False
            continue
        for j, msg in enumerate(entry["conversations"]):
            if "from" not in msg or "value" not in msg:
                fail(
                    f"{label}[{i}].conversations[{j}]",
                    "missing 'from' or 'value' key",
                )
                all_valid = False
    if all_valid:
        ok(f"{label} — conversation structure valid (sampled {len(sample)} entries)")


# ── 5. Validate notebook JSON ────────────────────────────────────────────────
print("\n🔍 5. Validating notebook JSON …")

try:
    nb = json.loads(NOTEBOOK_PATH.read_text(encoding="utf-8"))
    ok("train_and_publish.ipynb — valid JSON")

    cell_count = len(nb.get("cells", []))
    if cell_count == EXPECTED_CELL_COUNT:
        ok(f"Cell count = {cell_count} (expected {EXPECTED_CELL_COUNT})")
    else:
        fail(
            f"Cell count = {cell_count}",
            f"expected {EXPECTED_CELL_COUNT}",
        )
except json.JSONDecodeError as exc:
    fail("train_and_publish.ipynb — invalid JSON", str(exc))
except FileNotFoundError:
    fail("train_and_publish.ipynb not found", str(NOTEBOOK_PATH))
except Exception as exc:
    fail("notebook validation", str(exc))


# ── Summary ──────────────────────────────────────────────────────────────────
print(f"\n{'='*50}")
print(f"  Passed: {passed}   Failed: {failed}")
print(f"{'='*50}")

sys.exit(1 if failed else 0)
