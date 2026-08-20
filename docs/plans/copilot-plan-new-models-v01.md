# Copilot Plan-Mode Prompt — Evaluate Muse Glimmer + Nemotron 3.5 Lightning for ElBruno.LocalLLMs

**Version:** v01
**Date:** 2026-08-12
**Target repo:** `elbruno/ElBruno.LocalLLMs`
**Intended use:** paste the block in [§ THE PROMPT](#the-prompt) into GitHub Copilot **plan mode** with the repo open.
**Expected output from Copilot:** a written plan + a GitHub issue body. No code changes in this pass.

---

## Changelog

| Version | Date | Notes |
|---------|------|-------|
| v01 | 2026-08-12 | Initial. Covers Meta Muse Glimmer 30B and NVIDIA Nemotron 3.5 Lightning 30B-A3B. Encodes verified architecture facts, ORT GenAI constraints, and the Gemma 4 lessons from `docs/blocked-models.md`. |

---

## Background research (verified 2026-08-12)

### Meta — Muse Glimmer 30B

Announced Aug 10, 2026 alongside Zuckerberg's open-weight essay; Meta also said it will open the weights of Muse Spark 1.2, with larger models to follow.

| Property | Value |
|---|---|
| HF repo (BF16) | `meta-models/Muse-Glimmer-30B` |
| Other artifacts | `-GGUF` (Meta-calibrated k-quants), `-ExecuTorch-PTE`, `-assistant` (DFlash drafter) |
| License | Apache 2.0 |
| Params | 30B dense — 2B ViT Perception Encoder + 28B text decoder |
| Transformers class | `MuseGlimmerForConditionalGeneration` / `AutoModelForMultimodalLM` |
| Text decoder | 52 layers, pattern `(SWA, SWA, SWA, Full) × 13`; SWA window 2048 with RoPE; every 4th layer full-attention with **NoPE** |
| Attention | **Gated** GQA, 16 query heads per KV head; Q-K RMSNorm + extra query scaling |
| Vision | 50-layer ViT, 2×3×14×14 patchify, interpolated learned abs pos embeddings, 2D RoPE, pixel-shuffle 2×2 merge, projection into decoder embedding space |
| Video | same encoder frame-by-frame, 2 fps, 96-frame cap, timestamped `<\|video\|>` placeholders |
| Spec decoding | DFlash block-diffusion drafter, block size 16 (1 anchor + 15) |
| Control | `reasoning_strength: low / medium / high / xhigh` via system prompt |
| Day-0 support | transformers, llama.cpp, vLLM (transformers backend), MLX, ExecuTorch. **No ONNX.** |

Sources: <https://huggingface.co/blog/muse-glimmer> · <https://huggingface.co/meta-models/Muse-Glimmer-30B> · <https://research.meta.ai/blog/introducing-muse-glimmer-open-agentic-model>

### NVIDIA — Nemotron 3.5 Lightning 30B-A3B

Announced Aug 11, 2026. Smallest member of the Nemotron 3 family. NVIDIA has separately confirmed work on a Nemotron 4 family (reportedly ≥1T params, no release date) — out of scope here but worth a forward-looking note in the plan.

| Property | Value |
|---|---|
| HF repos | `nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16` (reference / customization), `-NVFP4` (inference), `-Base-BF16`, `-NVFP4-DFlash`, `-DSpark` variants |
| License | **OpenMDW-1.1** (not Apache/MIT — needs a compliance check before republishing derived weights) |
| Params | 30B total / 3B active |
| Architecture id (GGUF) | `nemotron_h_moe` |
| Architecture | Hybrid **LatentMoE** — interleaved Mamba-2 SSM layers + MoE layers + select attention layers |
| LatentMoE | tokens projected into a low-rank latent space before expert routing |
| MTP | Multi-Token Prediction heads baked in via a dedicated continued-pretraining phase; enables native speculative decoding |
| Context | up to 1M tokens |
| Spec decoding | MTP, DFlash, DSpark drafters shipped separately |
| Community GGUF | `ggml-org/…-GGUF`, `bartowski/…-GGUF` (Q4_K_M ≈ 22–25 GB) |
| Ecosystem | vLLM, SGLang, TensorRT-LLM, llama.cpp, Ollama, LM Studio, Unsloth. **No ONNX.** |

Sources: <https://developer.nvidia.com/blog/nvidia-nemotron-3-5-lightning-delivers-fast-accurate-specialized-task-execution-for-long-running-agents/> · <https://blogs.nvidia.com/blog/nemotron-lightning-switchyard-rtx-dgx/> · <https://huggingface.co/nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16>

### ONNX Runtime GenAI reality check

- README architecture list includes **Nemotron**, **Granite MoE Hybrid**, Fara, Qwen (language + vision), gpt-oss, Phi, Gemma, Llama, Mistral, DeepSeek, HunYuan, InternLM2, SmolLM3.
- v0.14.0 added **Qwen3.5-MoE (35B-A3B)** support — the most recent MoE precedent.
- Builder warns that **`MoE` is CUDA-only in ONNX Runtime**, and forces `--precision int4` for MoE models. This directly conflicts with `ExecutionProvider.Auto` CPU/DirectML fallback.
- Builder accepts GGUF as input (`-i path_to_gguf_file`) — a real alternate path, but only for architectures the builder already dispatches.
- Repo history (`docs/blocked-models.md`): Mixtral-8x7B is still blocked on MoE routing; Gemma 4 burned significant effort on PLE/variable-head-dim/KV-sharing before landing on "conversion path only, no published artifacts."

---

## Recommendation encoded in the prompt

The prompt below tells Copilot to evaluate, not to assume. But it is deliberately biased toward this ranking, and asks Copilot to disprove it rather than re-derive it:

1. **Nemotron 3.5 Lightning — primary ONNX candidate.** "Nemotron" and "Granite MoE Hybrid" are both already in the GenAI dispatch table, so Mamba-2 state caching and MoE routing have at least partial precedent. Realistic outcome: CUDA-only INT4, `Tier = Large`, no CPU fallback.
2. **Muse Glimmer — second, and only via the Fara multimodal pipeline.** `convert_fara_multimodal.py` already produces the three-file vision package the repo needs. Gated GQA + NoPE-on-full-attention is the novel risk. Video is explicitly out of scope.
3. **A GGUF/llama.cpp backend package is the honest fallback** for both. The repo already proved a non-ONNX sibling package works (`ElBruno.LocalLLMs.BitNet` over bitnet.cpp). If ONNX conversion fails for both models, a `ElBruno.LocalLLMs.Gguf` package unblocks *every* future model release in days instead of months. The plan must cost this out rather than defaulting to another `blocked-models.md` entry.

---

## THE PROMPT

> Copy everything below this line into Copilot plan mode.

---

You are working in the `elbruno/ElBruno.LocalLLMs` repository. **Plan mode only — do not modify any files in this pass.** Produce a written evaluation plan and a GitHub issue body.

### Goal

Evaluate what it would take to add two newly released open-weight models to this library, and produce a concrete, staged, go/no-go plan for each:

1. **Meta Muse Glimmer 30B** — `meta-models/Muse-Glimmer-30B`, Apache 2.0
2. **NVIDIA Nemotron 3.5 Lightning 30B-A3B** — `nvidia/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-BF16`, OpenMDW-1.1

Neither model ships ONNX weights. Both would require conversion to ONNX Runtime GenAI format, or a decision that ONNX is the wrong vehicle for them.

### Step 0 — Read these files before planning anything

Do not plan from memory of the repo. Actually open:

- `src/ElBruno.LocalLLMs/Models/ModelDefinition.cs`, `KnownModels.cs`, `OnnxModelType.cs`, `ChatTemplateFormat.cs`, `ModelTier.cs`
- `src/ElBruno.LocalLLMs/Templates/` — all formatters + `ChatTemplateFactory.cs`
- `src/ElBruno.LocalLLMs/Execution/OnnxGenAIModel.cs`, `OnnxVisionModel.cs`
- `src/ElBruno.LocalLLMs/Download/ModelDownloader.cs`
- `scripts/convert_to_onnx.py`, `scripts/convert_gemma4.py`, `scripts/convert_fara_multimodal.py`
- `docs/blocked-models.md`, `docs/onnx-conversion.md`, `docs/onnx-conversion-fara.md`, `docs/supported-models.md`
- `src/tests/ElBruno.LocalLLMs.Tests/KnownModelsRegistryTests.cs`, `Models/KnownModelsAllPropertiesTests.cs`, `Models/KnownModelsVisionTests.cs`
- `src/tests/ElBruno.LocalLLMs.IntegrationTests/NonNativeOnnxReachabilityTests.cs`
- `.github/copilot-instructions.md`, `.copilot/skills/meai-onnx-library-pattern/SKILL.md`

Report anything in these files that contradicts the facts below. The repo is the source of truth; this prompt is a briefing.

### Verified model facts (as of 2026-08-12)

**Muse Glimmer 30B**
- Dense 30B = 2B ViT "Perception Encoder" + 28B text decoder. Transformers class `MuseGlimmerForConditionalGeneration`, loaded via `AutoModelForMultimodalLM`.
- Text decoder: 52 layers in a repeating `(SWA, SWA, SWA, Full) × 13` pattern. Sliding window = 2048 tokens with RoPE; every 4th layer is full attention with **NoPE** (no positional embedding).
- **Gated** grouped-query attention, 16 query heads per KV head. Q-K RMSNorm applied per head, followed by an extra query scale factor acting as inverse softmax temperature.
- Vision tower: 50 layers, patchify to 2 frames × 3 channels × 14 × 14, linear projection, interpolated learned absolute position embeddings, 2D RoPE on Q/K, window-attention ×3 + full ×1 pattern, GELU MLPs, then 2×2 pixel shuffle before projection into the decoder embedding space.
- Video: same encoder frame-by-frame, 2 fps target, 96-frame cap, timestamped `<|video|>` placeholders interleaved with text.
- `-assistant` repo is a **DFlash block-diffusion drafter**, block size 16 (1 anchor + 15 proposed tokens). Optional.
- Reasoning strength is set in the system prompt: `Reasoning strength: low|medium|high|xhigh`.
- Ships: transformers, llama.cpp (day-0, incl. DFlash), vLLM via transformers backend, MLX, ExecuTorch PTE. No ONNX.

**Nemotron 3.5 Lightning 30B-A3B**
- Hybrid **LatentMoE**: interleaved Mamba-2 SSM layers + MoE layers + select attention layers. 30B total, 3B active. GGUF architecture id is `nemotron_h_moe`.
- LatentMoE projects tokens into a low-rank latent space *before* expert routing.
- **MTP (Multi-Token Prediction) heads are baked into the checkpoint** via a dedicated continued-pretraining stage.
- Context length up to 1M tokens (Mamba-2 gives linear-time scaling).
- Reference weights are BF16. The NVFP4 release is an NVIDIA-specific 4-bit format with custom kernels — **not** an ONNX Runtime quantization format.
- Separate DFlash / DSpark drafter checkpoints exist. Optional.
- License is **OpenMDW-1.1**, not Apache 2.0. This differs from every other model in `KnownModels`.

**ONNX Runtime GenAI constraints**
- The README architecture list already includes **Nemotron** and **Granite MoE Hybrid**. Verify by reading the actual dispatch table in `builder.py` on the installed version — do not trust the README.
- v0.14.0 added Qwen3.5-MoE (35B-A3B). This is the closest MoE precedent.
- The builder warns MoE is **CUDA-only** in ONNX Runtime and forces `--precision int4` for MoE models.
- The builder can take GGUF as input via `-i <gguf_file>`, but only for architectures it already dispatches.
- This repo currently pins `onnxruntime-genai` **0.15.1**.

### Constraints this repo has already learned the hard way

Re-read `docs/blocked-models.md` and treat these as binding:

1. **`genai_config.json` has single-valued fields.** Gemma 4 died on `head_size` because attention params vary per layer. Muse Glimmer's SWA/NoPE alternation and Nemotron's Mamba/MoE/attention interleave both risk the same failure class. Check this *before* spending a conversion run.
2. **A working ONNX export is not a working GenAI model.** Gemma 4 produced a valid 1.6 GB ONNX file that failed at runtime load with `ShapeInferenceError`. The transformers.js / `onnx-community` exports use a different I/O contract and are incompatible with GenAI's external KV cache management. Do not count an `optimum` export as success.
3. **Multimodal weights hide behind a prefix.** Gemma 4's weights sat under `model.language_model.*`. Expect the same for `MuseGlimmerForConditionalGeneration`. Inspect the safetensors index before assuming a text-only extraction is possible.
4. **Conversion RAM is the binary constraint at this size.** Llama-3.3-70B needed ~450 GB on CPU and only succeeded on CUDA. Budget RAM/VRAM explicitly for a 30B target and state which machine each conversion runs on.
5. **Don't reopen a monitor.** The Gemma 4 daily-poll workflow was retired deliberately. One canonical tracking issue per model, terminal-state only, no scheduled comment spam.
6. **Model-as-data.** Adding a model is adding a `ModelDefinition` record, not a class. If either model requires a new class, that is a signal the abstraction needs extending — call it out explicitly as an architecture change, not a model addition.

### What to investigate, in order

**Phase A — Feasibility triage (cheap, do first, no downloads of full weights)**

For each model:

- A1. Fetch `config.json` from the HF repo and record `architectures[0]`, `model_type`, layer counts, head dims, window sizes, and any non-standard fields.
- A2. Grep the installed `onnxruntime_genai/models/builder.py` dispatch table for that architecture string. Record whether it dispatches, falls through, or raises.
- A3. Diff the config's per-layer variability against the fields available in `genai_config.json`. Answer plainly: **can this architecture be represented in a single `genai_config.json`, yes or no?**
- A4. For Nemotron: determine how GenAI's cache manager handles Mamba-2 SSM state vs. transformer KV cache. Use `Granite MoE Hybrid` in the builder as the reference implementation. This is the single highest-information question in the whole evaluation.
- A5. For Muse Glimmer: inspect the safetensors index for the weight prefix layout, and decide whether text-decoder-only extraction is viable or whether the full multimodal path is mandatory.
- A6. Record license obligations. Nemotron is OpenMDW-1.1 — determine whether republishing converted weights to `elbruno/*-onnx` is permitted, and what attribution/notice files must travel with them. **Do not plan an upload until this is answered.**

**Gate A → B:** proceed to conversion only for models where A2 dispatches (or a targeted patch is clearly scoped) *and* A3 is "yes" *and* A6 permits redistribution. Otherwise route straight to Phase D.

**Phase B — Conversion attempt**

- B1. Nemotron first. Start from the **BF16** checkpoint, never NVFP4. Strip MTP heads before export and document exactly which tensors were dropped.
- B2. `--precision int4 --execution_provider cuda`. Record the exact builder invocation, wall-clock time, peak RAM/VRAM, and output size.
- B3. Validate by *loading in GenAI and generating*, not by file existence. Minimum: 20 tokens of coherent output from a fixed prompt, plus a tool-calling round-trip if `SupportsToolCalling` will be true.
- B4. Muse Glimmer second, via the `convert_fara_multimodal.py` pattern — vision ONNX + embedding ONNX + patched `genai_config.json`. Reuse, don't rewrite.
- B5. Log every failure with the exact error and the layer/op it occurred at. These logs are the body of the upstream issue if this fails.

**Phase C — Library integration (only for models that pass B3)**

- C1. `ModelDefinition` records. Propose exact `Id` (lowercase kebab, must pass `KnownModelsRegistryTests`), `DisplayName`, `HuggingFaceRepoId`, `RequiredFiles`, `ModelType`, `ChatTemplate`, `Tier`, `HasNativeOnnx`, `SupportsToolCalling`, `ModelSubPath`.
- C2. **Chat templates.** Both models need new `ChatTemplateFormat` members and formatters. Muse Glimmer needs `reasoning_strength` injected into the system prompt — decide whether that is a formatter concern or a new `LocalLLMsOptions` property, and justify the choice. Nemotron needs its own tool-call syntax verified against the real chat template, not assumed.
- C3. **The CUDA-only MoE problem.** If Nemotron converts CUDA-only, `ExecutionProvider.Auto` will silently fall back to CPU and fail at load. This breaks a documented library promise. Propose a fix: a `RequiredExecutionProvider` or `SupportedProviders` field on `ModelDefinition`, with a clear `ExecutionProviderException` at `CreateAsync` time rather than a native crash. Include the test that proves it.
- C4. Tests: registry tests, formatter tests (including the multilingual suite), `NonNativeOnnxReachabilityTests`, and vision tests if the Glimmer VLM path lands.
- C5. Docs: `docs/supported-models.md`, `docs/onnx-conversion.md`, README "What's New", and a version bump following the existing pattern.

**Phase D — If conversion is not viable**

Do **not** just write a `blocked-models.md` entry and stop. Produce:

- D1. A `docs/blocked-models.md` entry matching the existing format — blocker table, what was tried, what's available now, recommended alternatives.
- D2. One upstream issue on `microsoft/onnxruntime-genai`, modelled on issue #2062 (the Gemma 4 one from this repo). Include the config diff, the exact failing op, and what a fix would need to change. One issue per model, no monitor workflow.
- D3. **A costed proposal for a `ElBruno.LocalLLMs.Gguf` sibling package** wrapping llama.cpp behind `IChatClient`, mirroring how `ElBruno.LocalLLMs.BitNet` wraps bitnet.cpp. Both of these models ship day-0 GGUF and neither ships ONNX — and that pattern is now the norm, not the exception. Estimate: new project scaffolding, native binary acquisition/CI (reuse `build-bitnet-native.yml` as the template), `IChatClient` surface, streaming, tool calling, DI extension, tests, docs. Give a rough effort band and state whether it is worth doing.

### Required output format

Produce two artifacts in your response:

**1. The plan** — markdown, structured as:
- `## Executive summary` — three sentences max per model: verdict (viable / viable-with-caveats / blocked), the single deciding factor, and the recommended next action.
- `## Findings` — one subsection per model, tables where the data is tabular. Cite file paths and line references for anything read from the repo, and URLs for anything read from the web.
- `## Phased plan` — Phases A–D with concrete tasks, each with an owner-agnostic acceptance criterion.
- `## Decision gates` — explicit go/no-go criteria with the observable signal for each.
- `## Risks` — ranked, with mitigation. Include the RAM/VRAM ceiling and the CUDA-only MoE fallout.
- `## Effort estimate` — per phase, in rough bands (hours / days / weeks). No false precision.
- `## Open questions` — anything you could not resolve from the repo or the web.

**2. A GitHub issue body** — ready to paste, following this repo's issue conventions, with a task checklist matching Phase A. One issue covering both models, since the evaluation work overlaps; it can split later.

### Rules

- **Verify, don't assume.** Every architecture claim above should be confirmed against the actual `config.json` and the actual installed `builder.py`. If you find this briefing is wrong, say so prominently — that's a valuable finding, not an inconvenience.
- **No file modifications in this pass.** Plan only.
- If a phase is not worth doing, say so directly and explain why. A well-argued "don't do this" is a better outcome than a plan that burns a week to reach the same conclusion.
- Do not propose a scheduled/polling GitHub Actions workflow for either model.
- Prefer extending `ModelDefinition` over adding new classes. If a new class is unavoidable, flag it as an architecture decision needing separate sign-off.

---

## Notes for Bruno (not part of the prompt)

- **The most likely honest outcome is Phase D for both models.** Nemotron's Mamba-2 + LatentMoE + MTP stack and Glimmer's gated GQA + NoPE alternation are each further from the GenAI builder's assumptions than Gemma 4 was — and Gemma 4 cost real time and still has no published artifacts. Phase A is designed to reach that verdict in hours rather than weeks.
- **D3 is the point of the exercise.** Two flagship open-weight releases in two days, both day-0 on llama.cpp, neither on ONNX. That's a pattern, and a GGUF sibling package would convert a recurring multi-week conversion problem into a same-week model addition. The BitNet package already proves the sibling-package shape works in this repo.
- **Check the OpenMDW-1.1 terms yourself before any `elbruno/*-onnx` upload for Nemotron.** Every other model in the catalog is Apache/MIT/Llama-gated; this is a new license class for the repo and it isn't Copilot's call.
- Worth a look for the campaign side: `nemotron_h_moe` and Muse Glimmer both landing without ONNX support is a clean, concrete hook for a "what local inference in .NET actually costs you" post.
