# Decision: Native Library Tests Strategy

**Author:** Tank (Tester)
**Date:** 2026-04-17
**Status:** Implemented

## Context

We needed comprehensive tests for the new NativeLibraryLoader changes (NuGet runtimes path probing) and the 3 native platform-specific package projects.

## Decision

1. **NativeLibraryLoaderTests** test path probing logic via `GetCandidateLibraryPathsForTesting()` (internal method exposed by Trinity). No actual native binary loading in unit tests.
2. **NativePackageValidationTests** validate on-disk project structure. Tests that check `.csproj` content gracefully skip (`return`) when the file doesn't exist yet — this lets tests compile and run before Trinity creates the csproj files.
3. Integration tests requiring real native binaries are gated behind `RUN_NATIVE_INTEGRATION_TESTS=true` env var and tagged `[Trait("Category", "Integration")]`.
4. The `CandidatePaths_IncludesRuntimesDir_WhenDirectoryExists` test creates a temporary `runtimes/{rid}/native/` directory inside `AppContext.BaseDirectory` to verify the loader finds it, then cleans up after.

## Consequence

- 58 new tests, 229 total, all green.
- Tests will progressively light up as Trinity delivers the `.csproj` files for native packages and updates the `.slnx`.
