# Decision: Project structure conventions from copilot-instructions.md

**Date:** 2026-03-27
**Author:** Trinity (Core Developer)
**Status:** Active

## Context
Bruno's `.github/copilot-instructions.md` defines conventions for project structure (tests/samples under `src/`), TFM (net8.0), Directory.Build.props contents, global.json, icon naming, and CI build properties. The existing layout had tests/samples at repo root and targeted net10.0.

## Decision
Applied all conventions from copilot-instructions.md:
- `tests/` and `samples/` moved under `src/`
- All non-library projects retargeted to net8.0
- Directory.Build.props centralized author, license, code analysis, repo info
- global.json pins SDK 8.0.0 with latestMajor rollForward
- NuGet icon renamed to `nuget_logo.png`
- Library csproj gained symbol package and CI build properties

## Consequences
- All ProjectReference paths updated for new layout
- Benchmark project path unchanged (still correct)
- 359/359 tests pass; full solution builds with 0 warnings
