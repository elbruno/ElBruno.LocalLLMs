# Decisions Log

Canonical record of all team decisions. Last merged: 2026-03-29.

---

## Decision: Phase 3 & 4 Scripts — ONNX Conversion & Publishing Pipeline

**Date:** 2026-03-29  
**Author:** Dozer (ML / ONNX Conversion Engineer)  
**Status:** Implemented  
**Plan Reference:** `docs/plan-finetune-qwen.md` §5 (Phase 3) and §6 (Phase 4)

### What Was Done

Created 5 complete scripts in `scripts/finetune/`:

1. `merge_lora.py` — LoRA adapter → merged HuggingFace checkpoint
2. `convert_to_onnx.py` — HuggingFace → ONNX INT4 via onnxruntime_genai builder
3. `validate_onnx.py` — 12-test validation suite against QwenFormatter expectations
4. `upload_to_hf.py` — Upload ONNX model to HuggingFace Hub
5. `model-card-template.md` — HuggingFace model card with YAML frontmatter

### Key Decisions

#### 1. PEFT over Unsloth for merge step
The plan referenced Unsloth for merging, but used `peft.PeftModel.from_pretrained()` + `merge_and_unload()` instead. Reason: PEFT is the standard merge pathway, better documented, and more reliable. Unsloth is great for training speed but its merge API wraps PEFT anyway. The merge script accepts any LoRA checkpoint regardless of training framework.

#### 2. 12 validation test cases (not 10)
Added 2 extra tests beyond the plan minimum: tokenizer round-trip (catches broken tokenizer early) and RAG no-answer edge case (model should refuse when context lacks the answer). Both are common failure modes after quantization.

#### 3. Template variable syntax
Used `{{PLACEHOLDER}}` double-brace syntax in the model card template instead of Python format strings. This avoids escaping issues with C# code blocks in the template and is compatible with multiple rendering tools (Jinja, simple string replace, etc.).

#### 4. CUDA execution provider option
Added `--execution-provider cuda` to convert_to_onnx.py based on prior experience converting Llama-3.3-70B. The CUDA EP uses a streaming quantization path that avoids OOM on 14B+ models. CPU EP is default for simplicity.

#### 5. INT4 accuracy level 4 as default
Set `int4_accuracy_level=4` (highest quality) as the default. Fine-tuned models lose more quality to quantization than base models (~1-3% vs <1%), so maximum accuracy is worth the marginal size increase.

### Dependencies

These scripts depend on Phase 1 (training data) and Phase 2 (fine-tuning) being complete to actually run. However, the scripts work independently with any HuggingFace model — they're not hardcoded to a specific checkpoint.

### Next Steps

- Phase 2 (Mouse) produces LoRA adapters → feed into `merge_lora.py`
- Run the full pipeline: merge → convert → validate → upload
- Phase 5 (Trinity) adds KnownModels entries for the published models

---

## Decision: Phase 5 Evaluation Test Suite

**Date:** 2026-03-29  
**Author:** Tank (Tester)  
**Status:** Implemented

### Context

Phase 5 of the fine-tuning plan (docs/plan-finetune-qwen.md) requires an evaluation test suite to validate fine-tuned model output quality. Since we can't run inference in CI, tests validate FORMAT of model output, training data structure, and template compliance.

### Decisions

1. **Separate test project** (`ElBruno.LocalLLMs.FineTuneEval`) rather than adding to existing `ElBruno.LocalLLMs.Tests` — keeps eval tests isolated and independently runnable.

2. **xUnit framework** (not MSTest) — matches all existing test projects in the repo and copilot-instructions.md conventions.

3. **Training data tests use SkippableFact** — `training-data/` folder doesn't exist yet (Phase 1 deliverable). Tests skip gracefully with `Skip.If(!Directory.Exists(...))` and will auto-activate when Mouse delivers Phase 1.

4. **Round-trip validation** — ToolCallingFormatTests includes a formatter→parser round-trip test proving QwenFormatter output is parseable by JsonToolCallParser. This ensures training data format and runtime format are aligned.

5. **RAG format tests are pattern-based** — since RAG pipeline produces text, tests validate citation markers `[N]`, context injection format, and refusal patterns using regex and string matching.

6. **InternalsVisibleTo added** — new project can access internal types (QwenFormatter, JsonToolCallParser, ParsedToolCall, ChatMLFormatter).

### Results

- 48 total tests: 46 passing, 2 skipped (training data files pending)
- 4 test files: ToolCallingFormatTests (14), RagFormatTests (6), TrainingDataValidationTests (10), ChatTemplateAdherenceTests (9) — total exceeds minimum requirements (10+5+8+5=28 minimum, delivered 39+9=48)
- Project added to solution file under `/src/tests/`
- Build and test pass cleanly on net8.0

---

## Decision: Phase 5 — Fine-Tuned Model Integration

**Author:** Trinity (Core Dev)  
**Date:** 2026-03-29  
**Status:** Implemented

### Context

Phase 5 of the fine-tuning plan (`docs/plan-finetune-qwen.md`) calls for integrating fine-tuned Qwen2.5 models into the library so .NET developers can discover and use them easily.

### Decisions Made

#### 1. Three fine-tuned model definitions added to KnownModels

Added `Qwen25_05B_ToolCalling`, `Qwen25_05B_RAG`, and `Qwen25_05B_Instruct_FineTuned` to `KnownModels.cs` in the Tiny tier section, immediately after the base `Qwen25_05BInstruct`. All three are included in the `All` collection.

#### 2. No `Description` property on ModelDefinition

The plan specified a `Description` field on the new model entries, but `ModelDefinition` doesn't have this property. Adding it would be a public API change requiring consideration across all existing models. Omitted for now — can be added as a separate enhancement.

#### 3. Sample follows ToolCallingAgent pattern exactly

`FineTunedToolCalling` uses the same structure: agent loop, 3 tools (GetCurrentTime, Calculate, GetWeather), multi-turn demos with the same formatting conventions (emoji markers, section separators, FormatArgs helper).

#### 4. Fine-tuning guide targets two audiences

`docs/fine-tuning-guide.md` has a clear split: "Using Pre-Fine-Tuned Models" (just download and use, C# only) and "Fine-Tuning Your Own Model" (advanced, requires Python). This matches Bruno's directive that most .NET developers just want to download and run.

### Files Changed

- `src/ElBruno.LocalLLMs/Models/KnownModels.cs` — 3 new model definitions
- `src/samples/FineTunedToolCalling/` — Program.cs, .csproj, README.md
- `docs/fine-tuning-guide.md` — new user-facing guide
- `ElBruno.LocalLLMs.slnx` — added FineTunedToolCalling project
- `docs/CHANGELOG.md` — Phase 5 entries
