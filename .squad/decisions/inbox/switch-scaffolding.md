# Decision: Package Versions Updated from Architecture Doc

**Date:** 2026-03-17
**Author:** Switch (DevOps)
**Status:** Active

## Context

The architecture doc specified specific package versions that were outdated by the time of implementation. NuGet packages had newer stable releases.

## Decision

Updated all package versions to latest stable:

| Package | Architecture Doc | Actual Used |
|---------|-----------------|-------------|
| Microsoft.Extensions.AI.Abstractions | 10.3.0 | 10.4.0 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.3 | 10.0.5 |
| Microsoft.ML.OnnxRuntimeGenAI | 0.6.* | 0.8.3 |
| ElBruno.HuggingFace.Downloader | 0.5.0 | 0.6.0 |
| xunit | 2.* | 2.9.0 |
| NSubstitute | 5.* | 5.3.0 |

## Consequences

- MEAI 10.4.0 introduced breaking API changes (method renames, type renames). All source code updated accordingly.
- OnnxRuntimeGenAI 0.8.3 changed the token retrieval API. Generator wrapper updated.
- Pinned exact versions (not wildcards) to avoid NuGet resolution warnings with TreatWarningsAsErrors.

## Impact

All projects in the solution. Any future code must use the new API names (`GetResponseAsync`, `ChatResponse`, etc.).
