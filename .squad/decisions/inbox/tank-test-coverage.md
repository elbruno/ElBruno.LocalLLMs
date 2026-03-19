# Decision: BuildProviderFailureReason visibility changed to internal

**Date:** 2026-03-19
**Author:** Tank (Tester)
**Status:** Active

**Context:** `OnnxGenAIModel.BuildProviderFailureReason` was `private static`, making it untestable directly. The method has non-trivial behavior (truncation at 180 chars, newline replacement, formatting) that warrants direct unit tests.

**Decision:** Changed `BuildProviderFailureReason` from `private static` to `internal static`. The test project already has `InternalsVisibleTo` configured, so no additional plumbing was needed.

**Rationale:** Testing this through the constructor would require mocking `Model` creation (ONNX native interop) — impractical without a factory seam. Direct testing is cleaner and catches regressions in truncation/formatting logic. This follows the same pattern used for `ShouldFallbackToNextProvider` and `GetProviderFallbackOrder`, which are already `internal static`.

**Consequences:** None negative. The method is a pure function with no side effects. Exposing it to `internal` does not change any public API surface.
