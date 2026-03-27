# Decision: CI/CD targets net8.0 only on ubuntu-latest

**Author:** Switch (DevOps)
**Date:** 2026-03-27
**Status:** Applied

## Context

The CI workflow (`ci.yml`) previously used a multi-OS matrix (ubuntu + windows) and installed both `8.0.x` and `10.0.x` SDKs. The publish workflow (`publish.yml`) also installed both SDKs and referenced the old `tests/` path.

## Decision

Per `.github/copilot-instructions.md` conventions:
- CI runs on `ubuntu-latest` only (no matrix)
- Only `8.0.x` SDK installed (CI runners may not have preview SDKs)
- Restore/build use `-p:TargetFrameworks=net8.0`; test uses `--framework net8.0`
- Test path updated from `tests/` → `src/tests/` to match Trinity's restructure
- `.gitignore` cleaned: 80+ individual `cache_dir/` entries replaced with `cache_dir/` glob

## Consequences

- CI is faster (single OS) and won't break when net10.0 preview SDK isn't available on runners
- Library still multi-targets `net8.0;net10.0` in csproj — only CI is pinned to net8.0
- Model cache directories fully excluded from git tracking
