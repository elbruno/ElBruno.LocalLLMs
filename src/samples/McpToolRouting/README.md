# McpToolRouting Sample

Demonstrates the **full pipeline** for intelligent MCP tool selection using a local LLM:

```
User prompt → LLM distillation → embedding → MCPToolRouter filtering → results
```

## What It Shows

| Scenario | Description |
|----------|-------------|
| **1. Complex Prompt + Distillation** | A verbose, multi-part user message is distilled to a single intent by Qwen2.5-0.5B before routing |
| **2. Simple Prompt — Direct Routing** | A clear, single-intent prompt skips distillation and routes directly |
| **3. Token Savings Comparison** | Compares sending all 40 tools vs. top-3 routed tools to the LLM context |
| **4. Multi-Tool Query** | A prompt spanning multiple domains returns several relevant tools |

## Architecture

- **Qwen2.5-0.5B-Instruct** — smallest local LLM (~1 GB) used for prompt distillation via `IChatClient`
- **MCPToolRouter** (`ToolIndex.CreateAsync` + `SearchAsync`) — semantic tool search using local embeddings
- **40 MCP tool definitions** — realistic tools across weather, email, calendar, files, code, database, and more
- **No cloud dependency** — everything runs locally on CPU

## Prerequisites

- .NET 8.0 or later
- ~1 GB disk space for Qwen2.5-0.5B model (auto-downloaded on first run)
- ~100 MB for embedding model (auto-downloaded by MCPToolRouter)

## Run

```bash
dotnet run --project src/samples/McpToolRouting/
```

First run will download the LLM and embedding models from HuggingFace. Subsequent runs use cached models.

## Key APIs

```csharp
// Distill a verbose prompt to a clean intent
var intent = await PromptDistiller.DistillIntentAsync(chatClient, userPrompt);

// Build the tool index (one-time cost)
await using var index = await ToolIndex.CreateAsync(tools, new ToolIndexOptions { QueryCacheSize = 10 });

// Search for relevant tools
var results = await index.SearchAsync(intent, topK: 3);
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `ElBruno.LocalLLMs` | Local LLM inference via IChatClient |
| `Microsoft.ML.OnnxRuntimeGenAI` | ONNX Runtime for model execution |
| `ElBruno.ModelContextProtocol.MCPToolRouter` | Semantic tool routing with local embeddings |
