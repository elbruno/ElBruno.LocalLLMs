# Session Log: Gemma Model Conversions to ONNX GenAI

**Date:** 2026-03-18T15:00Z  
**Session Topic:** Gemma & Llama ONNX model conversions + documentation finalization  
**Participants:** Dozer (ML Engineer), Morpheus (Lead/Documentation), Trinity (Core Dev, pending)

## What Happened

### Phase 1: Dozer — ONNX Conversions (Gemma & Llama Batch)

**Objective:** Convert 5 target models to ONNX GenAI INT4 format

**Execution:**
1. Attempted conversion of Gemma-2B-IT, Llama-3.2-3B, Gemma-2-2B-IT, Gemma-2-9B-IT, Llama-3.3-70B
2. Successfully converted 3 Gemma models (v1 and v2 architectures)
3. Hit gated access on both Llama models (separate from Llama 3.1)

**Results:**
- ✅ Gemma-2B-IT: 3.5 GB INT4, uploaded to elbruno/Gemma-2B-IT-onnx
- ✅ Gemma-2-2B-IT: 3.8 GB INT4, uploaded to elbruno/Gemma-2-2B-IT-onnx
- ✅ Gemma-2-9B-IT: 9.0 GB INT4, uploaded to elbruno/Gemma-2-9B-IT-onnx
- ❌ Llama-3.2-3B-Instruct: 403 (access awaiting Meta review)
- ❌ Llama-3.3-70B-Instruct: 403 (not in authorized list)

**Key Discovery:** Gemma architecture (both v1 and v2) is now CONFIRMED SUPPORTED by onnxruntime_genai builder v0.12.1. This was previously unknown and expands the library's model coverage significantly.

### Phase 2: Morpheus — Documentation (Parallel)

**Objective:** Create comprehensive blocked-models reference document

**Execution:**
1. Documented all 5 blocked model categories with root causes
2. Added 6 next-generation models with timeline/expectations
3. Created decision point for alternative conversion strategies (e.g., 70B quantization via llama.cpp)

**Files Produced:**
- `docs/blocked-models.md` — Complete reference with architecture support matrix

### Phase 3: Trinity — C# Integration (Pending)

**Objective:** Update KnownModels.cs, README, docs for Gemma conversions

**Expected Actions:**
1. Add 3 Gemma model entries to KnownModels.cs
2. Update README model table
3. Update architecture support documentation
4. Run full test suite to validate integration

## Decisions Made

### Decision 25: Gemma Architecture Support (NEW)

**Status:** Confirmed  
**Context:** Gemma-2B, Gemma-2-2B, Gemma-2-9B all converted successfully to ONNX GenAI INT4.

**Decision:** Gemma is now an officially supported architecture family for the library. Both Gemma v1 and v2 are compatible with builder v0.12.1.

**Implications:**
- 3 new models become available immediately (pending KnownModels.cs updates)
- Gemma tier recommendation: Gemma-2-9B-IT for general purpose (good balance of size/quality), Gemma-2B-IT/Gemma-2-2B-IT for embedded scenarios
- Vocabulary size (256K) impacts embedding layer size significantly — document for users

## Blockers Identified

| Model | Blocker | Status | Next Action |
|---|---|---|---|
| Llama-3.2-3B | Gated (Meta) | Awaiting review | Monitor access request |
| Llama-3.3-70B | Gated (Meta) + likely OOM | Not requested yet | Request access, assess 70B feasibility |
| Command-R | Gated (Cohere) | Requires license | Not in scope for current batch |
| StableLM-2 | Architecture unsupported | Permanent | Document in blocked-models.md |
| Mixtral-8x7B | MoE not supported | Permanent | Document in blocked-models.md |

## Test Status

- ✅ Dozer: All 3 conversions validated (files present, HuggingFace repos public)
- ⏳ Trinity: Pending integration test run (expected: all 246+ tests pass)
- ✅ Morpheus: Documentation complete and cross-referenced

## Action Items

### For Bruno (User)
- [ ] Check Llama-3.2 access request status at https://huggingface.co/meta-llama/Llama-3.2-3B-Instruct
- [ ] Request Llama-3.3 access at https://huggingface.co/meta-llama/Llama-3.3-70B-Instruct
- [ ] (Optional) Request Command-R access at https://huggingface.co/CohereLabs/c4ai-command-r-v01

### For Dozer
- [ ] Monitor Llama gated requests; retry conversions once approved
- [ ] Document any learnings from Gemma v1 vs v2 architecture differences
- [ ] Prepare strategy for 70B models if Llama-3.3 is approved (likely needs alternative tool)

### For Trinity
- [ ] Update KnownModels.cs with Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT
- [ ] Update README model table (add 3 Gemma rows)
- [ ] Update architecture support documentation (confirm Gemma v1/v2)
- [ ] Run full test suite (validate 246+ tests pass)
- [ ] Commit with message: "Add Gemma models: Gemma-2B-IT, Gemma-2-2B-IT, Gemma-2-9B-IT"

## Summary

**Phase 1-2 Complete:** 3 Gemma models converted + blocked-models reference finalized. Gemma architecture is now officially supported by the library.

**Phase 3 In Progress:** Trinity to integrate 3 new Gemma models into codebase and validate tests.

**Running Conversion Count:** 15 of 21 models now converted (71%)
