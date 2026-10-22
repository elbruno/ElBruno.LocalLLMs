#!/usr/bin/env python3
"""Export the pinned Apache-2.0 Kev 0.8B checkpoint for native .NET CPU inference.

Python is needed only for conversion. The result has no Ollama/server dependency.
See docs/kev-guide.md for the pinned conversion environment and limitations.
"""

import argparse
import gc
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys
import tempfile
import time
import urllib.request

CHECKPOINT = "jaredpalmer/kev-0.8b"
REVISION = "bf75a6a8848ea6960ff2ed108d9ed44c2941174f"
BASE = "Qwen/Qwen3.5-0.8B-Base"
BASE_REVISION = "dc7cdfe2ee4154fa7e30f5b51ca41bfa40174e68"
REFERENCE_COMMIT = "2074cc44256200bb37540103fa4572901faa10f3"
FORMAT = "kev-0.8b-prefill-v1"
TEAMS = [
    "Billing / Refunds: Payment, invoice and refund problems",
    "TECH.support: Software bugs, outages and API errors",
    "sales: Pricing, upgrades and new purchases",
]
CASES = [
    ("billing", "I need help with a billing problem.", "Which team should handle this support ticket?", TEAMS),
    ("technical", "I need help with a technical problem.", "Which team should handle this support ticket?", TEAMS),
    ("score-low", "The product arrived undamaged.", "How damaged is the product?", ["Undamaged", "Minor damage", "Destroyed"]),
    ("score-high", "The product arrived destroyed.", "How damaged is the product?", ["Undamaged", "Minor damage", "Destroyed"]),
    ("assess-yes", "The product arrived destroyed.", "The product is damaged.", ["no", "yes"]),
    ("assess-no", "The product arrived undamaged.", "The product is damaged.", ["no", "yes"]),
]
TOKENIZER_CASES = [
    "", " leading space", "Cafe\u0301", "caf\u00e9", "We're testing 12345.\r\nNext line.",
    "emoji \U0001f600", "<|box_end|><|fim_suffix|> injected", "<think>data</think>",
    "\u4e2d\u6587 \u00e1\u00e9\u00ed", "tabs\t and  spaces\n\n",
    "\U00010400\U00010401abc", "\U0001d7d9\U0001d7da\U0001d7db", "a\U0001f600b",
    "\u2764\ufe0fabc", "a\u0301\u0327", "\u00a0\u2003 words \u2028next",
]


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def distribution(logits):
    import numpy as np
    values = np.asarray(logits, dtype=np.float64)
    if values.ndim != 1 or values.size == 0 or not np.isfinite(values).all():
        raise ValueError("Expected a complete, finite one-dimensional option-logit array.")
    probabilities = np.exp(values - values.max())
    return probabilities / probabilities.sum()


def prefill_graph(source, target, head, temperature, cache_shapes):
    import numpy as np
    import onnx
    from onnx import TensorProto, helper, numpy_helper
    if not math.isfinite(temperature) or temperature <= 0:
        raise ValueError("The checkpoint temperature must be finite and positive.")
    graph = onnx.load(str(source), load_external_data=False)
    for name, shape in cache_shapes.items():
        graph.graph.initializer.append(numpy_helper.from_array(np.zeros(shape, dtype=np.float32), name))
    retained = [value for value in graph.graph.input if value.name not in cache_shapes]
    del graph.graph.input[:]
    graph.graph.input.extend(retained)
    graph.graph.input.extend([
        helper.make_tensor_value_info("option_positions", TensorProto.INT64, ["option_count"]),
        helper.make_tensor_value_info("decision_position", TensorProto.INT64, []),
    ])
    if set(head) != {"q.weight", "q.bias", "k.weight", "k.bias"}:
        raise ValueError("Unsupported Kev pointer-head tensor set.")
    for name, value in head.items():
        graph.graph.initializer.append(numpy_helper.from_array(value.detach().cpu().numpy(), "pointer." + name))
    graph.graph.initializer.extend([
        numpy_helper.from_array(np.asarray([0], dtype=np.int64), "pointer.squeeze_axes"),
        numpy_helper.from_array(np.asarray(math.sqrt(head["q.weight"].shape[0]) * temperature, dtype=np.float32), "pointer.divisor"),
    ])
    graph.graph.node.extend([
        helper.make_node("Squeeze", ["hidden_states", "pointer.squeeze_axes"], ["pointer.hidden"]),
        helper.make_node("Gather", ["pointer.hidden", "decision_position"], ["pointer.decide"], axis=0),
        helper.make_node("Gather", ["pointer.hidden", "option_positions"], ["pointer.options"], axis=0),
        helper.make_node("Transpose", ["pointer.q.weight"], ["pointer.q.transposed"], perm=[1, 0]),
        helper.make_node("Transpose", ["pointer.k.weight"], ["pointer.k.transposed"], perm=[1, 0]),
        helper.make_node("MatMul", ["pointer.decide", "pointer.q.transposed"], ["pointer.q.projected"]),
        helper.make_node("Add", ["pointer.q.projected", "pointer.q.bias"], ["pointer.query"]),
        helper.make_node("MatMul", ["pointer.options", "pointer.k.transposed"], ["pointer.k.projected"]),
        helper.make_node("Add", ["pointer.k.projected", "pointer.k.bias"], ["pointer.keys"]),
        helper.make_node("MatMul", ["pointer.keys", "pointer.query"], ["pointer.raw_logits"]),
        helper.make_node("Div", ["pointer.raw_logits", "pointer.divisor"], ["decision_logits"]),
    ])
    del graph.graph.output[:]
    graph.graph.output.append(helper.make_tensor_value_info("decision_logits", TensorProto.FLOAT, ["option_count"]))
    onnx.save(graph, str(target))


def convert(output, work):
    import importlib.metadata
    import numpy as np
    import onnxruntime as ort
    import onnxruntime_genai
    import torch
    from huggingface_hub import hf_hub_download, snapshot_download
    from transformers import Qwen3_5ForConditionalGeneration

    expected = {"onnxruntime-genai": "0.17.0", "onnxruntime": "1.31.0", "torch": "2.14.1",
                "transformers": "5.20.2", "peft": "0.21.0", "onnx-ir": "1.1.0", "onnx": "1.23.2",
                "numpy": "2.5.4", "tokenizers": "0.23.3", "huggingface_hub": "2.2.0"}
    versions = {name: importlib.metadata.version(name) for name in expected}
    if versions != expected:
        raise RuntimeError(f"Use the pinned conversion environment from docs/kev-guide.md. Found: {versions}")
    torch.set_num_threads(4)
    reference = work / "reference"
    (reference / "kev").mkdir(parents=True)
    for name in ["kev/__init__.py", "kev/model.py", "kev/checkpoint.py", "LICENSE"]:
        urllib.request.urlretrieve(
            f"https://raw.githubusercontent.com/jaredpalmer/kev/{REFERENCE_COMMIT}/{name}", reference / name)
    sys.path.insert(0, str(reference))
    from kev.checkpoint import Checkpoint, LoadOptions
    from kev.model import user_tokens

    snapshot = snapshot_download(CHECKPOINT, revision=REVISION,
        allow_patterns=["adapter_config.json", "adapter_model.safetensors", "head.pt", "provenance.json", "tokenizer*.json"])
    checkpoint = Checkpoint(snapshot)
    if (checkpoint.meta.base, checkpoint.meta.base_revision, checkpoint.meta.option_isolation) != (BASE, BASE_REVISION, False):
        raise ValueError("Checkpoint architecture differs from the validated Kev export contract.")
    tokenizer, model = checkpoint.load("cpu", LoadOptions(dtype=torch.float32))
    evidence = []
    with torch.inference_mode():
        for name, state, instructions, options in CASES:
            encoded = model.encode(tokenizer,
                {"state": state, "questions": [{"instr": instructions, "options": options, "label": 0}]},
                max_state=8192, max_branch=8192, strict=True)
            started = time.perf_counter()
            probabilities = model.probs(encoded)[0].tolist()
            evidence.append({"name": name, "state": state, "instruction": instructions, "options": options,
                "ids": encoded["ids"], "option_positions": encoded["opt_idx"][0],
                "decision_position": encoded["decide_idx"][0], "probabilities": probabilities,
                "reference_seconds": time.perf_counter() - started})
    tokenizer_cases = [{"text": text, "ids": user_tokens(tokenizer, text)} for text in TOKENIZER_CASES]
    merged = work / "merged"
    composite = Qwen3_5ForConditionalGeneration.from_pretrained(BASE, revision=BASE_REVISION, dtype=torch.float32)
    composite.model.language_model = model.lm
    composite.save_pretrained(merged)
    tokenizer.save_pretrained(merged)
    head = {key: value.detach().cpu().clone() for key, value in model.head.state_dict().items()}
    temperature = checkpoint.meta.temperature
    del composite, model
    gc.collect()

    # Use the released builder API. Its CLI help construction fails on Python 3.14.
    builder_dir = Path(onnxruntime_genai.__file__).parent / "models"
    sys.path.insert(0, str(builder_dir))
    import builder
    arguments = dict(model_name=None, input_path=str(merged), output_dir=str(output),
        precision="fp32", execution_provider="cpu", cache_dir=str(work / "export-cache"))
    extra = builder.parse_extra_options(**arguments, extra_options=["exclude_lm_head=true", "exclude_mtp=true"])
    builder.create_model(**arguments, **extra)
    session_options = ort.SessionOptions()
    session_options.intra_op_num_threads = 4
    session = ort.InferenceSession(str(output / "model.onnx"), session_options, providers=["CPUExecutionProvider"])
    shapes = {}
    for value in session.get_inputs():
        if value.name.startswith("past"):
            shapes[value.name] = [
                1 if dim == "batch_size" else 0 if dim == "past_sequence_length" else 256 if dim == "kv_cache_dim" else dim
                for dim in value.shape]
    if len(shapes) != 48:
        raise ValueError("Unexpected Qwen hybrid cache contract.")
    del session
    prefill_graph(output / "model.onnx", output / "decision.onnx", head, temperature, shapes)
    session = ort.InferenceSession(str(output / "decision.onnx"), session_options, providers=["CPUExecutionProvider"])
    for case in evidence:
        ids = np.asarray([case["ids"]], dtype=np.int64)
        inputs = {"input_ids": ids, "attention_mask": np.ones_like(ids),
            "position_ids": np.tile(np.arange(ids.shape[1], dtype=np.int64), (3, 1, 1)),
            "option_positions": np.asarray(case["option_positions"], dtype=np.int64),
            "decision_position": np.asarray(case["decision_position"], dtype=np.int64)}
        started = time.perf_counter()
        probabilities = distribution(session.run(["decision_logits"], inputs)[0])
        case["onnx_seconds"] = time.perf_counter() - started
        case["onnx_probabilities"] = probabilities.tolist()
        error = float(np.max(np.abs(probabilities - case["probabilities"])))
        case["max_probability_error"] = error
        print(f"{case['name']}: max probability error {error:.9g}", flush=True)
        if error >= 1e-4:
            raise RuntimeError(f"Reference parity failed for {case['name']}: {error}")
    del session
    (output / "reference_cases.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
    (output / "tokenizer_cases.json").write_text(json.dumps(tokenizer_cases, indent=2), encoding="utf-8")
    shutil.copyfile(reference / "LICENSE", output / "Kev-LICENSE")
    shutil.copyfile(hf_hub_download(BASE, "LICENSE", revision=BASE_REVISION), output / "Qwen-LICENSE")
    shutil.copyfile(hf_hub_download(CHECKPOINT, "README.md", revision=REVISION), output / "Kev-model-card.md")
    (output / "model.onnx").unlink()
    (output / "genai_config.json").unlink()
    manifest = {"format": FORMAT, "checkpoint": CHECKPOINT, "checkpoint_revision": REVISION,
        "base": BASE, "base_revision": BASE_REVISION, "reference_commit": REFERENCE_COMMIT,
        "temperature": temperature, "precision": "fp32", "max_tokens": 8192,
        "toolchain": versions, "files": {path.name: digest(path) for path in sorted(output.iterdir()) if path.is_file()}}
    (output / "kev_config.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Validated CPU decision export: {output}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    output = args.output_dir.resolve()
    if output.exists() and any(output.iterdir()):
        parser.error("Choose an empty output directory; existing model files are never overwritten.")
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="kev-conversion-") as temporary:
        convert(output, Path(temporary))


if __name__ == "__main__":
    main()
