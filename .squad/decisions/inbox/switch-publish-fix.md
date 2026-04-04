# Decision: Publish workflow targets specific projects, not the full solution

**Author:** Switch (DevOps)
**Date:** 2026-07-25
**PR:** #14
**Status:** Proposed

## Context

The `publish.yml` workflow previously restored and built the entire `.slnx` solution with `-p:TargetFrameworks=net8.0`. This broke when `src/samples/ZeroCloudRag/` was added — it targets only `net10.0` and depends on `ElBruno.LocalEmbeddings 1.0.1` (net10.0-only package), causing `NU1202` restore errors.

Additionally, only .NET 8.0 SDK was installed and the pack step forced `-p:TargetFrameworks=net8.0`, meaning NuGet packages only shipped a single TFM instead of both `net8.0` and `net10.0`.

## Decision

1. **Publish workflow restores/builds specific projects, not the solution.** The workflow targets only the two library projects and their test projects. Samples, benchmarks, and other non-packable projects are excluded.

2. **Both .NET 8.0 and 10.0 SDKs are installed.** This enables multi-target builds for both TFMs.

3. **Pack step does not force a single TFM.** The `-p:TargetFrameworks=net8.0` override is removed so the package includes all targets defined in each `.csproj`.

4. **Both NuGet packages are built and published.** `ElBruno.LocalLLMs` and `ElBruno.LocalLLMs.Rag` are both packed and pushed in the same workflow run.

## Consequences

- Adding new samples or benchmarks to the solution will never break the publish workflow.
- Adding a new packable library requires updating `publish.yml` to include it in restore/build/pack steps.
- NuGet packages now correctly ship both `net8.0` and `net10.0` TFMs.
