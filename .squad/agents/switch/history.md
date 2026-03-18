# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.LocalLLMs — C# library for local LLM chat completions using ONNX Runtime
- **Stack:** C#, .NET 9, ONNX Runtime, Microsoft.Extensions.AI (IChatClient), NuGet
- **Reference repos:** elbruno/elbruno.localembeddings (embeddings), elbruno/ElBruno.QwenTTS (TTS)
- **Key dependency:** ElBruno.HuggingFace.Downloader for model downloads from HuggingFace
- **Target models:** Phi-3.5-mini, Qwen2.5-3B, Llama-3.2-3B (small); Qwen2.5-7B, Phi-4 (medium)
- **Created:** 2026-03-17

## Architecture Status

**2026-03-17:** Morpheus completed full solution architecture. Blueprint in `docs/architecture.md`. 9 decisions merged to `.squad/decisions.md`. Build infra (csproj, Directory.Build.props) and CI/CD scripting depend on architecture choices (net8.0, net10.0, ONNX GenAI). Switch can now proceed with solution scaffolding.

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **.NET 10.0.200 SDK available** — supports slnx format natively. No need for .sln migration.
- **Microsoft.Extensions.AI.Abstractions 10.4.0 breaking changes:** IChatClient interface renamed methods: `CompleteAsync` → `GetResponseAsync`, `CompleteStreamingAsync` → `GetStreamingResponseAsync`. Return types changed: `ChatCompletion` → `ChatResponse`, `StreamingChatCompletionUpdate` → `ChatResponseUpdate`. `ChatClientMetadata` constructor parameter renamed `modelId` → `defaultModelId`. `ChatResponseUpdate.Text` is now read-only (use constructor).
- **OnnxRuntimeGenAI 0.8.3 API change:** `Generator.GetNextTokens()` removed. Use `generator.GetSequence(0)[^1]` to get the latest token after `GenerateNextToken()`.
- **ElBruno.HuggingFace.Downloader latest is 0.6.0** (not 0.5.0 as architecture doc stated).
- **xUnit 2.9.0 / NSubstitute 5.3.0** are latest stable (not 2.8.2 / 5.1.1). TreatWarningsAsErrors means NuGet version resolution warnings become hard errors — pin exact versions.
- **Record types with `string[]` properties** don't get value equality — arrays use reference equality. Tests must compare properties individually or share array instances.
- **Multi-target `net8.0;net10.0` works** with the current package set. All dependencies resolve for both TFMs.
- **GitHub Actions workflows scaffolded from Node.js templates** — All Squad workflows (squad-release, squad-ci, squad-promote, etc.) were Node.js templates applied to C# project. Required full rewrite: `setup-node` → `setup-dotnet`, `node --test` → `dotnet test`, version extraction from `package.json` → `.csproj`. Version extraction pattern: `grep -oP '(?<=<Version>).*(?=</Version>)' src/ElBruno.LocalLLMs/ElBruno.LocalLLMs.csproj`.
- **Worktree-local strategy allows `.squad/` on main** — Unlike standard Squad setup (where `.squad/` stays on dev), this project commits `.squad/` and `.ai-team/` to main by design. `squad-main-guard.yml` updated to allow these paths.
- **NuGet publishing requires secret check** — `publish.yml` checks if `NUGET_API_KEY` secret is set before attempting push. If not set, workflow still succeeds and uploads artifact (enables testing publish workflow without NuGet credentials).
- **CHANGELOG validation expects Keep a Changelog format** — All release workflows validate CHANGELOG.md has `## [X.Y.Z]` entry matching version in `.csproj`. Must update both files before release.
- **NuGet OIDC Trusted Publishing replaces API keys** — `publish.yml` now uses `NuGet/login@v1` for OIDC token exchange instead of `NUGET_API_KEY`. Requires: (1) NuGet.org Trusted Publishing policy matching repo/workflow/environment, (2) GitHub `release` environment, (3) `NUGET_USER` secret (profile name). Trigger changed from `push: tags` to `release: published` + `workflow_dispatch`.
- **Release chain: main → squad-release → publish** — Push to main triggers `squad-release.yml` which creates a GitHub Release via `gh release create`. The `release: published` event then triggers `publish.yml`. No manual tag creation needed.
- **GITHUB_TOKEN suppresses most triggered events** — GitHub Actions prevents recursive loops by not creating new workflow runs for events triggered by `GITHUB_TOKEN`, with two exceptions: `workflow_dispatch` and `repository_dispatch`. This means `release: published` won't fire when `gh release create` uses `GITHUB_TOKEN`. Fix: explicitly trigger publish via `gh workflow run publish.yml` (workflow_dispatch), which IS allowed. Requires `actions: write` permission.

