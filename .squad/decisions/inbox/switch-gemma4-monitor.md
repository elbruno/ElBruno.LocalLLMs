# Decision: Gemma 4 Blocker Monitoring Workflow

**Author:** Switch (DevOps)  
**Date:** 2026-07-25  
**Status:** Implemented  

## Context

Morpheus proposed a tiered monitoring system for the Gemma 4 blocker (Tier 1: automated workflow). The three blockers — PLE, variable head dimensions, KV cache sharing — are runtime-level in `onnxruntime-genai` v0.12.2. Manual checking is unreliable.

## Decision

Created `.github/workflows/monitor-gemma4-blocker.yml` — a daily GitHub Actions workflow that:

1. Checks NuGet for new `Microsoft.ML.OnnxRuntimeGenAI` versions
2. Checks upstream issue `microsoft/onnxruntime-genai#2062` status
3. Combines a confidence score and auto-creates a GitHub issue when score ≥ 50

## Rationale

- **Shell + github-script** keeps it simple (no custom actions, no external dependencies)
- **Confidence scoring** prevents noise — a new version alone (score 20) doesn't trigger an issue; it needs keyword evidence or issue closure
- **Dedup via label check** prevents duplicate issues on consecutive runs
- **Env vars for config** so the blocked version can be bumped without editing logic
- **Minimal permissions** (`contents: read`, `issues: write`) follows repo convention

## Implications

- When the blocker is resolved, update `KNOWN_BLOCKED_VERSION` env var and close the generated issue
- Labels `gemma4` and `investigation` are auto-created if missing
- Workflow uses no secrets (public NuGet API + public GitHub API via GITHUB_TOKEN)
