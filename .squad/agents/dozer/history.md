# Dozer — History

## Latest: superwhisper/s1-mini ONNX Conversion — Complete (2026-08-19)

**2026-08-19 (follow-up note):** Tank's end-to-end verification (Decision 37) confirms the C# library path is empirically equivalent to Dozer's Python baseline — prompt parity is byte-for-byte and all 6 reference outputs match. Your ONNX conversion and smoke-eval findings are validated. The library is production-ready from quality/correctness perspective for the tested CPU path.

**2026-08-19 (original):** Converted `superwhisper/s1-mini` (0.6B ASR-transcript normalizer,
Qwen3 arch) to ONNX INT4 (0.37 GB) and FP16 (1.12 GB) via
`scripts/convert_s1_mini.py` (modeled on `convert_magentic_brain.py`). Uploaded
both variants to `elbruno/s1-mini-onnx` (`int4/`, `fp16/` subfolders, matching
the `KnownModels.Phi4`/`GptOss20B` `ModelSubPath` convention).

Smoke-eval (`scripts/eval_s1_mini.py`) found and fixed two onnxruntime-genai
0.15.1 native crashes: (1) `temperature=0.0` with `do_sample=False` still
divides by temperature and crashes — must omit temperature for greedy
decoding; (2) `tokenizer.decode([])` on an empty (immediate-EOS) generated
sequence crashes with an integer divide-by-zero — must guard for zero-length
output. Also found the model's chat template requires an explicit literal
`<think>\n\n</think>\n\n` block after the assistant turn header to suppress
reasoning output (its `enable_thinking=False` handling).

**Recommendation: INT4 is the only usable default.** FP16 fails to run at all
on CPU with onnxruntime-genai 0.15.1 — a shape-mismatch in the ORT
buffer-reuse graph optimizer's GQA `repeat_kv` `Reshape` node, reproduced
consistently across all test prompts. INT4 passed all 6 smoke-eval prompts
(normalization, filler→empty-string, `[Context: email]`, `[Structure:
lists]`) with high-quality output matching the model card's reference
example. Full evidence in
`.squad/decisions/inbox/dozer-s1-mini-conversion.md`.

Also probed Trinity's 4 unverified `TranscriptNormalizer` enum values against
the INT4 model per follow-up request: `Styling.Formal`/`Styling.Casual`
confirmed as real and distinct; `Context.Message`/`Context.Notes` found to be
no-ops (identical output to `Context.General`). Full evidence in
`.squad/decisions/inbox/dozer-s1-mini-conversion.md`.

---

## Previous: Phase 3A — magentic-ui .NET Port — Complete (2026-07-23)

**2026-07-23T16:38:** Phase 3A complete. Dozer's Phase 3 research brief (Amendment A1) was applied before scaffold. Switch confirmed SK spike finding: `Agents.MagenticOne` does not exist; `Agents.Magentic` preview-only. Path B (MEAI OmniAgent loop) adopted. All three projects built (0 errors); 40 Tank tests passing.

---


## Previous Work Summary

Delivered across 20+ sessions since 2026-03-17:
- Magentic-ui Phase 3A (2026-07-23): 3-project ASP.NET Core orchestration, 40 tests passing
- VLM support (Fara1.5-9B, bitnet, Qwen3, Phi-4, GPT-OSS-20B)
- Conversion pipelines for ONNX, quantization strategies (INT4/FP16)
- Test coverage, CI/CD workflows, documentation standards
- DI patterns, chat template formats, model registry architecture

See decision archive for full records.
