# Decision: BitNet Native NuGet Package Architecture

**Date:** 2026-07-25
**Author:** Switch (DevOps/Packaging)
**Status:** Active

## Context

Users of `ElBruno.LocalLLMs.BitNet` currently must manually build bitnet.cpp and place native binaries on the system path. This is the #1 friction point for adoption.

## Decision

Ship platform-specific native NuGet packages that contain pre-built bitnet.cpp binaries:

- `ElBruno.LocalLLMs.BitNet.Native.win-x64` → `runtimes/win-x64/native/llama.dll`
- `ElBruno.LocalLLMs.BitNet.Native.linux-x64` → `runtimes/linux-x64/native/libllama.so`
- `ElBruno.LocalLLMs.BitNet.Native.osx-arm64` → `runtimes/osx-arm64/native/libllama.dylib`

### Key technical choices:
1. **Separate packages per RID** (not a single multi-RID package) — keeps package size small, users only download what they need
2. **Content-only csproj** (`netstandard2.0`, `NoBuild=true`) — no managed code, just native binaries in `runtimes/` layout
3. **Separate publish pipeline** (`publish-bitnet-native.yml`) — native builds are expensive (clone + compile bitnet.cpp), isolated from managed package publishing
4. **Reusable build workflow** (`build-bitnet-native.yml`) — can be called by publish or triggered manually for testing
5. **OIDC trusted publishing** — same pattern as existing `publish.yml`, no API keys stored
6. **Release tag convention:** `native-v*` (e.g., `native-v0.15.0`) to differentiate from managed package releases

## Consequences

- Users install with `dotnet add package ElBruno.LocalLLMs.BitNet.Native.win-x64` — zero manual setup
- Future: `ElBruno.LocalLLMs.BitNet` can add conditional `PackageReference` to auto-pull the right native package
- bitnet.cpp commit should be pinned to a SHA after first successful build to ensure reproducibility
- Native packages version independently from managed packages (different release cadence expected)
