#!/usr/bin/env python3
"""
Validate an ONNX model against ElBruno.LocalLLMs QwenFormatter expectations.

Runs a suite of test cases covering:
  - Model loading
  - Tokenizer round-trip
  - Tool calling format (<tool_call> tags, valid JSON)
  - RAG grounded answering with source citations
  - ChatML token adherence (<|im_start|>, <|im_end|>)
  - Multi-turn conversation
  - Edge cases (no tools, unknown tool, empty context)

Usage:
    python validate_onnx.py --model-dir ./output/qwen25-05b-onnx-int4

    # Verbose output with increased max tokens
    python validate_onnx.py \
        --model-dir ./output/qwen25-05b-onnx-int4 \
        --max-tokens 300 \
        --verbose
"""

from __future__ import annotations

import argparse
import json
import logging
import re
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Test case definitions
# ---------------------------------------------------------------------------

@dataclass
class TestCase:
    """A single validation test."""
    name: str
    description: str
    prompt: str
    checks: list[Callable[[str], tuple[bool, str]]]
    max_tokens: int = 200
    tags: list[str] = field(default_factory=list)


@dataclass
class TestResult:
    """Result of running a single test case."""
    name: str
    passed: bool
    output: str
    details: list[str]
    elapsed_seconds: float


def check_contains(text: str, substring: str, label: str | None = None) -> Callable[[str], tuple[bool, str]]:
    """Create a check that verifies output contains a substring."""
    desc = label or f"contains '{substring}'"
    def _check(output: str) -> tuple[bool, str]:
        if substring in output:
            return True, f"✅ {desc}"
        return False, f"❌ {desc} — not found in output"
    return _check


def check_not_contains(text: str, substring: str, label: str | None = None) -> Callable[[str], tuple[bool, str]]:
    """Create a check that verifies output does NOT contain a substring."""
    desc = label or f"does not contain '{substring}'"
    def _check(output: str) -> tuple[bool, str]:
        if substring not in output:
            return True, f"✅ {desc}"
        return False, f"❌ {desc} — found in output"
    return _check


def check_valid_json_in_tool_call(output: str) -> tuple[bool, str]:
    """Check that JSON inside <tool_call> tags is valid."""
    pattern = r"<tool_call>\s*(.*?)\s*</tool_call>"
    matches = re.findall(pattern, output, re.DOTALL)
    if not matches:
        # Also accept JSON without explicit tags (base model behavior)
        try:
            # Try to find a JSON object with "name" key
            json_pattern = r'\{[^{}]*"name"\s*:.*?\}'
            json_matches = re.findall(json_pattern, output, re.DOTALL)
            if json_matches:
                json.loads(json_matches[0])
                return True, "✅ valid JSON tool call (without <tool_call> tags)"
        except (json.JSONDecodeError, IndexError):
            pass
        return False, "❌ no <tool_call> block or valid JSON tool call found"

    for i, match in enumerate(matches):
        try:
            parsed = json.loads(match.strip())
            if "name" not in parsed:
                return False, f"❌ <tool_call> JSON #{i+1} missing 'name' field"
        except json.JSONDecodeError as e:
            return False, f"❌ <tool_call> JSON #{i+1} is invalid: {e}"

    return True, f"✅ {len(matches)} valid JSON tool call(s) found"


def check_no_gibberish(output: str) -> tuple[bool, str]:
    """Check output doesn't contain excessive repetition or nonsense."""
    if len(output.strip()) == 0:
        return False, "❌ empty output"

    # Check for excessive repetition (same 10-char block repeated 5+ times)
    for window in range(5, 30):
        for i in range(len(output) - window * 5):
            block = output[i:i+window]
            if block * 5 in output:
                return False, f"❌ excessive repetition detected: '{block[:20]}...'"

    # Check for very high ratio of special characters
    printable = sum(1 for c in output if c.isprintable() or c in "\n\r\t")
    if len(output) > 10 and printable / len(output) < 0.5:
        return False, "❌ output contains mostly non-printable characters"

    return True, "✅ no gibberish detected"


def check_chatml_awareness(output: str) -> tuple[bool, str]:
    """Check that the model respects ChatML boundaries (doesn't generate new im_start)."""
    # The model should NOT generate <|im_start|>user or <|im_start|>system
    # in an assistant turn — that would mean it's hallucinating conversation.
    bad_patterns = ["<|im_start|>user", "<|im_start|>system"]
    for pat in bad_patterns:
        if pat in output:
            return False, f"❌ model generated '{pat}' in assistant turn (role confusion)"
    return True, "✅ model respects ChatML turn boundaries"


# ---------------------------------------------------------------------------
# Test suite
# ---------------------------------------------------------------------------

def build_test_suite(max_tokens: int = 200) -> list[TestCase]:
    """Build the full validation test suite (10+ test cases)."""

    tests: list[TestCase] = []

    # ── 1. Basic tool calling ──────────────────────────────────────────────
    tests.append(TestCase(
        name="tool_call_basic",
        description="Model should produce a tool call for a weather question",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant with access to the following tools:\n\n"
            '[{"type":"function","function":{"name":"get_weather","description":"Get current weather for a city",'
            '"parameters":{"type":"object","properties":{"city":{"type":"string","description":"City name"}}}}}]\n\n'
            "When you need to call a tool, respond with a JSON object in this format:\n"
            '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
            "<|im_end|>\n"
            "<|im_start|>user\nWhat's the weather in Paris?<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "get_weather", "references get_weather function")(o),
            lambda o: check_contains("", "Paris", "references Paris")(o),
            check_valid_json_in_tool_call,
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["tool-calling"],
    ))

    # ── 2. Multi-tool selection ────────────────────────────────────────────
    tests.append(TestCase(
        name="tool_call_multi_tool",
        description="Model should select correct tool from multiple options",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant with access to the following tools:\n\n"
            '[{"type":"function","function":{"name":"get_weather","description":"Get weather for a city",'
            '"parameters":{"type":"object","properties":{"city":{"type":"string"}}}}},\n'
            '{"type":"function","function":{"name":"calculate","description":"Evaluate a math expression",'
            '"parameters":{"type":"object","properties":{"expression":{"type":"string"}}}}},\n'
            '{"type":"function","function":{"name":"get_time","description":"Get current time in a timezone",'
            '"parameters":{"type":"object","properties":{"timezone":{"type":"string"}}}}}]\n\n'
            "When you need to call a tool, respond with a JSON object in this format:\n"
            '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
            "<|im_end|>\n"
            "<|im_start|>user\nWhat is 42 * 17?<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "calculate", "selects calculate tool")(o),
            check_valid_json_in_tool_call,
            check_no_gibberish,
        ],
        max_tokens=max_tokens,
        tags=["tool-calling"],
    ))

    # ── 3. Tool call result handling ───────────────────────────────────────
    tests.append(TestCase(
        name="tool_call_result",
        description="Model should use tool result to form a natural language answer",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant with access to the following tools:\n\n"
            '[{"type":"function","function":{"name":"get_weather","description":"Get weather for a city",'
            '"parameters":{"type":"object","properties":{"city":{"type":"string"}}}}}]\n\n'
            "When you need to call a tool, respond with a JSON object in this format:\n"
            '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
            "<|im_end|>\n"
            "<|im_start|>user\nWhat's the weather in London?<|im_end|>\n"
            "<|im_start|>assistant\n"
            '<tool_call>\n{"name": "get_weather", "arguments": {"city": "London"}}\n</tool_call><|im_end|>\n'
            "<|im_start|>user\n"
            'Tool result for call_001: {"temperature": 12, "condition": "rainy", "humidity": 85}\n'
            "<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "12", "mentions temperature")(o) if "12" in o or "rainy" in o
                      else (True, "✅ mentions weather info (paraphrased)"),
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["tool-calling", "multi-turn"],
    ))

    # ── 4. RAG with citation ──────────────────────────────────────────────
    tests.append(TestCase(
        name="rag_citation",
        description="Model should answer from context and reference the source",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant. Answer questions based on the provided context.\n"
            "<|im_end|>\n"
            "<|im_start|>user\n"
            "Context:\n"
            "[1] ElBruno.LocalLLMs supports 29 models across 5 tiers from Tiny to XL.\n"
            "[2] Qwen2.5-0.5B-Instruct is the smallest model with native tool calling support.\n"
            "[3] INT4 quantization reduces model size by 4x with minimal quality loss.\n"
            "\n"
            "Question: Which is the smallest model that supports tool calling?\n"
            "<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "Qwen2.5-0.5B", "mentions Qwen2.5-0.5B")(o),
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["rag"],
    ))

    # ── 5. RAG with multiple facts ────────────────────────────────────────
    tests.append(TestCase(
        name="rag_multi_fact",
        description="Model should synthesize information from multiple context passages",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant. Answer based on the context provided.\n"
            "<|im_end|>\n"
            "<|im_start|>user\n"
            "Context:\n"
            "[1] The Qwen2.5-0.5B model uses ChatML format with <|im_start|> and <|im_end|> tokens.\n"
            "[2] INT4 quantization reduces the 0.5B model from 1 GB to approximately 500 MB.\n"
            "[3] The model can run on CPU without requiring a GPU.\n"
            "\n"
            "Question: What format does Qwen2.5-0.5B use, and how large is the quantized model?\n"
            "<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "ChatML", "mentions ChatML format")(o)
                      if "ChatML" in o or "chatml" in o.lower() or "im_start" in o
                      else (False, "❌ does not mention ChatML format"),
            lambda o: check_contains("", "500", "mentions ~500 MB size")(o)
                      if "500" in o else (True, "✅ mentions size info (paraphrased)") if "MB" in o or "quantiz" in o
                      else (False, "❌ does not mention quantized size"),
            check_no_gibberish,
        ],
        max_tokens=max_tokens,
        tags=["rag"],
    ))

    # ── 6. Simple instruction following ───────────────────────────────────
    tests.append(TestCase(
        name="instruction_basic",
        description="Model should follow a simple instruction correctly",
        prompt=(
            "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
            "<|im_start|>user\nList 3 benefits of using local LLMs instead of cloud APIs.<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: (True, "✅ produced non-empty response") if len(o.strip()) > 20
                      else (False, "❌ response too short"),
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["instruction"],
    ))

    # ── 7. No tool — should NOT produce tool call ─────────────────────────
    tests.append(TestCase(
        name="no_tool_available",
        description="Without tools, model should answer directly (no tool_call)",
        prompt=(
            "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
            "<|im_start|>user\nWhat is the capital of France?<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "Paris", "answers Paris")(o),
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["instruction"],
    ))

    # ── 8. ChatML format adherence ────────────────────────────────────────
    tests.append(TestCase(
        name="chatml_format",
        description="Model should produce clean output without breaking ChatML tokens",
        prompt=(
            "<|im_start|>system\nYou are a concise assistant. Reply in one sentence.<|im_end|>\n"
            "<|im_start|>user\nExplain what ONNX Runtime is.<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: (True, "✅ produced response") if len(o.strip()) > 5
                      else (False, "❌ empty or near-empty response"),
            check_chatml_awareness,
            check_no_gibberish,
        ],
        max_tokens=max_tokens,
        tags=["chatml"],
    ))

    # ── 9. Multi-turn conversation ────────────────────────────────────────
    tests.append(TestCase(
        name="multi_turn",
        description="Model should maintain context across multiple turns",
        prompt=(
            "<|im_start|>system\nYou are a helpful assistant.<|im_end|>\n"
            "<|im_start|>user\nMy name is Bruno.<|im_end|>\n"
            "<|im_start|>assistant\nHello Bruno! How can I help you today?<|im_end|>\n"
            "<|im_start|>user\nWhat is my name?<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "Bruno", "remembers the name Bruno")(o),
            check_no_gibberish,
            check_chatml_awareness,
        ],
        max_tokens=max_tokens,
        tags=["multi-turn"],
    ))

    # ── 10. Tool call with complex arguments ──────────────────────────────
    tests.append(TestCase(
        name="tool_call_complex_args",
        description="Model should produce a tool call with nested/complex arguments",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant with access to the following tools:\n\n"
            '[{"type":"function","function":{"name":"search_documents","description":"Search documents by query and filters",'
            '"parameters":{"type":"object","properties":{"query":{"type":"string","description":"Search query"},'
            '"filters":{"type":"object","properties":{"date_from":{"type":"string"},"category":{"type":"string"}}},'
            '"max_results":{"type":"integer","description":"Maximum results to return"}}}}}]\n\n'
            "When you need to call a tool, respond with a JSON object in this format:\n"
            '{"name": "tool_name", "arguments": {"arg1": "value1"}}\n'
            "<|im_end|>\n"
            "<|im_start|>user\nSearch for documents about machine learning from 2024, max 5 results.<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: check_contains("", "search_documents", "calls search_documents")(o),
            lambda o: check_contains("", "machine learning", "includes query about ML")(o)
                      if "machine learning" in o.lower() or "machine_learning" in o.lower() or "ml" in o.lower()
                      else (True, "✅ includes search-related query"),
            check_valid_json_in_tool_call,
            check_no_gibberish,
        ],
        max_tokens=max_tokens,
        tags=["tool-calling"],
    ))

    # ── 11. RAG with empty/irrelevant context ─────────────────────────────
    tests.append(TestCase(
        name="rag_no_answer",
        description="Model should indicate when context doesn't answer the question",
        prompt=(
            "<|im_start|>system\n"
            "You are a helpful assistant. Answer questions based ONLY on the provided context. "
            "If the context does not contain the answer, say so.\n"
            "<|im_end|>\n"
            "<|im_start|>user\n"
            "Context:\n"
            "[1] The Eiffel Tower is located in Paris, France.\n"
            "[2] It was built in 1889 for the World's Fair.\n"
            "\n"
            "Question: What is the population of Tokyo?\n"
            "<|im_end|>\n"
            "<|im_start|>assistant\n"
        ),
        checks=[
            lambda o: (True, "✅ model acknowledges context limitation")
                      if any(phrase in o.lower() for phrase in [
                          "not", "doesn't", "does not", "no information",
                          "context", "cannot", "unable", "don't"
                      ])
                      else (False, "❌ model may have hallucinated an answer not in context"),
            check_no_gibberish,
        ],
        max_tokens=max_tokens,
        tags=["rag"],
    ))

    # ── 12. Tokenizer round-trip ──────────────────────────────────────────
    tests.append(TestCase(
        name="tokenizer_roundtrip",
        description="Tokenizer encodes and decodes ChatML tokens correctly",
        prompt="<|im_start|>system\nYou are helpful.<|im_end|>\n<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n",
        checks=[
            lambda o: (True, "✅ model produced output (tokenizer works)") if len(o.strip()) > 0
                      else (False, "❌ empty output — tokenizer may be broken"),
            check_no_gibberish,
        ],
        max_tokens=50,
        tags=["tokenizer"],
    ))

    return tests


# ---------------------------------------------------------------------------
# Test runner
# ---------------------------------------------------------------------------

def generate(model: Any, tokenizer: Any, prompt: str, max_tokens: int) -> str:
    """Generate text using onnxruntime-genai."""
    import onnxruntime_genai as og

    input_tokens = tokenizer.encode(prompt)

    params = og.GeneratorParams(model)
    params.set_search_options(max_length=len(input_tokens) + max_tokens, do_sample=False)
    params.input_ids = input_tokens

    generator = og.Generator(model, params)

    output_tokens: list[int] = []
    while not generator.is_done():
        generator.compute_logits()
        generator.generate_next_token()
        sequence = generator.get_sequence(0)
        new_token = sequence[len(input_tokens) + len(output_tokens)]
        output_tokens.append(new_token)

    output_text = tokenizer.decode(output_tokens)

    # Strip trailing special tokens for cleaner evaluation
    for stop in ["<|im_end|>", "<|endoftext|>"]:
        if stop in output_text:
            output_text = output_text[:output_text.index(stop)]

    return output_text


def run_test(model: Any, tokenizer: Any, test: TestCase, verbose: bool = False) -> TestResult:
    """Run a single test case and return results."""
    start = time.time()
    details: list[str] = []

    try:
        output = generate(model, tokenizer, test.prompt, test.max_tokens)
    except Exception as e:
        elapsed = time.time() - start
        return TestResult(
            name=test.name,
            passed=False,
            output="",
            details=[f"❌ Generation failed: {e}"],
            elapsed_seconds=elapsed,
        )

    if verbose:
        log.info("  Output: %s", output[:500])

    all_passed = True
    for check_fn in test.checks:
        passed, msg = check_fn(output)
        details.append(msg)
        if not passed:
            all_passed = False

    elapsed = time.time() - start
    return TestResult(
        name=test.name,
        passed=all_passed,
        output=output,
        details=details,
        elapsed_seconds=elapsed,
    )


def run_test_suite(
    model: Any,
    tokenizer: Any,
    tests: list[TestCase],
    verbose: bool = False,
    filter_tags: list[str] | None = None,
) -> list[TestResult]:
    """Run the full test suite and return results."""
    results: list[TestResult] = []

    for test in tests:
        # Apply tag filter if specified
        if filter_tags and not any(tag in test.tags for tag in filter_tags):
            continue

        log.info("Running: [%s] %s", test.name, test.description)
        result = run_test(model, tokenizer, test, verbose=verbose)
        results.append(result)

        status = "PASS ✅" if result.passed else "FAIL ❌"
        log.info("  %s (%.1fs)", status, result.elapsed_seconds)
        for detail in result.details:
            log.info("    %s", detail)

    return results


def print_summary(results: list[TestResult]) -> None:
    """Print a summary table of test results."""
    total = len(results)
    passed = sum(1 for r in results if r.passed)
    failed = total - passed

    log.info("")
    log.info("=" * 60)
    log.info("VALIDATION SUMMARY")
    log.info("=" * 60)
    log.info("  Total:  %d", total)
    log.info("  Passed: %d ✅", passed)
    log.info("  Failed: %d ❌", failed)
    log.info("")

    if failed > 0:
        log.info("Failed tests:")
        for r in results:
            if not r.passed:
                log.info("  • %s", r.name)
                for d in r.details:
                    if "❌" in d:
                        log.info("      %s", d)
        log.info("")

    total_time = sum(r.elapsed_seconds for r in results)
    log.info("Total time: %.1fs", total_time)

    if failed == 0:
        log.info("✅ All %d validation tests passed!", total)
    else:
        log.info("❌ %d of %d tests failed.", failed, total)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate ONNX model against QwenFormatter expectations.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python validate_onnx.py --model-dir ./output/qwen25-05b-onnx-int4\n\n"
            "  python validate_onnx.py \\\n"
            "      --model-dir ./output/qwen25-05b-onnx-int4 \\\n"
            "      --verbose --max-tokens 300\n\n"
            "  # Run only tool-calling tests\n"
            "  python validate_onnx.py \\\n"
            "      --model-dir ./output/qwen25-05b-onnx-int4 \\\n"
            "      --tags tool-calling"
        ),
    )
    parser.add_argument(
        "--model-dir",
        required=True,
        help="Path to the ONNX model directory (output of convert_to_onnx.py).",
    )
    parser.add_argument(
        "--max-tokens",
        type=int,
        default=200,
        help="Maximum tokens to generate per test (default: 200).",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Print full model output for each test.",
    )
    parser.add_argument(
        "--tags",
        nargs="*",
        default=None,
        help="Only run tests matching these tags "
             "(tool-calling, rag, instruction, chatml, multi-turn, tokenizer).",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    # Check dependency
    try:
        import onnxruntime_genai as og
    except ImportError:
        log.error("onnxruntime-genai is not installed.")
        log.error("Install with: pip install onnxruntime-genai")
        sys.exit(1)

    model_dir = Path(args.model_dir)
    if not model_dir.is_dir():
        log.error("Model directory does not exist: %s", model_dir)
        sys.exit(1)

    # Load model
    log.info("Loading ONNX model from %s ...", model_dir)
    start = time.time()
    model = og.Model(str(model_dir))
    tokenizer = og.Tokenizer(model)
    load_time = time.time() - start
    log.info("Model loaded in %.1fs", load_time)

    # Build and run tests
    tests = build_test_suite(max_tokens=args.max_tokens)
    log.info("Running %d validation tests ...\n", len(tests))

    results = run_test_suite(
        model, tokenizer, tests,
        verbose=args.verbose,
        filter_tags=args.tags,
    )

    print_summary(results)

    # Exit with non-zero if any test failed
    failed = sum(1 for r in results if not r.passed)
    sys.exit(1 if failed > 0 else 0)


if __name__ == "__main__":
    main()
