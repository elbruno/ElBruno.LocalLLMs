# Decision: Rewrite Squad Workflows for C# Project

**Date:** 2026-03-18  
**Author:** Switch (DevOps/Packaging)  
**Status:** Implemented

## Context

Eight GitHub Actions workflows were scaffolded from a Node.js Squad template but applied to `ElBruno.LocalLLMs`, which is a C# .NET 8/10 project. All workflows were failing because they referenced:
- `actions/setup-node@v4`
- `node --test test/*.test.js`
- `node -e "console.log(require('./package.json').version)"`
- Non-existent `docs/build.js` script

The project structure is:
- Solution: `ElBruno.LocalLLMs.slnx`
- Main project: `src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj`
- Tests: `tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj`
- Version: `<Version>0.1.0</Version>` in `.csproj`
- CHANGELOG format: `## [0.1.0] - 2026-03-18`

The working reference was `.github/workflows/ci.yml` which correctly used `actions/setup-dotnet@v4`.

## Decision

Rewrote all 8 workflows to be C#-native:

### 1. **squad-release.yml** (CRITICAL — triggers on every push to main)
- **Before:** Node.js setup, `node --test`, `node -e` for version
- **After:** .NET setup (8.0.x + 10.0.x), `dotnet restore/build/test`, `grep -oP` to extract version from `.csproj`
- **Logic kept:** Tag existence check, GitHub release creation

### 2. **squad-main-guard.yml** (Worktree-local strategy fix)
- **Before:** Blocked `.ai-team/` and `.squad/` from main
- **After:** **Allowed `.ai-team/` and `.squad/` on main** (this project uses worktree-local strategy)
- **Still blocked:** `.ai-team-templates/`, `team-docs/`, `docs/proposals/`
- **Rationale:** Design decision — `.squad/` committed to main for this project

### 3. **publish.yml** (NuGet resilience)
- **Before:** Always tried to push to NuGet (failed if `NUGET_API_KEY` not set)
- **After:** Check if `NUGET_API_KEY` secret is set. If not, skip push but still upload artifact
- **Rationale:** Allows workflow to succeed even without NuGet credentials configured

### 4. **squad-ci.yml** (CI for PRs/dev)
- **Before:** Node.js tests
- **After:** .NET restore/build/test against solution and test project

### 5. **squad-preview.yml** (Preview branch validation)
- **Before:** Node.js version extraction, node tests
- **After:** .NET tests, version extraction from `.csproj`, CHANGELOG validation
- **Kept:** Check that `.ai-team/` and `.squad/` are NOT tracked on preview branch (release hygiene)

### 6. **squad-promote.yml** (dev → preview → main promotion)
- **Before:** `node -e` for version reads in 3 places
- **After:** `grep -oP '(?<=<Version>).*(?=</Version>)' src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj` for all version reads
- **Kept:** Dry run mode, forbidden path stripping, CHANGELOG validation

### 7. **squad-insider-release.yml** (Insider builds)
- **Before:** Node.js tests, `node -e` for version, npm install instructions in release notes
- **After:** .NET tests, `grep` for version, **`dotnet add package ElBruno.LocalLLMs --version X.X.X-insider+SHA`** in release notes
- **Kept:** Insider version format (`0.1.0-insider+a1b2c3d`), prerelease flag

### 8. **squad-docs.yml** (Documentation)
- **Before:** `node docs/build.js` (script doesn't exist)
- **After:** **Simplified to validate `docs/` directory exists**, list `.md` files, upload raw markdown
- **Rationale:** No docs build system exists yet — validate structure only

## Implementation Details

### Version Extraction Pattern
All workflows now use:
```bash
VERSION=$(grep -oP '(?<=<Version>).*(?=</Version>)' src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj)
```

### .NET Setup Pattern
All workflows now use:
```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      8.0.x
      10.0.x
```

### Build Pattern
All workflows now use:
```bash
dotnet restore ElBruno.LocalLLMs.slnx
dotnet build ElBruno.LocalLLMs.slnx --configuration Release --no-restore
dotnet test tests/ElBruno.LocalLLMs.Tests/ElBruno.LocalLLMs.Tests.csproj --configuration Release --no-build --verbosity normal
```

## Consequences

### ✅ Benefits
- **All workflows now valid for C# project** — no more Node.js failures
- **squad-release.yml unblocked** — can now tag releases on push to main
- **publish.yml resilient** — succeeds even without NuGet key, still uploads artifact
- **squad-main-guard.yml correct** — allows `.squad/` on main (worktree-local strategy)
- **Consistent patterns** — all workflows use same .NET setup, version extraction, build commands

### 🔄 Release Flow (Fixed)
1. **Push to main** → `squad-release.yml` runs tests, validates CHANGELOG, creates `v0.1.0` tag
2. **Tag created** → `publish.yml` builds, tests, packs, pushes to NuGet (if key set), uploads artifact
3. **Manual promotion** → `squad-promote.yml` can promote dev → preview → main with .NET version reads

### 📝 Maintenance
- **Version bumps:** Update `<Version>` in `.csproj` AND add entry to `CHANGELOG.md`
- **New .NET versions:** Add to `dotnet-version` list in all workflows (currently 8.0.x + 10.0.x)
- **NuGet key:** Set `NUGET_API_KEY` secret in GitHub repo settings to enable NuGet publishing

### ⚠️ Known Limitations
- **squad-docs.yml** only validates docs exist — no HTML site generation yet
- **Integration tests** not run in CI (gated by `RUN_INTEGRATION_TESTS=true` env var)
- **CHANGELOG validation** checks for `## [X.Y.Z]` format — must match project CHANGELOG style

## References
- **Working reference:** `.github/workflows/ci.yml` (C# CI workflow)
- **Version source:** `src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj` line 14
- **CHANGELOG format:** Keep a Changelog (keepachangelog.com)
- **Related decision:** Worktree-local strategy (allows `.squad/` on main)
