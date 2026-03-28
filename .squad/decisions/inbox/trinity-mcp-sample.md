# Decision: McpToolRouting Sample Patterns

**Date:** 2026-03-27
**Author:** Trinity (Core Dev)
**Status:** Proposed

## Context

Built the McpToolRouting sample that combines local LLM inference (ElBruno.LocalLLMs) with MCPToolRouter (ElBruno.ModelContextProtocol) for intelligent tool selection.

## Decisions Made

### 1. Prompt Distillation is Optional per Query Complexity

Simple single-intent prompts ("Send an email to Alice") route directly to tools without distillation. Complex multi-part prompts benefit from LLM distillation first. The sample shows both paths — the application should decide based on prompt length or complexity heuristics.

### 2. MCPToolRouter NuGet Version Pinning

Used `Version="*"` (latest) for `ElBruno.ModelContextProtocol.MCPToolRouter` since the package is actively developed and the API surface used (`ToolIndex.CreateAsync`, `SearchAsync`) is stable. If a breaking change occurs, pin to a specific version.

### 3. Token Estimation Heuristic

Used `ceil(length / 4.0)` as a simple English-text token estimator. This is good enough for demonstrating savings ratios. Production code should use a proper tokenizer.

## Consequences

- The sample establishes the canonical pattern for combining local LLMs with MCPToolRouter in this repo
- Future samples can reference this for the distillation + routing pipeline
- The 40-tool `ToolDefinitions.cs` can be reused by other samples needing realistic MCP tool sets
