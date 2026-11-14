# Publishing ElBruno.LocalLLMs to NuGet

This guide covers how the published ElBruno.LocalLLMs packages are published using **OIDC Trusted Publishing** — no long-lived API keys required.

## Package

| Package | Project | NuGet |
|---------|---------|-------|
| `ElBruno.LocalLLMs` | `src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj` | [nuget.org/packages/ElBruno.LocalLLMs](https://www.nuget.org/packages/ElBruno.LocalLLMs) |
| `ElBruno.LocalLLMs.Rag` | `src/ElBruno.LocalLLMs.Rag/ElBruno.LocalLLMs.Rag.csproj` | [nuget.org/packages/ElBruno.LocalLLMs.Rag](https://www.nuget.org/packages/ElBruno.LocalLLMs.Rag) |
| `ElBruno.LocalLLMs.BitNet` | `src/ElBruno.LocalLLMs.BitNet/ElBruno.LocalLLMs.BitNet.csproj` | [nuget.org/packages/ElBruno.LocalLLMs.BitNet](https://www.nuget.org/packages/ElBruno.LocalLLMs.BitNet) |
| `ElBruno.LocalLLMs.BlazorComponents` | `src/ElBruno.LocalLLMs.BlazorComponents/ElBruno.LocalLLMs.BlazorComponents.csproj` | [nuget.org/packages/ElBruno.LocalLLMs.BlazorComponents](https://www.nuget.org/packages/ElBruno.LocalLLMs.BlazorComponents) |
| `ElBruno.LocalLLMs.Decisions` | `src/ElBruno.LocalLLMs.Decisions/ElBruno.LocalLLMs.Decisions.csproj` | [nuget.org/packages/ElBruno.LocalLLMs.Decisions](https://www.nuget.org/packages/ElBruno.LocalLLMs.Decisions) |

---

## One-Time Setup

### 1. NuGet.org — Add Trusted Publishing Policy

1. Go to **nuget.org → Manage Packages → ElBruno.LocalLLMs → Trusted publishers**
2. Click **Add trusted publisher** and fill in:

   | Field | Value |
   |-------|-------|
   | Repository owner | `elbruno` |
   | Repository name | `ElBruno.LocalLLMs` |
   | Workflow file | `publish.yml` |
   | Environment | `release` |

3. Save the policy.

### 2. GitHub — Create `release` Environment

1. Go to **Settings → Environments → New environment**
2. Name it: `release`
3. (Optional) Add deployment protection rules:
   - Required reviewers — recommended for production packages
   - Limit to `main` branch only

### 3. GitHub — Add `NUGET_USER` Secret

1. Go to **Settings → Secrets and variables → Actions**
2. Add a new **repository secret**:

   | Name | Value |
   |------|-------|
   | `NUGET_USER` | Your NuGet.org profile username |

> **Note:** No `NUGET_API_KEY` secret is needed. OIDC Trusted Publishing replaces long-lived API keys with short-lived tokens issued per workflow run.

---

## How to Publish

### Option A: GitHub Release (Recommended)

The automated flow:

1. Use `scripts/Set-ReleaseVersion.ps1` to update the shared `<PublishedSiblingPackageVersion>` in `Directory.Build.props`, the README highlights, and `docs/CHANGELOG.md`.
2. Update the user-facing docs for the release surface (`README.md` plus any feature-specific guide such as `docs/observability.md`)
3. Push to `main`
4. `squad-release.yml` automatically creates a GitHub Release with tag `vX.Y.Z`
5. The release event triggers `publish.yml` → all five packages are built and pushed to NuGet.org at the same version

### Option B: Manual Dispatch

For ad-hoc or pre-release publishes:

1. Go to **Actions → Publish to NuGet → Run workflow**
2. Optionally enter a version (e.g., `1.2.0-beta.1`)
3. If left empty, the shared version from `Directory.Build.props` is used

> **Local machine note:** this repository's normal publish path is GitHub Actions OIDC. A local `dotnet pack` can validate the package contents, but a local `dotnet nuget push` still requires a maintainer-provided NuGet API key. Without that key, publish from the workflow instead of the terminal.

## Release Checklist

Before triggering publish:

1. `Directory.Build.props` matches the release version intended for all five packages.
2. `docs/CHANGELOG.md` contains the same version.
3. README and any feature docs reflect the shipped behavior.
4. `dotnet test ElBruno.LocalLLMs.slnx` passes locally for `net10.0` and `net11.0`.
5. The publish workflow successfully packs all five packages for both target frameworks.

---

## How It Works

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Push to     │────▶│ squad-release│────▶│  GitHub      │
│  main        │     │  creates tag │     │  Release     │
└──────────────┘     │  + release   │     │  (published) │
                     └──────────────┘     └──────┬───────┘
                                                 │
                                                 ▼
                     ┌──────────────────────────────────────┐
                     │  publish.yml                         │
                     │                                      │
                     │  1. Checkout code                    │
                     │  2. Setup .NET 11                    │
                     │  3. Determine version (tag/input/    │
                     │     csproj)                          │
                     │  4. Restore → Build → Test → Pack    │
                     │  5. NuGet/login@v1 (OIDC exchange)   │
                     │     ┌─────────────────────────────┐  │
                     │     │ GitHub OIDC token            │  │
                     │     │   → NuGet validates:         │  │
                     │     │     • repo owner             │  │
                     │     │     • repo name              │  │
                     │     │     • workflow file           │  │
                     │     │     • environment             │  │
                     │     │   → Returns temp API key     │  │
                     │     └─────────────────────────────┘  │
                     │  6. Push .nupkg to NuGet.org         │
                     │  7. Upload artifact                  │
                     └──────────────────────────────────────┘
```

### OIDC Flow

1. GitHub Actions mints a **short-lived OIDC token** (JWT) containing claims about the workflow run (repo, workflow file, environment, etc.)
2. `NuGet/login@v1` sends this token to NuGet.org
3. NuGet.org validates the token against the **Trusted Publishing policy** you configured
4. If all claims match, NuGet.org returns a **temporary API key** scoped only to the packages in the policy
5. The workflow uses this temp key to push — it expires after the run

**Benefits over API keys:**
- No secrets to rotate or leak
- Scoped to specific repo + workflow + environment
- Audit trail tied to GitHub Actions run ID

---

## Version Resolution

The workflow determines the package version using a 3-tier priority:

| Priority | Source | When |
|----------|--------|------|
| 1 | Release tag | `release` event — strips `v` prefix (e.g., `v1.2.3` → `1.2.3`) |
| 2 | Manual input | `workflow_dispatch` with version input provided |
| 3 | csproj fallback | `workflow_dispatch` with no version — reads `<Version>` from csproj |

All versions are validated against the regex `^[0-9]+\.[0-9]+\.[0-9]+` — the workflow fails fast on invalid versions.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `403 Forbidden` on NuGet push | Trusted Publishing policy mismatch | Verify repo owner, repo name, workflow file name, and environment all match exactly |
| `OIDC token exchange failed` | Missing `id-token: write` permission | Ensure the job has `permissions: id-token: write` |
| `Invalid version` error | Tag format unexpected | Ensure tags are `vX.Y.Z` (e.g., `v1.2.3`). Leading `v` and `.` are stripped |
| `NUGET_USER` not found | Secret not configured | Add `NUGET_USER` repository secret with your NuGet.org username |
| `Environment 'release' not found` | GitHub environment not created | Create the `release` environment in repo Settings → Environments |
| Package pushed but not visible | NuGet.org indexing delay | Packages can take 5–15 minutes to appear. Check nuget.org status page |
| Tests fail in publish workflow | Build config mismatch | Ensure tests pass locally with `dotnet test -c Release` |

---

## References

- [NuGet Trusted Publishing](https://devblogs.microsoft.com/nuget/trusted-publishing-for-nuget-packages/)
- [NuGet/login GitHub Action](https://github.com/NuGet/login)
- [GitHub OIDC for third-party services](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [GitHub Environments](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment)
