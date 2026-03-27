# RAG Tool Routing — Implementation Plan

> **Author:** Morpheus (Lead/Architect)  
> **Date:** 2026-03-27  
> **Status:** Approved — ready for execution  
> **Requested by:** Bruno Capuano

---

## Goal

Build a fully local RAG pipeline for MCP tool selection: user prompt → embedding search → optional SLM reasoning → tool(s) selected. The SLM selects which tool(s) to invoke from a large catalog (20–100+ tools). It does **not** generate arguments.

## Constraints (Bruno-confirmed)

| Constraint | Detail |
|---|---|
| **Compute** | Must work on CPU (portability) and GPU (speed) |
| **Scale** | 20+ tools — designed for large catalogs, not trivial cases |
| **Scope** | Tool selection only — SLM picks tool IDs, never generates arguments |
| **Latency** | As low as possible for local execution |

## Architecture Principles (from previous evaluation)

1. **MCPToolRouter stays pure** — no LLM dependency added to the existing package
2. **Composition via MEAI interfaces** — `IEmbeddingGenerator<string, Embedding<float>>` + `IChatClient`
3. **SLM layer is optional** — users compose explicitly; embedding-only is the default fast path
4. **Graceful degradation** — if SLM fails or times out, fall back to embedding-only results
5. **JSON schema enforcement** — tiny models need structured output constraints (14% strict JSON at 0.5B)

## External Libraries

| Library | Interface | Role |
|---|---|---|
| `ElBruno.LocalEmbeddings` | `IEmbeddingGenerator<string, Embedding<float>>` | Embed tool descriptions + user queries |
| `ElBruno.ModelContextProtocol.MCPToolRouter` | — | Semantic search over MCP tool catalog |
| `ElBruno.LocalLLMs` | `IChatClient` | Local SLM inference for tool re-ranking/selection |

---

## Models to Benchmark

| # | Model | Params | HuggingFace ID | ONNX Status | Est. Size (INT4) | Notes |
|---|---|---|---|---|---|---|
| 1 | **Qwen2.5-0.5B-Instruct** | 0.5B | `Qwen/Qwen2.5-0.5B-Instruct` | ✅ Already converted (`elbruno/Qwen2.5-0.5B-Instruct-onnx`) | ~825 MB | Native tool calling. **TOP PICK.** |
| 2 | **SmolLM2-360M-Instruct** | 360M | `HuggingFaceTB/SmolLM2-360M-Instruct` | 🔄 Needs conversion | ~600 MB | Edge-optimized. **RUNNER-UP.** |
| 3 | **SmolLM2-135M-Instruct** | 135M | `HuggingFaceTB/SmolLM2-135M-Instruct` | 🔄 Needs conversion | ~450 MB | Smallest viable. **BUDGET.** |
| 4 | **Qwen3-0.6B-Instruct** | 0.6B | `Qwen/Qwen3-0.6B` | 🔄 Needs conversion | ~900 MB | Newest, thinking mode. **WILD CARD.** |
| 5 | **Gemma-3-270M** | 270M | `google/gemma-3-270m-it` | 🔍 Investigate ONNX availability | ~500 MB | Google nano, native function calling. **INVESTIGATE.** |
| 6 | **TinyAgent-1.1B** | 1.1B | `squeeze-ai-lab/TinyAgent-1.1B` | 🔍 Investigate ONNX availability | ~1.2 GB | Berkeley, specialized tool calling. **INVESTIGATE.** |

---

## Phase 0: Model Conversion & Availability

**Owner:** Dozer (ML Engineer)  
**Depends on:** Nothing — can start immediately  
**Outputs:** ONNX INT4 model directories for each model, uploaded to HuggingFace under `elbruno/` namespace

### Task 0.1 — Verify Qwen2.5-0.5B-Instruct ONNX

**Who:** Dozer  
**What:** Confirm the existing `elbruno/Qwen2.5-0.5B-Instruct-onnx` repo works with `Microsoft.ML.OnnxRuntimeGenAI 0.12.2`. Load it with `LocalChatClient`, send a simple tool-selection prompt, verify output. This is our reference model — everything else is compared against it.  
**Where:** Run locally against `src/ElBruno.LocalLLMs/` using the existing `KnownModels.Qwen25_05BInstruct` definition.  
**Why:** The model is already in `KnownModels` but hasn't been validated for tool-selection prompts specifically. Must confirm before building the benchmark suite on top of it.

### Task 0.2 — Convert SmolLM2-360M-Instruct to ONNX INT4

**Who:** Dozer  
**What:** Convert `HuggingFaceTB/SmolLM2-360M-Instruct` to ONNX INT4 using the existing conversion pipeline:
```bash
python scripts/convert_to_onnx.py \
    --model-id HuggingFaceTB/SmolLM2-360M-Instruct \
    --output-dir ./models/smollm2-360m-instruct
```
Upload the result to `elbruno/SmolLM2-360M-Instruct-onnx` on HuggingFace. Record actual file size.  
**Where:** `scripts/convert_to_onnx.py` for conversion. May need to update the script if SmolLM2-360M requires special handling (different architecture than SmolLM2-1.7B which is already converted).  
**Why:** Runner-up model — smallest instruct model from HuggingFace with proven edge performance. Needed for benchmark comparison.

### Task 0.3 — Convert SmolLM2-135M-Instruct to ONNX INT4

**Who:** Dozer  
**What:** Same process as Task 0.2 but for `HuggingFaceTB/SmolLM2-135M-Instruct`. Upload to `elbruno/SmolLM2-135M-Instruct-onnx`.  
**Where:** `scripts/convert_to_onnx.py`  
**Why:** Budget model. If 135M can do tool selection with acceptable accuracy, it enables deployment on extremely constrained devices.

### Task 0.4 — Convert Qwen3-0.6B-Instruct to ONNX INT4

**Who:** Dozer  
**What:** Convert `Qwen/Qwen3-0.6B` to ONNX INT4. This model is newer than Qwen2.5; verify that `onnxruntime-genai` 0.12.2 supports its architecture. If Qwen3 uses a different tokenizer or attention pattern, document any required changes. Upload to `elbruno/Qwen3-0.6B-Instruct-onnx`.  
**Where:** `scripts/convert_to_onnx.py` — may need architecture-specific flags.  
**Why:** Wild card model — Qwen3 introduces "thinking mode" which could improve reasoning for ambiguous tool selection. Worth benchmarking even if conversion is harder.

### Task 0.5 — Investigate Gemma-3-270M ONNX Availability

**Who:** Dozer  
**What:** Research whether `google/gemma-3-270m-it` has an official ONNX export or community conversion. Check:
1. Does Google publish ONNX weights on HuggingFace?
2. Is the architecture supported by `optimum[onnxruntime]`?
3. Does `onnxruntime-genai` 0.12.2 support Gemma-3 architecture? (Gemma-2 is supported — check delta)
4. Does native function calling survive ONNX conversion?

Document findings in `.squad/decisions/inbox/dozer-gemma3-270m-investigation.md`. If viable, convert and upload to `elbruno/Gemma-3-270M-IT-onnx`.  
**Where:** Research + `scripts/convert_to_onnx.py` if viable.  
**Why:** Google's nano model with native function calling could be ideal for tool selection — but only if ONNX conversion preserves that capability.

### Task 0.6 — Investigate TinyAgent-1.1B ONNX Availability

**Who:** Dozer  
**What:** Research Berkeley's `squeeze-ai-lab/TinyAgent-1.1B`:
1. Is the model architecture standard (LLaMA-based? Custom?)?
2. Can it be converted with `optimum`?
3. Does `onnxruntime-genai` support its architecture?
4. How does it represent tool-calling output? (Custom format? JSON? Function call tokens?)

Document findings in `.squad/decisions/inbox/dozer-tinyagent-investigation.md`. If viable, convert to ONNX INT4.  
**Where:** Research + conversion pipeline.  
**Why:** A model specifically trained for tool calling could outperform general instruct models at this task, even if it needs special output parsing.

### Task 0.7 — Add New ModelDefinitions to KnownModels

**Who:** Trinity (Core Dev)  
**Depends on:** Tasks 0.2–0.6 (as each model becomes available)  
**What:** For each successfully converted model, add a `ModelDefinition` to `KnownModels.cs`:
```csharp
public static readonly ModelDefinition SmolLM2_360MInstruct = new()
{
    Id = "smollm2-360m-instruct",
    DisplayName = "SmolLM2-360M-Instruct",
    HuggingFaceRepoId = "elbruno/SmolLM2-360M-Instruct-onnx",
    RequiredFiles = ["*"],
    ModelType = OnnxModelType.GenAI,
    ChatTemplate = ChatTemplateFormat.ChatML,  // verify per model
    Tier = ModelTier.Tiny,
    HasNativeOnnx = true
};
```
Also add corresponding unit tests in `tests/ElBruno.LocalLLMs.Tests/`.  
**Where:** `src/ElBruno.LocalLLMs/Models/KnownModels.cs`, `tests/ElBruno.LocalLLMs.Tests/`  
**Why:** Models must be registered before benchmarks can reference them by `KnownModels.*` instead of hardcoded paths.

---

## Phase 1: Benchmark Framework

**Owner:** Tank (Tester) — framework design; Morpheus (Lead) — scenario definition  
**Depends on:** Phase 0 (at least Task 0.1 for Qwen2.5-0.5B; other models can be added incrementally)  
**Outputs:** Reproducible benchmark suite with published results

### Task 1.1 — Create Benchmark Project

**Who:** Tank  
**What:** Create a new benchmark project at `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/`. This is separate from the existing `ElBruno.LocalLLMs.Benchmarks` (which benchmarks chat templates and model definitions). The new project focuses specifically on tool routing accuracy and latency.

Project structure:
```
benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/
├── ElBruno.LocalLLMs.ToolRouting.Benchmarks.csproj
├── Program.cs
├── Data/
│   ├── ToolCatalogs.cs          // 20, 50, 100 tool definitions
│   └── TestPrompts.cs           // categorized test prompts
├── Scenarios/
│   ├── EmbeddingOnlyBenchmark.cs
│   ├── EmbeddingPlusSlmBenchmark.cs
│   └── SlmOnlyBenchmark.cs
├── Metrics/
│   ├── AccuracyTracker.cs       // correct tool selection tracking
│   └── MemoryTracker.cs         // peak memory measurement
└── Results/
    └── .gitkeep                 // benchmark results go here
```

The `.csproj` should reference:
- `BenchmarkDotNet 0.14.*`
- `ElBruno.LocalLLMs` (project reference)
- `ElBruno.LocalEmbeddings` (NuGet — latest)
- `ElBruno.ModelContextProtocol.MCPToolRouter` (NuGet — latest)

Target `net8.0` only (benchmarks don't need multi-target).  
**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/`  
**Why:** Isolating tool-routing benchmarks from existing benchmarks keeps concerns separate and allows independent iteration.

### Task 1.2 — Define Tool Catalogs

**Who:** Tank  
**What:** Create synthetic but realistic MCP tool catalogs in `Data/ToolCatalogs.cs`. Three sizes:

**20 tools** — represents a typical developer setup:
- File operations (read, write, list, search, move)
- Git operations (status, commit, diff, log, branch)
- Web operations (fetch, search, screenshot)
- Code operations (lint, format, compile, test)
- System operations (env, process-list, disk-usage)

**50 tools** — represents a power user / multi-server setup:
- All 20 above, plus:
- Database operations (query, insert, update, schema, migrate)
- Docker operations (build, run, stop, logs, ps)
- Cloud operations (deploy, status, logs, scale)
- Communication (email-send, slack-post, calendar-create)
- AI operations (summarize, translate, classify, embed)
- Monitoring (metrics, alerts, healthcheck)

**100 tools** — stress test:
- All 50 above, duplicated across namespaces (e.g., `aws.deploy` vs `azure.deploy` vs `gcp.deploy`)
- Intentionally confusable tools (e.g., `file.search` vs `code.search` vs `web.search`)
- Tools with overlapping descriptions

Each tool definition must include:
- `name` (string, unique)
- `description` (1-2 sentences, realistic)
- `inputSchema` (JSON schema — for catalog realism, even though SLM won't generate args)

**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Data/ToolCatalogs.cs`  
**Why:** Realistic catalogs are critical — benchmarks on toy data produce misleading results. The 100-tool catalog with confusable tools specifically tests the value of SLM re-ranking over embedding-only search.

### Task 1.3 — Define Test Prompts

**Who:** Tank, reviewed by Morpheus  
**What:** Create categorized test prompts in `Data/TestPrompts.cs`. Each prompt has a known expected tool (ground truth) for accuracy scoring.

**Categories:**

1. **Clear single-tool** (15 prompts) — unambiguous match  
   Example: `"Show me the git log for the last 5 commits"` → `git.log`

2. **Ambiguous single-tool** (15 prompts) — multiple tools could match  
   Example: `"Find that error in the codebase"` → `code.search` (not `file.search` or `web.search`)

3. **Multi-tool** (10 prompts) — query requires 2-3 tools  
   Example: `"Run the tests and if they pass, commit the changes"` → `[code.test, git.commit]`

4. **Adversarial** (10 prompts) — designed to confuse embeddings  
   Example: `"Deploy the docker container to the cloud"` → `[docker.build, cloud.deploy]` (not `docker.deploy` which doesn't exist)

5. **Out-of-scope** (5 prompts) — no matching tool exists  
   Example: `"What's the weather in Seattle?"` → `[]` (empty — no tool should be selected)

Each prompt is a record:
```csharp
public record TestPrompt(
    string Query,
    string[] ExpectedTools,
    PromptCategory Category,
    string Rationale  // why this ground truth is correct
);
```

**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Data/TestPrompts.cs`  
**Why:** Ground-truth prompts enable automated accuracy scoring. The categories ensure we measure performance on the cases that matter (ambiguous queries are where SLM adds value over embeddings).

### Task 1.4 — Implement Embedding-Only Benchmark

**Who:** Tank  
**What:** Create `Scenarios/EmbeddingOnlyBenchmark.cs` — the baseline. Uses `MCPToolRouter` directly (no SLM). For each test prompt:
1. Call `MCPToolRouter.RouteAsync(prompt)` to get top-K tools (K = 1, 3, 5)
2. Measure latency (P50, P95, P99)
3. Score accuracy: does the top-K result contain the expected tool(s)?
4. Record peak memory via `GC.GetTotalMemory()` and `Process.WorkingSet64`

Use BenchmarkDotNet for latency. Use a custom `AccuracyTracker` for correctness (BenchmarkDotNet measures throughput, not accuracy).

Parameters to vary:
- Tool catalog size: 20, 50, 100
- Top-K: 1, 3, 5
- Prompt category: all five

**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Scenarios/EmbeddingOnlyBenchmark.cs`  
**Why:** This is the control group. Every SLM result is compared against this baseline. If embeddings alone achieve >95% on clear prompts, the SLM value proposition is specifically for ambiguous/multi-tool cases.

### Task 1.5 — Implement Embedding+SLM Benchmark

**Who:** Tank  
**What:** Create `Scenarios/EmbeddingPlusSlmBenchmark.cs` — the two-stage RAG pipeline. For each test prompt:
1. Call `MCPToolRouter.RouteAsync(prompt, topK: 10)` to get candidate tools
2. Format a tool-selection prompt with the candidate tools and user query
3. Call `IChatClient.GetResponseAsync()` with the formatted prompt
4. Parse the SLM response to extract selected tool ID(s)
5. Measure end-to-end latency (embedding + SLM combined)
6. Score accuracy against ground truth

Parameters to vary:
- All embedding-only parameters, plus:
- Model: each of the 6 benchmark models
- Execution provider: CPU vs GPU (via `OnnxRuntimeGenAI` session options)
- Prompt template: model-specific (see Task 2.3)

The prompt template for the SLM stage follows this pattern:
```
You are a tool router. Given a user query and a list of available tools,
select the tool(s) that best match the query.

Available tools:
{{#each candidateTools}}
- {{name}}: {{description}}
{{/each}}

User query: {{userQuery}}

Respond with ONLY a JSON array of tool names. Example: ["tool.name"]
Do not explain. Do not add arguments.
```

**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Scenarios/EmbeddingPlusSlmBenchmark.cs`  
**Why:** This measures the core value proposition — does adding an SLM step improve accuracy enough to justify the latency cost? The per-model comparison identifies which tiny SLM is best for this specific task.

### Task 1.6 — Implement SLM-Only Benchmark (Control)

**Who:** Tank  
**What:** Create `Scenarios/SlmOnlyBenchmark.cs` — gives the SLM the full tool catalog without embedding pre-filtering. This tests whether the SLM can handle large tool catalogs directly (likely poor — context window limits).

For each prompt:
1. Format prompt with ALL tools from the catalog (no embedding pre-filter)
2. Call `IChatClient.GetResponseAsync()`
3. Parse and score

This will likely fail for 50+ tools (exceeds context window of 0.5B models). Document the failure point.  
**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Scenarios/SlmOnlyBenchmark.cs`  
**Why:** Proves that embedding pre-filtering is mandatory for large catalogs. Quantifies the context window limitation of tiny models.

### Task 1.7 — Benchmark Runner and Results Format

**Who:** Tank  
**What:** Create `Program.cs` that orchestrates all benchmarks and produces a unified results file. The runner should:
1. Accept CLI args: `--models` (comma-separated), `--catalogs` (20,50,100), `--gpu` (flag)
2. Run BenchmarkDotNet for latency measurements
3. Run custom accuracy suite separately (accuracy is not a BenchmarkDotNet metric)
4. Output results as both:
   - Markdown table (human-readable) → `Results/results-{timestamp}.md`
   - CSV (machine-parseable) → `Results/results-{timestamp}.csv`

Results columns:
```
Model, CatalogSize, PromptCategory, Pipeline (emb-only|emb+slm|slm-only),
TopK, Accuracy%, LatencyP50ms, LatencyP95ms, PeakMemoryMB, ExecutionProvider (CPU|GPU)
```

**Where:** `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Program.cs`  
**Why:** Reproducible results with standard format enable comparison across runs, machines, and future models.

---

## Phase 2: Sample / Integration Project

**Owner:** Trinity (Core Dev) — implementation; Morpheus (Lead) — API review  
**Depends on:** Phase 0 Task 0.1 (at least one working model); Phase 1 not required  
**Outputs:** Working sample demonstrating the composition pattern

### Task 2.1 — Create ToolRoutingWithSlm Sample

**Who:** Trinity  
**What:** Create a new sample project at `samples/ToolRoutingWithSlm/` that demonstrates the full composition pattern. This is the reference implementation that users will copy.

```
samples/ToolRoutingWithSlm/
├── ToolRoutingWithSlm.csproj
├── Program.cs
├── ToolSelectionService.cs
├── ToolSelectionOptions.cs
├── SampleTools.cs
└── PromptTemplates.cs
```

The `.csproj` should reference:
- `ElBruno.LocalLLMs` (project reference)
- `ElBruno.LocalEmbeddings` (NuGet)
- `ElBruno.ModelContextProtocol.MCPToolRouter` (NuGet)
- Target `net8.0`

**Program.cs** should demonstrate three modes:
```csharp
// Mode 1: Embedding-only (fast path — ~40ms)
var router = await McpToolRouter.CreateAsync(tools, embeddingGenerator);
var results = await router.RouteAsync("run the tests");
Console.WriteLine($"Fast path: {results[0].Name}"); // code.test

// Mode 2: Embedding + SLM (reasoning path — ~1.2s)
var service = new ToolSelectionService(router, chatClient, options);
var selected = await service.SelectToolsAsync("run the tests and commit");
Console.WriteLine($"Reasoning path: {string.Join(", ", selected)}"); // code.test, git.commit

// Mode 3: DI registration
services.AddLocalEmbeddings();
services.AddLocalLLMs(opts => opts.Model = KnownModels.Qwen25_05BInstruct);
services.AddSingleton<ToolSelectionService>();
```

**Where:** `samples/ToolRoutingWithSlm/`  
**Why:** Users need a copy-pasteable reference. This sample proves the composition pattern works end-to-end without requiring any changes to existing libraries.

### Task 2.2 — Implement ToolSelectionService

**Who:** Trinity, reviewed by Morpheus  
**What:** Implement `ToolSelectionService` — the composition layer that wires MCPToolRouter + LocalChatClient together. This is sample code, not a published library (users copy/adapt it).

```csharp
public class ToolSelectionService
{
    private readonly McpToolRouter _router;
    private readonly IChatClient _chatClient;
    private readonly ToolSelectionOptions _options;

    public ToolSelectionService(
        McpToolRouter router,
        IChatClient chatClient,
        ToolSelectionOptions? options = null)
    {
        _router = router;
        _chatClient = chatClient;
        _options = options ?? new();
    }

    /// <summary>
    /// Two-stage tool selection: embedding search → SLM re-ranking.
    /// Falls back to embedding-only results if SLM fails.
    /// </summary>
    public async Task<IReadOnlyList<string>> SelectToolsAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        // Stage 1: Embedding search (fast)
        var candidates = await _router.RouteAsync(
            userQuery,
            topK: _options.EmbeddingTopK,
            cancellationToken: cancellationToken);

        if (!_options.UseSlmReasoning)
            return candidates.Select(c => c.Name).ToList();

        // Stage 2: SLM reasoning (slower, more accurate)
        try
        {
            var prompt = FormatToolSelectionPrompt(userQuery, candidates);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.SlmTimeout);

            var response = await _chatClient.GetResponseAsync(
                prompt, cancellationToken: cts.Token);

            var selected = ParseToolSelection(response.Text);
            return selected.Count > 0 ? selected : FallbackToEmbeddings(candidates);
        }
        catch (OperationCanceledException)
        {
            // SLM timed out — fall back to embedding results
            return FallbackToEmbeddings(candidates);
        }
        catch (Exception)
        {
            // SLM failed — fall back to embedding results
            return FallbackToEmbeddings(candidates);
        }
    }
}
```

Key design points:
- **Timeout:** `ToolSelectionOptions.SlmTimeout` defaults to `TimeSpan.FromSeconds(5)` — if SLM is slower, fall back
- **Parsing:** `ParseToolSelection()` uses multiple strategies (see Task 3.1)
- **Fallback:** Always returns embedding results if SLM fails — never throws, never returns empty unless no tools match at all

**Where:** `samples/ToolRoutingWithSlm/ToolSelectionService.cs`  
**Why:** This is the architectural pattern we recommend. It demonstrates graceful degradation, timeout handling, and clean MEAI composition — all in ~100 lines of code.

### Task 2.3 — Create Model-Specific Prompt Templates

**Who:** Trinity  
**What:** Create `PromptTemplates.cs` with optimized prompt templates for each model family. Tiny models need shorter, more constrained prompts.

Templates to create:

**Qwen-family (Qwen2.5-0.5B, Qwen3-0.6B):**
```
<|im_start|>system
You select tools. Output ONLY a JSON array of tool names.
<|im_end|>
<|im_start|>user
Tools:
{{toolList}}

Query: {{query}}
<|im_end|>
<|im_start|>assistant
```

**SmolLM2-family (360M, 135M):**
```
Select tools for the query. Reply with ONLY tool names in a JSON array.

Tools: {{toolList}}

Query: {{query}}

Selected: [
```
(Note: Pre-fill the opening bracket to nudge the model toward JSON output)

**Gemma-family:**
```
<start_of_turn>user
Select the best tool(s) for this query.
Tools: {{toolList}}
Query: {{query}}
Reply with ONLY a JSON array of tool names.<end_of_turn>
<start_of_turn>model
```

**Generic fallback** (for unknown models):
```
Given these tools and a query, select the matching tool(s).
Reply with ONLY a JSON array of tool names like ["tool.name"].

Tools:
{{toolList}}

Query: {{query}}

Selected tools:
```

Each template should also include:
- Maximum token budget for the tool list portion (based on model context window)
- Tool list format: `"- name: description"` (one line per tool, truncated descriptions)

**Where:** `samples/ToolRoutingWithSlm/PromptTemplates.cs`  
**Why:** Prompt engineering is the #1 lever for tiny model quality. A prompt tuned for 0.5B parameters is very different from one for 7B. The pre-fill technique (starting the response with `[`) dramatically improves JSON compliance in tiny models.

### Task 2.4 — Add Sample to Solution

**Who:** Trinity  
**What:** Add the new sample project to `ElBruno.LocalLLMs.slnx`:
```xml
<Project Path="samples/ToolRoutingWithSlm/ToolRoutingWithSlm.csproj" />
```
Also add the benchmark project from Phase 1:
```xml
<Project Path="benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks.csproj" />
```
Verify `dotnet build` succeeds for the full solution.  
**Where:** `ElBruno.LocalLLMs.slnx`  
**Why:** All projects must be in the solution file for CI/CD and IDE discovery.

---

## Phase 3: Optimization

**Owner:** Trinity (Core Dev) + Dozer (ML) — implementation; Morpheus (Lead) — design review  
**Depends on:** Phase 1 results (need benchmark data to know what to optimize)  
**Outputs:** Optimized parsing, caching, and loading strategies

### Task 3.1 — JSON Output Parsing Strategies

**Who:** Trinity  
**What:** Implement robust JSON parsing in `ToolSelectionService.ParseToolSelection()` that handles the messy output of tiny models. The parser should try strategies in order:

1. **Strict JSON** — `JsonSerializer.Deserialize<string[]>(response)`
2. **Extract JSON array** — regex `\[.*?\]` to find a JSON array anywhere in the response
3. **Line-by-line** — split response by newlines, match each line against known tool names
4. **Fuzzy match** — for each token in the response, compute Levenshtein distance against known tool names (threshold: 2 edits)
5. **Give up** — return empty list (trigger fallback to embeddings)

The parser should log which strategy succeeded (for benchmark telemetry).

Additionally, implement **constrained decoding** if the model supports it:
- For OnnxRuntimeGenAI, investigate `LogitsProcessor` to constrain output tokens to valid JSON + tool name tokens only
- This is an advanced optimization — implement as optional, gated behind `ToolSelectionOptions.UseConstrainedDecoding`

**Where:** `samples/ToolRoutingWithSlm/ToolSelectionService.cs` (parsing methods)  
**Why:** At 0.5B parameters, only 14% of outputs are strict JSON. The fallback chain recovers valid tool selections from the other 86%. This is the difference between "works in demos" and "works in production."

### Task 3.2 — Caching Strategy

**Who:** Trinity  
**What:** Implement a cache layer for repeated/similar queries in `ToolSelectionService`:

1. **Exact-match cache** — `ConcurrentDictionary<string, IReadOnlyList<string>>` keyed by normalized query (lowercase, trimmed). TTL-based eviction.
2. **Embedding-similarity cache** — for queries with cosine similarity > 0.95 to a cached query, return the cached result. Uses the same `IEmbeddingGenerator` already available.
3. **Configuration:**
   ```csharp
   public class ToolSelectionOptions
   {
       public bool EnableCache { get; set; } = true;
       public int MaxCacheEntries { get; set; } = 1000;
       public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(30);
       public float SimilarityCacheThreshold { get; set; } = 0.95f;
   }
   ```

The cache operates at the `ToolSelectionService` level (not inside MCPToolRouter or LocalChatClient — those are external libraries).  
**Where:** `samples/ToolRoutingWithSlm/ToolSelectionService.cs`  
**Why:** In interactive scenarios, users often rephrase similar queries. The embedding-similarity cache avoids re-running the SLM for "run tests" vs "execute the test suite" (which map to the same tool). The cache alone can reduce perceived latency from 1.2s to <5ms for repeated patterns.

### Task 3.3 — Model Warm-Up and Lazy Loading

**Who:** Trinity  
**What:** Add model lifecycle management to the sample:

1. **Lazy loading** — `LocalChatClient` is not created until the first SLM query. This avoids the ~2-5s model load time on startup if the user only uses the fast path.
   ```csharp
   private readonly Lazy<Task<IChatClient>> _chatClient;
   ```

2. **Warm-up** — optional explicit warm-up that sends a dummy prompt to force model loading + JIT compilation of ONNX Runtime kernels:
   ```csharp
   public async Task WarmUpAsync(CancellationToken ct = default)
   {
       var client = await _chatClient.Value;
       await client.GetResponseAsync("ping", cancellationToken: ct);
   }
   ```

3. **Disposal** — `ToolSelectionService` implements `IAsyncDisposable` and cleans up the `IChatClient` if it was created.

**Where:** `samples/ToolRoutingWithSlm/ToolSelectionService.cs`  
**Why:** First-query latency is 5-10x worse than steady-state due to model loading and kernel compilation. Lazy loading defers the cost; explicit warm-up lets users control when it happens (e.g., during app startup, not during first user interaction).

### Task 3.4 — Cross-Encoder Re-Ranking (Alternative to SLM)

**Who:** Dozer (model research) + Trinity (implementation)  
**What:** Investigate and prototype cross-encoder re-ranking as a lighter alternative to full SLM reasoning:

1. **Research:** Identify a small cross-encoder model suitable for ONNX (e.g., `cross-encoder/ms-marco-MiniLM-L-6-v2`, ~80MB). Check ONNX compatibility.
2. **Prototype:** Create a `CrossEncoderReranker` class that:
   - Takes (query, tool_description) pairs
   - Scores relevance using the cross-encoder
   - Re-ranks the embedding results
   - Expected latency: 100-300ms (between embedding-only and full SLM)
3. **Benchmark:** Add to the benchmark suite as a fourth pipeline option

The cross-encoder approach is fundamentally different from the SLM approach:
- **SLM**: Understands the query, reasons about which tools fit, outputs tool names
- **Cross-encoder**: Scores (query, tool) pairs independently, no reasoning, just relevance scoring

This is a potential "sweet spot" between accuracy and latency.  
**Where:** New file in the sample project: `samples/ToolRoutingWithSlm/CrossEncoderReranker.cs`; benchmark addition in `benchmarks/ElBruno.LocalLLMs.ToolRouting.Benchmarks/Scenarios/CrossEncoderBenchmark.cs`  
**Why:** If cross-encoder re-ranking achieves 90%+ of the SLM accuracy improvement at 10% of the latency cost, it becomes the recommended default over SLM for latency-sensitive applications. This was flagged in the architecture evaluation as a promising alternative.

---

## Phase 4: Documentation & Decision Tree

**Owner:** Morpheus (Lead)  
**Depends on:** Phase 1 results (need data to write accurate guidance)  
**Outputs:** User-facing documentation

### Task 4.1 — Write Tool Routing Guide

**Who:** Morpheus  
**What:** Create `docs/tool-routing-guide.md` — a user-facing guide that explains:
1. What tool routing is and when to use it
2. The three pipelines (embedding-only, embedding+SLM, embedding+cross-encoder) with tradeoffs
3. Decision tree: "Which pipeline should I use?"
   - <20 tools + clear prompts → embedding-only
   - 20-50 tools + ambiguous prompts → cross-encoder re-ranking
   - 50+ tools + multi-tool + complex queries → embedding+SLM
4. Step-by-step setup for each pipeline
5. Performance data from benchmarks (filled in after Phase 1 completes)
6. Prompt template reference for each model

**Where:** `docs/tool-routing-guide.md`  
**Why:** Users need to make informed decisions about which pipeline to use. The decision tree prevents over-engineering (using SLM when embeddings suffice) or under-engineering (using embeddings when SLM is needed).

### Task 4.2 — Update Architecture Doc

**Who:** Morpheus  
**What:** Add a section to `docs/architecture.md` covering the optional SLM composition pattern. Include:
- Component diagram showing the three pipelines
- Interface boundaries (which MEAI interfaces are used where)
- The "SLM is optional" principle and why it's important
- Link to the tool routing guide

**Where:** `docs/architecture.md` (append new section)  
**Why:** The architecture doc is the source of truth for library design decisions. The composition pattern must be documented there.

---

## Dependency Graph

```
Phase 0 (Model Conversion)
├── Task 0.1 (Verify Qwen2.5-0.5B) ─────────────────────┐
├── Task 0.2 (Convert SmolLM2-360M) ──┐                  │
├── Task 0.3 (Convert SmolLM2-135M) ──┤                  │
├── Task 0.4 (Convert Qwen3-0.6B) ────┤                  │
├── Task 0.5 (Investigate Gemma-3) ────┤                  │
├── Task 0.6 (Investigate TinyAgent) ──┤                  │
└── Task 0.7 (Add KnownModels) ◄──────┘                  │
                                                          │
Phase 1 (Benchmarks)                                      │
├── Task 1.1 (Create project) ◄───────────────────────────┘
├── Task 1.2 (Tool catalogs) ◄── Task 1.1
├── Task 1.3 (Test prompts) ◄── Task 1.1
├── Task 1.4 (Embedding-only bench) ◄── Tasks 1.2, 1.3
├── Task 1.5 (Embedding+SLM bench) ◄── Tasks 1.2, 1.3, 0.7
├── Task 1.6 (SLM-only bench) ◄── Tasks 1.2, 1.3, 0.7
└── Task 1.7 (Runner + results) ◄── Tasks 1.4, 1.5, 1.6

Phase 2 (Sample)                    ◄── Task 0.1 (only needs 1 working model)
├── Task 2.1 (Create sample project)
├── Task 2.2 (ToolSelectionService) ◄── Task 2.1
├── Task 2.3 (Prompt templates) ◄── Task 2.1
└── Task 2.4 (Add to solution) ◄── Tasks 2.1, 1.1

Phase 3 (Optimization)             ◄── Phase 1 results
├── Task 3.1 (JSON parsing)
├── Task 3.2 (Caching)
├── Task 3.3 (Warm-up / lazy load)
└── Task 3.4 (Cross-encoder) ◄── Dozer research

Phase 4 (Documentation)            ◄── Phase 1 results
├── Task 4.1 (Tool routing guide)
└── Task 4.2 (Architecture doc update)
```

## Parallelism Opportunities

- **Phase 0 tasks 0.1–0.6** are all independent — Dozer can run them in parallel
- **Phase 2** can start as soon as Task 0.1 completes (only needs Qwen2.5-0.5B)
- **Phase 1 tasks 1.1–1.3** can start as soon as Task 0.1 completes
- **Phase 1 tasks 1.4 and 1.5** can run in parallel once data and models are ready
- **Phase 3 tasks 3.1–3.3** are independent of each other
- **Phase 4** must wait for Phase 1 results to write accurate documentation

## Success Criteria

| Metric | Target |
|---|---|
| Embedding-only accuracy (20 tools, clear prompts) | >90% |
| Embedding+SLM accuracy (20 tools, ambiguous prompts) | >80% |
| Embedding-only latency P95 | <100ms |
| Embedding+SLM latency P95 (CPU, 0.5B model) | <3s |
| Embedding+SLM latency P95 (GPU, 0.5B model) | <500ms |
| Memory usage (0.5B model loaded) | <2 GB |
| Sample builds and runs on `net8.0` | ✅ |
| Graceful fallback when SLM fails | ✅ |
| At least 3 of 6 models benchmarked | ✅ |

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Qwen3-0.6B not supported by onnxruntime-genai 0.12.2 | Lose wild card model | Accept — Qwen2.5-0.5B is proven |
| Gemma-3-270M / TinyAgent-1.1B ONNX conversion fails | Lose two candidates | Accept — they're investigation-only |
| All 0.5B models <60% accuracy on ambiguous prompts | SLM value proposition weakened | Pivot to cross-encoder re-ranking (Task 3.4) as primary alternative |
| JSON parsing too unreliable across models | Functional failure | Task 3.1 fallback chain + constrained decoding |
| SmolLM2-135M too small for any useful tool selection | Lose budget model | Accept — still interesting as a data point |
