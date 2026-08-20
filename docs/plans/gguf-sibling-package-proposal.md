# Proposal: `ElBruno.LocalLLMs.Gguf` — GGUF/llama.cpp Sibling Package

**Status:** Proposal — not implementation
**Date:** 2026-08-12
**Owner:** Bruno Capuano (ElBruno)
**Source plan:** [`docs/plans/copilot-plan-new-models-v01.md`](copilot-plan-new-models-v01.md), Phase D3
**Companion docs:** [`docs/blocked-models.md`](../blocked-models.md), [`docs/bitnet-guide.md`](../bitnet-guide.md)
**Recommendation:** **Go — build MVP under an `experimental-gguf` insider preview** *(see [§ 12](#12-recommendation-go--hold--no-go))*

> **Scope note.** This is a proposal artifact. It is not a design doc, and it is
> not a green-light to write product code. The goal is a costed, honest
> answer to the question the plan raises in Phase D3:
> *"if ONNX conversion is not viable for Muse Glimmer 30B and Nemotron 3.5
> Lightning 30B-A3B, is it worth standing up a GGUF sibling package?"*

---

## 1. Why this proposal exists

Two flagship open-weight models landed in the 48 hours before this proposal
was written. **Neither ships ONNX weights. Both ship day-0 GGUF.** From the
Phase-A findings encoded in the source plan:

| Model | Blocker for ONNX | GGUF status |
|---|---|---|
| **Meta Muse Glimmer 30B** *(2026-08-10)* | 52-layer `(SWA, SWA, SWA, Full) × 13` decoder with **NoPE on full-attention layers**, gated GQA, multimodal weights hidden under `model.language_model.*`. Single-valued fields in `genai_config.json` almost certainly cannot represent the mixed SWA/NoPE alternation — the same failure class that killed Gemma 4 (`docs/blocked-models.md` § Gemma 4). | Meta ships `-GGUF` day-0 with calibrated k-quants; llama.cpp supports it day-0 including DFlash drafter. |
| **NVIDIA Nemotron 3.5 Lightning 30B-A3B** *(2026-08-11)* | `nemotron_h_moe` — hybrid Mamba-2 SSM + LatentMoE + selective attention. ONNX Runtime GenAI's `MoE` op is **CUDA-only** and forces `--precision int4`, and Mamba-2 SSM state does not fit GenAI's KV-cache manager cleanly. **License is OpenMDW-1.1** (not Apache/MIT — a first for the catalog). | `ggml-org/…-GGUF` and `bartowski/…-GGUF` Q4_K_M (~22–25 GB) available; llama.cpp, Ollama, LM Studio, Unsloth all day-0. |

The plan's honest expectation for both models is Phase D (no ONNX). This is
also the shape of the last three high-profile blockers: **Devstral-Small-2**
is explicitly parked with *"or use GGUF via llama.cpp"* as its recommended
workaround (`docs/blocked-models.md`), **Mixtral-8x7B** is still MoE-blocked,
and **Gemma 4** cost weeks to reach a "conversion path only" state that
still has no published `elbruno/*-onnx` artifacts.

**The pattern is now the norm, not the exception.** A managed GGUF path
converts a recurring multi-week conversion problem into a same-week model
addition. And this repo has already proved the sibling-package shape works:
`ElBruno.LocalLLMs.BitNet` wraps bitnet.cpp (a llama.cpp fork) via P/Invoke
behind `IChatClient` with the same `ChatTemplateFormat` catalog, the same
HuggingFace downloader, and the same DI story the ONNX package uses
(`src/ElBruno.LocalLLMs.BitNet/BitNetChatClient.cs`,
`.../BitNetServiceExtensions.cs`,
`.../BitNetModelDownloader.cs`).

Standing up `ElBruno.LocalLLMs.Gguf` is not a new pattern — it is the same
pattern applied to *upstream* llama.cpp instead of the bitnet.cpp fork, so
it inherits GPU offload, wider architecture coverage, and per-tensor quant
support that bitnet.cpp does not target.

---

## 2. Non-goals

To keep the MVP shippable:

- **Not a llama.cpp binding rewrite.** No feature parity with `LLamaSharp`
  or `llama-cpp-python`. We wrap a narrow surface: model load, tokenize,
  batch decode, sample, detokenize, free.
- **Not a GGUF creator.** We consume `.gguf` files; we do not convert
  safetensors → GGUF ourselves. Point users at upstream `convert_hf_to_gguf.py`.
- **Not a training/fine-tuning path.** LoRA at inference time is a Phase 2
  question, not MVP.
- **Not multimodal on day one.** Muse Glimmer's vision tower ships as a
  separate mmproj GGUF; wire it up in a follow-up (§ 5.6). The MVP is
  text-only.
- **Not a replacement for `ElBruno.LocalLLMs`.** ONNX Runtime GenAI stays
  the default recommendation for models that have native ONNX. GGUF is the
  fallback for models that do not.

---

## 3. MVP package structure

Mirror the BitNet layout exactly. This is the shape that already ships and
is already accepted by the CI/publish workflows.

```
src/
├── ElBruno.LocalLLMs.Gguf/                       # managed IChatClient
│   ├── ElBruno.LocalLLMs.Gguf.csproj             # net8.0;net10.0, PackageId ElBruno.LocalLLMs.Gguf
│   ├── GgufChatClient.cs
│   ├── GgufOptions.cs
│   ├── GgufModelDefinition.cs
│   ├── GgufKnownModels.cs
│   ├── GgufModelDownloader.cs                    # uses ElBruno.HuggingFace.Downloader
│   ├── GgufServiceExtensions.cs                  # AddGgufChatClient(...)
│   ├── GgufInferenceException.cs
│   ├── GgufNativeLibraryException.cs
│   ├── GgufQuantization.cs                       # Q4_K_M, Q5_K_M, Q6_K, Q8_0, F16
│   ├── GgufAccelerator.cs                        # Cpu, Cuda, Metal, Vulkan
│   └── Native/
│       ├── LlamaNative.cs                        # P/Invoke, "llama" DllImport name
│       ├── LlamaSampler.cs
│       └── NativeLibraryLoader.cs
├── ElBruno.LocalLLMs.Gguf.Native.win-x64/        # netstandard2.0, NoBuild, runtimes\win-x64\native\llama.dll
├── ElBruno.LocalLLMs.Gguf.Native.linux-x64/      # netstandard2.0, NoBuild, runtimes/linux-x64/native/libllama.so
├── ElBruno.LocalLLMs.Gguf.Native.osx-arm64/      # netstandard2.0, NoBuild, runtimes/osx-arm64/native/libllama.dylib
└── tests/
    └── ElBruno.LocalLLMs.Gguf.Tests/             # xUnit, net8.0
```

**Managed csproj** copies the shape of
`src/ElBruno.LocalLLMs.BitNet/ElBruno.LocalLLMs.BitNet.csproj` verbatim,
except `PackageId` / `PackageTags` / `Description`, and `ProjectReference` to
`..\ElBruno.LocalLLMs\ElBruno.LocalLLMs.csproj` (to reuse
`ChatTemplateFormat`, `IChatTemplateFormatter`, `ModelDownloadProgress`,
`ChatTemplateFactory`).

**Native csprojs** mirror
`ElBruno.LocalLLMs.BitNet.Native.linux-x64.csproj`:
`TargetFramework=netstandard2.0`, `NoBuild=true`,
`IncludeBuildOutput=false`, `NoWarn=$(NoWarn);NU5128`, and a single
`<None Include="runtimes\{rid}\native\{binary}" Pack="true" PackagePath="runtimes\{rid}\native\" />`.

**Solution:** add all four projects + the test project to
`ElBruno.LocalLLMs.slnx` under `/src/` and `/src/tests/`.

---

## 4. Package boundary — reuse vs. duplicate

**Reused from `ElBruno.LocalLLMs`** (via `ProjectReference`,
`InternalsVisibleTo` if needed):

- `ChatTemplateFormat` enum + all `*Formatter` classes and
  `ChatTemplateFactory` (`src/ElBruno.LocalLLMs/Templates/*`). Both new
  models can be expressed in existing formats plus one addition (§ 5.5).
- `ModelDownloadProgress` record and the whole
  `ElBruno.HuggingFace.Downloader` pipeline
  (`src/ElBruno.LocalLLMs.BitNet/BitNetModelDownloader.cs` shows the exact
  reuse pattern).
- `IChatClient`, `ChatMessage`, `ChatOptions`, `ChatResponseUpdate` from
  `Microsoft.Extensions.AI.Abstractions` 10.8.3 — pinned to the same
  version as the ONNX and BitNet packages to avoid MEAI drift.

**New in `ElBruno.LocalLLMs.Gguf`**:

- `GgufModelDefinition` — parallel to `BitNetModelDefinition`
  (`src/ElBruno.LocalLLMs.BitNet/BitNetModelDefinition.cs`) but with
  additional fields for llama.cpp specifics: `Quantization`,
  `SupportsGpuOffload`, `MinContextLength`, `RecommendedContextLength`,
  `RequiredMmproj` *(nullable, non-null only for future VLMs like Muse
  Glimmer)*, `LicenseId`.
- `GgufKnownModels` — the catalog. MVP entries listed in § 5.4.
- `GgufChatClient` — same public shape as `BitNetChatClient`
  (`.CreateAsync(GgufOptions, IProgress<ModelDownloadProgress>?, …)`).
- `GgufOptions` — MVP fields listed in § 5.3.

**Not reused, deliberately separate from BitNet:**

- P/Invoke signatures. The bitnet.cpp fork tracks a specific llama.cpp
  commit and defines a matching `LlamaModelParams` / `LlamaContextParams`
  layout (`src/ElBruno.LocalLLMs.BitNet/Native/LlamaNative.cs`). Upstream
  llama.cpp evolves those structs frequently. A separate `LlamaNative` in
  `ElBruno.LocalLLMs.Gguf.Native` pins to a specific upstream commit and
  changes independently.
- Native library name. BitNet ships `llama` DllImport too, but the
  binaries **are not interchangeable** — bitnet.cpp includes ternary
  kernels the upstream lacks, and upstream includes GPU offload,
  MoE routing, and Mamba-2 SSM support the fork lacks. Same DllImport
  name is fine because the two packages ship separate native NuGets that
  land in different `runtimes/{rid}/native/` folders and are consumed by
  different managed assemblies — the `NativeLibraryLoader` in each
  package searches its **own** RID path first
  (`src/ElBruno.LocalLLMs.BitNet/Native/NativeLibraryLoader.cs:126-149`).

---

## 5. Public API surface (MVP)

### 5.1 `IChatClient` + streaming

Identical shape to `BitNetChatClient`. Two constructors (sync +
`ILoggerFactory`), two `CreateAsync` factories (with and without a
`IProgress<ModelDownloadProgress>?`). Both `GetResponseAsync` and
`GetStreamingResponseAsync` are implemented; the sync path calls
`Task.Run(GenerateResponse, ct)` behind an `_inferenceLock` semaphore.
Cancellation is honoured every token via `ct.ThrowIfCancellationRequested()`
— same as `BitNetChatClient.GenerateResponse` /
`GenerateStreamingResponse` in
`src/ElBruno.LocalLLMs.BitNet/BitNetChatClient.cs`.

`IAsyncDisposable` + `IDisposable`. Same `DisposeCore` shape: free
context, free model, `llama_backend_free`.

### 5.2 DI extension

```csharp
services.AddGgufChatClient(opts =>
{
    opts.Model = GgufKnownModels.NemotronLightning30B_Q4KM;
    opts.Accelerator = GgufAccelerator.Cuda;   // Cpu is default
    opts.ContextSize = 32_768;
});
```

`GgufServiceExtensions.AddGgufChatClient` mirrors
`src/ElBruno.LocalLLMs.BitNet/BitNetServiceExtensions.cs` line-for-line:
`AddSingleton<GgufOptions>`, then `AddSingleton<IChatClient>` that resolves
`GgufOptions` + optional `ILoggerFactory`.

### 5.3 `GgufOptions`

Copy `BitNetOptions` and add three llama.cpp-specific properties:

| Property | Type | Default | Notes |
|---|---|---|---|
| `Model` | `GgufModelDefinition` | `GgufKnownModels.NemotronLightning30B_Q4KM` (MVP flagship) | Same shape as `BitNetOptions.Model` |
| `ModelPath` | `string?` | `null` | GGUF file. When null, auto-download from HF. |
| `MmprojPath` | `string?` | `null` | *Reserved for VLM follow-up; MVP ignores.* |
| `NativeLibraryPath` | `string?` | `null` | Same resolution rules as BitNet: NuGet `runtimes/{rid}/native/` first, then env vars. |
| `CacheDirectory` | `string?` | `%LOCALAPPDATA%/ElBruno/LocalLLMs/models` | **Shared with BitNet + ONNX** — one cache to rule them all. |
| `EnsureModelDownloaded` | `bool` | `true` | |
| `Accelerator` | `GgufAccelerator` | `Cpu` | `Cpu` / `Cuda` / `Metal` / `Vulkan`. Requires a matching native package (§ 6). Falls back to CPU with a **logged warning** if the requested backend is not available. |
| `GpuLayerCount` | `int` | `-1` (= all layers when Accelerator ≠ Cpu, 0 when Cpu) | Maps to llama.cpp `n_gpu_layers`. |
| `ContextSize` | `int` | `4096` | Nemotron and Muse Glimmer both support far more; users opt in. |
| `MaxTokens` / `Temperature` / `TopP` / `TopK` / `RepetitionPenalty` / `ThreadCount` / `SystemPrompt` / `ChatTemplateOverride` | | | Copy verbatim from `BitNetOptions`. |

### 5.4 Model-data surface — MVP catalog

`GgufKnownModels` — six entries at MVP. The two triggering models plus
four that unblock existing entries in `docs/blocked-models.md`:

| Id | HF repo | GGUF file | Quant | License | Notes |
|---|---|---|---|---|---|
| `nemotron-3.5-lightning-30b-a3b-q4km` | `bartowski/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-GGUF` | `NVIDIA-Nemotron-3.5-Lightning-30B-A3B-Q4_K_M.gguf` | Q4_K_M | **OpenMDW-1.1** | Flagship. Requires license text in downloaded folder — enforce in downloader. |
| `nemotron-3.5-lightning-30b-a3b-q8` | *(same repo)* | `…-Q8_0.gguf` | Q8_0 | OpenMDW-1.1 | High-fidelity variant for GPU users. |
| `muse-glimmer-30b-q4km` | `meta-models/Muse-Glimmer-30B-GGUF` | `Muse-Glimmer-30B-Q4_K_M.gguf` | Q4_K_M | Apache-2.0 | **Text-only mode at MVP.** Vision requires a follow-up (§ 5.6). |
| `mixtral-8x7b-instruct-q4km` | `TheBloke/Mixtral-8x7B-Instruct-v0.1-GGUF` | `mixtral-8x7b-instruct-v0.1.Q4_K_M.gguf` | Q4_K_M | Apache-2.0 | Unblocks `docs/blocked-models.md` § Mixtral. |
| `devstral-small-2-24b-q4km` | *(upstream Devstral GGUF once mirrored)* | `Devstral-Small-2-24B-Q4_K_M.gguf` | Q4_K_M | Apache-2.0 | Unblocks `docs/blocked-models.md` § Devstral. |
| `stablelm-2-1.6b-zephyr-q6k` | *(community GGUF)* | `stablelm-2-zephyr-1_6b-Q6_K.gguf` | Q6_K | Non-commercial | Unblocks `docs/blocked-models.md` § StableLM. |

Model IDs follow the kebab-case rule enforced by
`src/tests/ElBruno.LocalLLMs.Tests/KnownModelsRegistryTests.cs`. The MVP
catalog test lives in the new Gguf test project and enforces the same
invariants: unique IDs, non-empty display names, resolvable HF repos.

### 5.5 Chat templates

- **Nemotron 3.5 Lightning** — verify against the chat template embedded in
  the GGUF file. Expected to map to a Nemotron-specific format
  (`<|system|>`, `<|user|>`, `<|assistant|>` variant). **Add a new
  `ChatTemplateFormat.Nemotron` member and a `NemotronFormatter`** to the
  shared `ElBruno.LocalLLMs/Templates/` folder. Rationale: this is a
  library-wide capability, not a Gguf-only concern; a future ONNX
  Nemotron would need the same formatter.
- **Muse Glimmer** — text-only mode uses ChatML variant with a
  **`Reasoning strength: {low|medium|high|xhigh}`** injection in the
  system prompt. Two options:
  1. **`GgufOptions.ReasoningStrength` enum** — Gguf-specific, doesn't
     leak into `LocalLLMsOptions`. Formatter reads it via a new
     `IReasoningAwareFormatter` interface.
  2. **New `LocalLLMsOptions.ReasoningStrength`** — library-wide, so any
     future reasoning-controlled model (o1-style, DeepSeek-R1) reuses it.

  **Recommendation for MVP: option 1**, because option 2 is a public-API
  change to the base package that deserves its own proposal.

- **Existing models** in the MVP catalog (Mixtral → Mistral, Devstral →
  Mistral, StableLM → ChatML) use existing formatters unchanged.

### 5.6 Multimodal (deferred, out of MVP)

Muse Glimmer's 2B ViT Perception Encoder ships as a separate `mmproj-*.gguf`
in the same HF repo. llama.cpp loads it via `llama_mmproj_from_file` /
`llama_batch_decode` with an image encoder path. This is a **second-pass**
feature:

- Managed side: `GgufVisionChatClient : LocalVisionChatClient`-shaped
  API, `GgufOptions.MmprojPath`, `LlamaNative.llama_mmproj_*` bindings.
- Native side: no change — the same `llama` binary already includes
  llava/qwen-vl support upstream.
- Explicitly deferred to a follow-up spike; MVP tests assert
  `Muse-Glimmer.IsVisionCapable == false` in Gguf mode until then.

### 5.7 Tool calling scope (MVP)

**In scope for MVP:**
- Reuse `IToolCallParser` and `ToolCallParserFactory` from
  `src/ElBruno.LocalLLMs/ToolCalling/`. `GgufChatClient.BuildPrompt` passes
  `options.Tools` through the formatter (same as
  `BitNetChatClient.BuildPrompt`).
- **Nemotron** — chat template ships with a Nemotron-specific tool-call
  syntax; add a `NemotronJsonToolCallParser` extending `JsonToolCallParser`
  if the format differs.
- **Mixtral** / **Devstral** — reuse existing `MistralFormatter` +
  `JsonToolCallParser`; no new code.

**Out of scope for MVP:**
- Muse Glimmer tool calling — deferred with vision, because Meta's tool
  spec is only defined for the multimodal path in the model card. Set
  `SupportsToolCalling = false` in the Muse Glimmer `GgufModelDefinition`
  until the vision follow-up.
- Non-JSON tool syntaxes (XML, Hermes, etc.). None of the MVP models
  need them.

---

## 6. Native binary acquisition + CI/RIDs

### 6.1 RIDs at MVP

Match the BitNet native RIDs 1:1:

| RID | Runner | Binary | Package |
|---|---|---|---|
| `win-x64` | `windows-latest` | `llama.dll` | `ElBruno.LocalLLMs.Gguf.Native.win-x64` |
| `linux-x64` | `ubuntu-latest` | `libllama.so` | `ElBruno.LocalLLMs.Gguf.Native.linux-x64` |
| `osx-arm64` | `macos-latest` | `libllama.dylib` | `ElBruno.LocalLLMs.Gguf.Native.osx-arm64` |

**Deferred RIDs** (`win-arm64`, `linux-arm64`, `osx-x64`): explicitly
listed as "🔄 Planned" in the BitNet guide table
(`docs/bitnet-guide.md`). Same posture here.

**GPU variants** — see § 6.3.

### 6.2 CI workflow — `.github/workflows/build-gguf-native.yml`

Fork `.github/workflows/build-bitnet-native.yml` verbatim, then diff:

1. `env.LLAMA_CPP_COMMIT` — pin a specific upstream `ggerganov/llama.cpp`
   commit SHA (the BitNet workflow's `env.BITNET_CPP_COMMIT: main` is a
   `TODO: Pin` in the file itself; do it correctly for llama.cpp from day
   one).
2. Replace `git clone --recursive https://github.com/microsoft/BitNet.git`
   with `git clone https://github.com/ggerganov/llama.cpp.git`.
3. Replace `python setup_env.py --hf-repo …` with a direct CMake build:
   ```bash
   cmake -B build -DGGML_NATIVE=OFF -DBUILD_SHARED_LIBS=ON -DLLAMA_CURL=OFF
   cmake --build build --config Release -j
   ```
   Rationale: no Python step, no HF download during CI, faster.
4. Same "find binary → copy to `staging/` → `upload-artifact`" tail — the
   binary names (`llama.dll`, `libllama.so`, `libllama.dylib`) are
   identical, so this block is copy-paste.
5. **Add a smoke test step per runner** that the BitNet workflow omits:
   after copying, run a 10-line C# test project that loads the library,
   calls `llama_backend_init` / `llama_backend_free`, and exits non-zero
   on failure. Catches ABI drift before publish.

### 6.3 Publish workflow — `.github/workflows/publish-gguf-native.yml`

Fork `.github/workflows/publish-bitnet-native.yml` verbatim, diff:

1. `uses: ./.github/workflows/build-gguf-native.yml`.
2. Version-extraction fallback path points at
   `src/ElBruno.LocalLLMs.Gguf.Native.win-x64/…csproj`.
3. Three `dotnet pack` lines pointed at the Gguf native csprojs.
4. Same OIDC login (`NuGet/login@v1` + `NUGET_USER` secret + `release`
   environment).
5. **Release tag convention** — the BitNet publish key on `native-v*`
   tag pattern (`VERSION="${VERSION#native-v}"`). Use `gguf-native-v*` for
   the sibling to avoid tag collision.

### 6.4 Managed publish

Extend the existing `.github/workflows/publish.yml` (or add a
`publish-gguf.yml`) to `dotnet pack` the managed `ElBruno.LocalLLMs.Gguf`
csproj. It has no native content — same OIDC flow, no per-RID matrix.

### 6.5 GPU variants (Phase 2, costed here)

llama.cpp supports CUDA, Metal (built-in on macos-arm64), and Vulkan.
Adding CUDA is the only interesting one for MVP+1:

- **Windows/Linux CUDA:** two extra native packages
  (`ElBruno.LocalLLMs.Gguf.Native.win-x64-cuda`,
  `.Native.linux-x64-cuda`). Runners need CUDA toolkit; use
  `Jimver/cuda-toolkit@v0.2.14` action to install CUDA on `ubuntu-latest`
  / `windows-latest`. Rough CI time: +12 min per RID.
- **Metal:** the `osx-arm64` binary already includes Metal automatically
  when built on macOS. No extra package needed; enable at runtime via
  `GgufAccelerator.Metal`.
- **DirectML:** llama.cpp does not have a DirectML backend. Do not offer
  one — align the docs to "DirectML → use the ONNX package."

**MVP posture: ship CPU-only.** Add CUDA in the first patch release once
CPU is stable. Metal ships free with the `osx-arm64` package.

### 6.6 Licence propagation

The Nemotron model requires OpenMDW-1.1 attribution shipping alongside the
weights. `GgufModelDownloader.EnsureModelAsync` must download the LICENSE
file into the model cache directory next to the `.gguf`, and log a warning
if the file was not present in the HF repo. Enforced by a Gguf-specific
`ILicenseDownloader` in `Native/` — MVP feature, not deferred.

---

## 7. Testing

New test project: `src/tests/ElBruno.LocalLLMs.Gguf.Tests`.

Structure copies `src/tests/ElBruno.LocalLLMs.BitNet.Tests/` file-for-file:

| BitNet test file | Gguf equivalent | Purpose |
|---|---|---|
| `BitNetChatClientTests.cs` | `GgufChatClientTests.cs` | Constructor/dispose/CreateAsync semantics with a mocked `LlamaNative`. |
| `BitNetOptionsTests.cs` | `GgufOptionsTests.cs` | Default values, mutation, null-guards. |
| `BitNetKnownModelsTests.cs` | `GgufKnownModelsTests.cs` | Every catalog entry: unique kebab-case id, non-empty display, resolvable HF repo, valid quantization enum. |
| `BitNetModelDownloaderTests.cs` | `GgufModelDownloaderTests.cs` | Cache hit path, download path, license file assertion (Nemotron). |
| `BitNetServiceExtensionsTests.cs` | `GgufServiceExtensionsTests.cs` | DI registration resolves `IChatClient`. |
| `NativeLibraryLoaderTests.cs` | *(same name)* | Candidate-path enumeration on each RID. |
| `NativePackageValidationTests.cs` | *(same name)* | Assert that the three native `.nupkg`s each contain exactly the expected `runtimes/{rid}/native/{binary}` file. |
| — new — | `GgufChatTemplateTests.cs` | Format expectations for `NemotronFormatter` (new) and reused formatters. |

**Integration tests** (`src/tests/ElBruno.LocalLLMs.IntegrationTests`) —
add one `NonNativeGgufReachabilityTests.cs` matching the existing
`NonNativeOnnxReachabilityTests.cs`: HF `HEAD` request per catalog entry,
asserts the GGUF file is fetchable without downloading. No live inference
in CI — model weights are too large for GitHub runner disk.

**Manual E2E** (documented, not automated): "run the sample against
Nemotron Q4_K_M on a 32GB CUDA box" — checked off in the release notes for
each catalog entry.

---

## 8. Docs and sample work

### 8.1 New docs

| File | Content |
|---|---|
| `docs/gguf-guide.md` | Mirror of `docs/bitnet-guide.md`: what GGUF is, when to pick it over ONNX and over BitNet, install snippet for the two NuGets, quick start, streaming, DI, supported-models table, options table, comparison table, troubleshooting. Include a "Nemotron OpenMDW-1.1" callout box. |
| `docs/plans/gguf-sibling-package-proposal.md` | *(this file)* — remains as historical rationale. |

### 8.2 Updated docs

| File | Change |
|---|---|
| `docs/supported-models.md` | New section "🟩 GGUF models (llama.cpp)" mirroring the "🟦 BitNet models" section, with the six MVP entries. |
| `docs/blocked-models.md` | Add a header banner: *"Models blocked from ONNX conversion but supported via `ElBruno.LocalLLMs.Gguf`: Nemotron 3.5 Lightning 30B-A3B, Mixtral-8x7B, Devstral-Small-2-24B, StableLM-2-1.6B, Muse Glimmer 30B (text-only)"*. Cross-link each individual blocker entry to `docs/gguf-guide.md`. |
| `README.md` | Add a "GGUF Sibling Package" section right after the BitNet section. Update the "What's New" list — replace the oldest entry with "GGUF sibling package (experimental) — Nemotron 3.5 Lightning + Muse Glimmer + Mixtral + Devstral + StableLM catalog." |
| `docs/CHANGELOG.md` | Version bump + entry. |

### 8.3 Samples

New folder `src/samples/GgufChatSample/` — one-file console
sample that prints token-by-token generation from Nemotron 3.5 Lightning
Q4_K_M. Mirrors the existing BitNet sample if any exists (if not, create
one for both at the same time and cross-reference).

---

## 9. Rough effort bands

Bands reflect *my* estimate as author of the BitNet package, not any
team's average velocity. Numbers cover engineering + docs + tests, not
review latency.

| Phase | Bucket | Band | Notes |
|---|---|---|---|
| **0** | Repo scaffolding (four csprojs, slnx entries, InternalsVisibleTo, initial csproj metadata) | **0.5 day** | Pure copy-paste from BitNet. |
| **1** | P/Invoke layer for upstream llama.cpp (`LlamaNative`, `LlamaSampler`, `NativeLibraryLoader`), pinned to a specific commit | **2–3 days** | The BitNet P/Invoke was ~2 days; upstream llama.cpp has larger structs (GPU offload, MoE state) — budget an extra day. |
| **2** | `GgufChatClient` end-to-end (init → tokenize → decode → sample → stream), single-model manual smoke test on a Q4_K_M file locally | **1.5 days** | ~90 % copy-paste from `BitNetChatClient`. |
| **3** | `GgufOptions`, `GgufKnownModels` catalog (6 entries), `GgufModelDefinition`, `GgufModelDownloader` including OpenMDW-1.1 handling | **1 day** | |
| **4** | DI (`AddGgufChatClient`), `NemotronFormatter` + `ChatTemplateFormat.Nemotron`, tool-call parser verification | **1 day** | Formatter is a small addition to the base package. |
| **5** | CI workflow — `build-gguf-native.yml`, `publish-gguf-native.yml`, smoke-test step, tag convention | **1.5 days** | The workflow surgery is short; iterating the runner setup for CMake/CUDA is where time goes. |
| **6** | Test suite (managed) + `NonNativeGgufReachabilityTests` integration test | **1 day** | Test parity with BitNet is ~1:1. |
| **7** | Docs (`gguf-guide.md`, `supported-models.md` update, `blocked-models.md` banner, README section, sample) | **1 day** | |
| **8** | End-to-end validation: three models × three RIDs, one release cycle through insider preview into a v0.x.0 tag | **2–3 days** | Includes the first NuGet publish and any OIDC / trusted-publisher setup for the new package IDs. |
| — | **MVP total** | **~11–14 days** = **~2.5 weeks solo, ~1.5 weeks with review overlap** | |
| **9** | GPU variants (CUDA on win-x64 + linux-x64) — patch release | **+3–4 days** | |
| **10** | Multimodal (Muse Glimmer vision) — follow-up minor | **+4–6 days** | Vision surface, image preprocessing, mmproj loading. |
| **11** | Tool calling for Muse Glimmer + Nemotron regression suite | **+2 days** | |

**Compare to ONNX conversion cost for these two models** (§ Notes for Bruno
in the source plan and the Gemma 4 blocker section): each of Nemotron and
Muse Glimmer would need Phase A (0.5 day) + Phase B (~1 week if runtime
already supports the arch, indefinite otherwise) + Phase C (~1 week
integration) + upstream issue tracking + hosting the converted weights on
HF. Expected outcome from the source plan: Phase D for both. **Net: the
GGUF MVP is faster than *one* successful ONNX conversion, and unblocks
five catalog entries at once.**

---

## 10. Risks

Ranked by impact × likelihood, with mitigation.

| # | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| 1 | **Upstream llama.cpp ABI churn.** `LlamaModelParams` / `LlamaContextParams` / `LlamaBatch` change on a weekly cadence upstream. A pinned commit fixes today but blocks new architectures tomorrow. | High | High | **Pin a specific commit SHA** in `build-gguf-native.yml`. Adopt an **N-week ABI review cadence** — bump the commit + re-verify P/Invoke structs. Never track `main`. Follows the "`TODO: Pin`" the BitNet workflow already documents. |
| 2 | **OpenMDW-1.1 redistribution risk.** The Nemotron licence is new to this repo. If we mirror the GGUF to `elbruno/*` for stability, the licence obligations are stricter than Apache. | High | Medium | **Do not mirror.** Point the downloader at `bartowski/…-GGUF` and `ggml-org/…-GGUF` directly. Ship the LICENSE next to the weights in the local cache. Add a docs banner. Legal sign-off is documented in `blocked-models.md` before catalog entry lands. |
| 3 | **Native binary bloat.** Upstream llama.cpp built without curl/server is ~4–8 MB per RID. With CUDA, ~250 MB per RID. | Medium | High | MVP ships **CPU-only NuGets** — small footprint. CUDA lives in separate `*.Native.*-cuda` packages so users opt in. Mirrors the ONNX pattern (`Microsoft.ML.OnnxRuntimeGenAI.Cuda` is a separate NuGet from `Microsoft.ML.OnnxRuntimeGenAI`). |
| 4 | **CI runner disk pressure.** llama.cpp CMake build is small, but the smoke-test step needs a real GGUF file. A 22 GB Nemotron Q4_K_M does not fit on a GitHub runner. | Medium | High | Smoke-test with the **tiniest** Q2_K community GGUF (e.g. `TinyLlama-1.1B` Q2_K ~ 400 MB). Only verifies the P/Invoke loads and decodes; larger models tested manually. |
| 5 | **Nemotron `nemotron_h_moe` arch novelty.** llama.cpp support just landed. Regressions on Mamba-2 SSM state caching are plausible in the next 4–6 weeks. | Medium | Medium | Pin the llama.cpp commit **after** the Nemotron support has soaked for 2 weeks upstream. Track upstream issues per catalog entry. |
| 6 | **Muse Glimmer vision path becomes a rabbit hole.** DFlash drafter + mmproj + reasoning_strength interaction is untested. | Medium | Medium | **Vision explicitly out of MVP** (§ 5.6). Fail closed: `MuseGlimmer.SupportsVision == false` until Phase 10. |
| 7 | **Windows CUDA build fragility on `windows-latest`.** Toolkit versions and MSBuild integration drift more than on Linux. | Medium | Medium | CUDA is Phase 2, not MVP. Even then, gate the CUDA job with `continue-on-error: false` but a **manual dispatch** rather than automatic on every push. |
| 8 | **DllImport name collision between BitNet and Gguf** if a user installs both native packages. Both packages ship a library named `llama` under `runtimes/{rid}/native/`. .NET's native library loader picks whichever is nearest on the search path. | Low | Low | Deployed apps almost never install both packages; the two clients target different scenarios. If it becomes a real problem, **rename the Gguf DllImport target to `llama_upstream`** at build time via `-DLLAMA_LIB_NAME=llama_upstream` — CMake supports it. Cost: one-line CMake flag, matching rename in `Native/LlamaNative.cs`. |
| 9 | **User support burden.** GGUF has many quantizations, samplers, and templates. Users will ask why Q6_K is slower than Q4_K_M, why their template is wrong, etc. | Low | High | Documentation heavy from day 1 (§ 8). Ship a diagnostic method `GgufDiagnostics.DescribeGguf(path)` that prints the embedded chat template + quantization + arch. Not blocking MVP but budget in Phase 7. |
| 10 | **Cannibalisation of ONNX package.** If GGUF "just works" for everything, users abandon ONNX. | Low | Low | ONNX still wins for DirectML, official Microsoft support, and Phi/Qwen native ONNX artifacts. Docs explicitly recommend ONNX first, GGUF as fallback. |

---

## 11. Open questions

1. **Model IDs across packages.** BitNet uses `bitnet-b1.58-2b-4t`; should
   Gguf mirror that with `gguf-` prefix? Or is the HF repo unambiguous
   enough? **Recommendation:** no prefix — IDs live under
   `GgufKnownModels`, so the namespace disambiguates. But confirm with the
   existing catalog conventions before merging.
2. **Shared model cache directory.** `%LOCALAPPDATA%/ElBruno/LocalLLMs/models`
   is used by ONNX and BitNet today. If a user pulls both Phi-3.5 ONNX and
   `microsoft/Phi-3.5-mini-instruct-GGUF`, will directory names collide?
   **Recommendation:** namespace by `RepoId/FileName`, which is what
   `BitNetModelDownloader` already does via
   `SanitizeModelId(model.Id)`.
3. **`ChatTemplateFormat.Nemotron` vs. `Custom`.** Adding a new enum
   member is a public-API change to the base package. Is that acceptable
   for a Gguf sibling addition? **Recommendation:** yes — the enum is
   designed to grow (see `Fara` addition for Fara VLM). But formalise
   with a note in `docs/CHANGELOG.md`.
4. **Insider-preview channel.** Ship the MVP as `0.x.0-preview` under the
   Squad insider channel first? The repo has
   `.github/workflows/squad-insider-release.yml`. **Recommendation:** yes.
   First release is preview until three model smoke tests pass in the
   real world.
5. **Should `experimental` show up in the package ID?** e.g.
   `ElBruno.LocalLLMs.Gguf.Preview`. Argument for: reduces expectation of
   stability. Argument against: package rename later is painful.
   **Recommendation:** no — ship `ElBruno.LocalLLMs.Gguf` v0.x.0-preview
   and rely on the `-preview` suffix.

---

## 12. Recommendation: go / hold / no-go

**Go, as an MVP scoped to § 3 non-goals, on a 2.5-week solo band.**

**Reasoning:**

- Two flagship 2026 releases in one week, both GGUF-day-0, neither ONNX.
  The source plan
  (`docs/plans/copilot-plan-new-models-v01.md`, "Notes for Bruno") calls
  this a pattern; the evidence supports it.
- The BitNet sibling has already normalised the shape. Every layer — DI,
  downloader, chat templates, P/Invoke loader, native NuGet split, CI/OIDC
  publish — is copy-paste plus deltas, not novel design.
- One MVP unblocks **five existing catalog entries** in `blocked-models.md`
  (Nemotron, Muse Glimmer, Mixtral, Devstral, StableLM), each of which
  would otherwise cost weeks of ONNX conversion work with the Gemma 4
  probability of failure.
- The failure modes are known and bounded (§ 10). None of the top-ranked
  risks are architecture-breaking; the worst-case is "pin an older
  llama.cpp commit and skip the newest model until upstream stabilises."

**What would flip this to Hold:**

- If a Phase-B ONNX conversion for Nemotron 3.5 Lightning succeeds on the
  first attempt (§ Phase B in the source plan). That's the only scenario
  where the recurring-blocker argument goes away. The source plan itself
  rates this outcome unlikely.
- If OpenMDW-1.1 turns out to prohibit redistribution in a form the
  downloader path cannot honour. That's a legal question, not an
  engineering one — must be answered before the Nemotron catalog entry
  ships.

**What would flip this to No-go:**

- If `LLamaSharp` (community binding) adopts the sibling-package shape
  first with `Microsoft.Extensions.AI` support at the level this repo
  needs. Currently it does not: `LLamaSharp` targets its own executor
  interface, not `IChatClient`. But this is worth a quarterly re-check.

**Concrete next step if go is accepted:**

1. Open a tracking issue "ElBruno.LocalLLMs.Gguf MVP" mirroring the Phase
   A checklist from `copilot-plan-new-models-v01.md`.
2. Land Phase 0 + 1 (scaffolding + P/Invoke) behind a feature branch on
   the insider channel.
3. First public preview NuGet only after Phase 4 (Nemotron generates real
   tokens end-to-end on Windows x64 CPU).
4. Do not update `docs/blocked-models.md` cross-links until the MVP tag
   ships — the doc's authority depends on those cross-links being live.

---

*End of proposal.*
