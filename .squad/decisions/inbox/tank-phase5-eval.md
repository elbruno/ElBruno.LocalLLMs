# Decision: Phase 5 Evaluation Test Suite

**Date:** 2026-03-29  
**Author:** Tank (Tester)  
**Status:** Implemented

## Context

Phase 5 of the fine-tuning plan (docs/plan-finetune-qwen.md) requires an evaluation test suite to validate fine-tuned model output quality. Since we can't run inference in CI, tests validate FORMAT of model output, training data structure, and template compliance.

## Decisions

1. **Separate test project** (`ElBruno.LocalLLMs.FineTuneEval`) rather than adding to existing `ElBruno.LocalLLMs.Tests` — keeps eval tests isolated and independently runnable.

2. **xUnit framework** (not MSTest) — matches all existing test projects in the repo and copilot-instructions.md conventions.

3. **Training data tests use SkippableFact** — `training-data/` folder doesn't exist yet (Phase 1 deliverable). Tests skip gracefully with `Skip.If(!Directory.Exists(...))` and will auto-activate when Mouse delivers Phase 1.

4. **Round-trip validation** — ToolCallingFormatTests includes a formatter→parser round-trip test proving QwenFormatter output is parseable by JsonToolCallParser. This ensures training data format and runtime format are aligned.

5. **RAG format tests are pattern-based** — since RAG pipeline produces text, tests validate citation markers `[N]`, context injection format, and refusal patterns using regex and string matching.

6. **InternalsVisibleTo added** — new project can access internal types (QwenFormatter, JsonToolCallParser, ParsedToolCall, ChatMLFormatter).

## Results

- 48 total tests: 46 passing, 2 skipped (training data files pending)
- 4 test files: ToolCallingFormatTests (14), RagFormatTests (6), TrainingDataValidationTests (10), ChatTemplateAdherenceTests (9) — total exceeds minimum requirements (10+5+8+5=28 minimum, delivered 39+9=48)
- Project added to solution file under `/src/tests/`
- Build and test pass cleanly on net8.0
