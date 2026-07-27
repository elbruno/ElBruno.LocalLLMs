# Integration Test Results

This directory contains per-run markdown reports for the integration lifecycle test suite.

## File naming

Each file is named `YYYY-MM-DD-HH-run-results.md` (UTC hour, 24-hour clock):

```
docs/tests/2026-07-26-21-run-results.md
docs/tests/2026-07-27-09-run-results.md
```

Only **one file per hour** is kept — if two runs complete within the same UTC hour, the second
overwrites the first. This prevents unbounded file accumulation while always showing the latest run.

## How to run the integration tests

Integration tests require:
- `RUN_INTEGRATION_TESTS=true` environment variable
- Sufficient disk space and network access (model downloads)
- Hardware capable of running INT4 ONNX inference (any modern CPU; GPU optional)

```powershell
# Run all integration tests (downloads models as needed)
$env:RUN_INTEGRATION_TESTS = "true"
dotnet test src/tests/ElBruno.LocalLLMs.IntegrationTests --framework net8.0

# Run only lifecycle tests for text models
dotnet test src/tests/ElBruno.LocalLLMs.IntegrationTests --framework net8.0 `
    --filter "FullyQualifiedName~ModelLifecycleTests"

# Run only tool-calling lifecycle tests
dotnet test src/tests/ElBruno.LocalLLMs.IntegrationTests --framework net8.0 `
    --filter "FullyQualifiedName~ToolCallingLifecycleTests"

# Run only vision lifecycle tests
dotnet test src/tests/ElBruno.LocalLLMs.IntegrationTests --framework net8.0 `
    --filter "FullyQualifiedName~VisionLifecycleTests"
```

## Lifecycle test phases

| Phase | What it tests |
|-------|---------------|
| **Phase A — Fresh download** | Clears the local cache, creates a client, downloads the model, asks a math question, asserts the answer contains "4". |
| **Phase B — Cache hit** | Creates a new client against the same cache, verifies fast initialization (no re-download), asks the same question, asserts the answer. |
| **Phase C — Delete** | Calls `DeleteModelFromCacheAsync()`, asserts the model directory is gone. |

## Non-native ONNX models (`HasNativeOnnx=false`)

These models cannot be auto-downloaded. To run their lifecycle tests, set:

```powershell
$env:MODEL_PATH_STABLELM_2_1_6B_CHAT = "C:\models\stablelm-onnx"
$env:MODEL_PATH_GEMMA_4_E2B_IT = "C:\models\gemma-4-e2b-onnx"
# ... etc.
```

The env var name is `MODEL_PATH_` followed by the model ID uppercased with `-`, `.`, `/` replaced by `_`.

## Report structure

Each report contains:
- Run timestamp and duration
- Pass/fail/skip per model and phase
- Phase timing (how long download, cache-hit, and deletion took)
- Error messages for failed phases

Results are written by the `TestRunReporter` xUnit collection fixture at the end of each test run.
