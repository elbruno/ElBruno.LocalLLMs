# Decision: OIDC Trusted Publishing for NuGet

**Date:** 2026-03-18
**Author:** Switch (DevOps/Packaging)
**Status:** Implemented

## Context

The `publish.yml` workflow used a long-lived `NUGET_API_KEY` secret for NuGet pushes, triggered by `push: tags: v*`. This has security concerns (key rotation, leak risk) and didn't integrate cleanly with the squad-release flow (which creates GitHub Releases, not just tags).

## Decision

Migrated to **NuGet OIDC Trusted Publishing** using `NuGet/login@v1`:

1. **Trigger:** Changed from `push: tags: v*` to `release: published` + `workflow_dispatch` with optional version input
2. **Auth:** Replaced `NUGET_API_KEY` secret with OIDC token exchange via `NuGet/login@v1` + `NUGET_USER` secret
3. **Environment:** Added `environment: release` (required for OIDC policy matching)
4. **Permissions:** `id-token: write` + `contents: read` (for OIDC token minting)
5. **Version:** 3-tier priority (release tag → manual input → csproj fallback) with validation

## Release Chain

```
Push to main → squad-release.yml (creates GitHub Release) → release event triggers publish.yml → OIDC → NuGet
```

## Consequences

- ✅ No long-lived API keys to rotate or leak
- ✅ Scoped temp tokens per workflow run
- ✅ Clean integration with squad-release flow
- ✅ Manual dispatch for ad-hoc/pre-release publishes
- 📝 Requires one-time NuGet.org Trusted Publishing policy setup
- 📝 Requires GitHub `release` environment + `NUGET_USER` secret
- 📝 See `docs/publishing.md` for full setup guide

## Files Changed

- `.github/workflows/publish.yml` — Rewritten for OIDC
- `docs/publishing.md` — New publishing guide
