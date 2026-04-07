# QA Review: monitor-gemma4-blocker.yml

**Date:** 2026-04-07
**Author:** Tank (Tester/QA)
**File:** `.github/workflows/monitor-gemma4-blocker.yml`
**Status:** Reviewed & fixed

## Bugs Fixed

### 1. Expression Injection in `evaluate` Job (Critical)

The `evaluate` job's `actions/github-script` step interpolated all job outputs directly into JavaScript via `${{ }}` string literals. The `latest_comment` output — sourced from untrusted upstream issue comments — was a real code injection vector. A malicious comment containing `'); process.exit(1); //` would execute arbitrary code in the workflow.

**Fix:** All outputs now pass through the step's `env:` block and are read via `process.env.*`. This is the standard GitHub Actions mitigation for expression injection.

### 2. NuGet API Failure Produces False Positives (Medium)

The NuGet fetch step had no error handling. If `curl` failed or returned garbage, `jq` could output `null` or empty string, which would compare as `!= 0.12.2` and trigger a false "new version" signal.

**Fix:** Added `set -eo pipefail` and explicit validation (`-z` and `= "null"` checks) with `::error::` annotation.

### 3. Shell Injection via External Data (Medium)

Shell `run:` blocks interpolated `${{ steps.nuget.outputs.latest_version }}` directly from NuGet API data. While NuGet version strings are well-constrained, the safe pattern is env var indirection.

**Fix:** Moved external data to step-level `env:` blocks in both `release-notes` and `evaluate-release` steps.

## Action Items for Switch

### KNOWN_BLOCKED_VERSION is Stale

`KNOWN_BLOCKED_VERSION` is `0.12.2` but NuGet already has `0.13.0`. The workflow **will trigger on its very first run** with score ≥ 20 (new version). If 0.13.0 is also confirmed blocked for Gemma 4, Switch should update to `0.13.0` before merging or first scheduled run.

### "architecture" Keyword May Be Noisy

The keyword `architecture` in the release notes search is generic. Any onnxruntime-genai release mentioning architecture changes (unrelated to Gemma) would contribute to a +40 keyword score. Consider narrowing to `gemma.architecture` or `model.architecture` if false positives appear.

## Verified Correct

- ✅ NuGet flat container API URL and `.versions[-1]` approach
- ✅ Confidence scoring: +20 new version, +40 keyword match, +30 issue closed = 90 max
- ✅ Issue dedup: checks open issues with `gemma4` label before creating
- ✅ Label auto-creation with try/catch pattern
- ✅ Permission scope: `contents: read, issues: write` is sufficient
- ✅ `workflow_dispatch` properly configured for manual triggers
- ✅ Job dependency: `evaluate` correctly declares `needs: [check-release, check-issue]`
- ✅ `release_notes.txt` lifecycle: written in `release-notes` step, guarded by `[ -f ]` in `evaluate-release`
- ✅ GitHub API calls use correct endpoints and Octokit patterns
- ✅ Issue body template is well-structured with evidence, links, and next steps
