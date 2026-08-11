# Scripts

Helper scripts for testing, model management, and ONNX conversion.

---

## Test Runner

Use `run-tests.ps1` (Windows) or `run-tests.sh` (Linux/macOS/WSL) to build the solution, run unit tests, and run the full integration lifecycle test suite. After the integration run, the path to the auto-generated `docs/tests/YYYY-MM-DD-HH-run-results.md` report is printed.

### Quick start

```powershell
# Full run (PowerShell / Windows)
.\scripts\run-tests.ps1

# Full run (bash / Linux / macOS / WSL)
bash scripts/run-tests.sh
```

### Parameters — `run-tests.ps1`

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-SkipBuild` / `-NoBuild` | — | Skip `dotnet build` |
| `-SkipUnitTests` | — | Skip unit test project |
| `-SkipIntegrationTests` | — | Skip integration tests (build + unit only) |
| `-Framework` | `net8.0` | Target framework |
| `-HfToken <token>` | — | Sets `HF_TOKEN` for private HuggingFace repos |
| `-Filter <expr>` | — | xUnit `--filter` expression (integration tests only) |

### Parameters — `run-tests.sh`

| Flag | Default | Description |
|------|---------|-------------|
| `--skip-build` / `-B` / `--no-build` | — | Skip build |
| `--skip-unit-tests` / `-U` | — | Skip unit tests |
| `--skip-integration-tests` / `-I` | — | Skip integration tests |
| `--framework <value>` | `net8.0` | Target framework |
| `--hf-token <value>` | — | Sets `HF_TOKEN` |
| `--filter <value>` | — | xUnit filter expression |

### Common examples

```powershell
# Unit tests only (fast, no downloads)
.\scripts\run-tests.ps1 -SkipIntegrationTests

# Integration tests only, lifecycle tests only
.\scripts\run-tests.ps1 -SkipUnitTests -Filter "FullyQualifiedName~LifecycleTests"

# Integration tests with HuggingFace token (for private repos)
.\scripts\run-tests.ps1 -HfToken "hf_xxxx"

# Skip build (use existing binaries)
.\scripts\run-tests.ps1 -SkipBuild
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | All requested steps passed |
| `1` | Build failed |
| `2` | Unit tests failed |
| `3` | Integration tests failed |
| `99` | Unexpected error |

### Scheduling (unattended)

**Windows Task Scheduler** (daily at 2 AM):
```
Action:    powershell.exe
Arguments: -NonInteractive -ExecutionPolicy Bypass -File "C:\src\ElBruno.LocalLLMs\scripts\run-tests.ps1" -SkipBuild
```

**Linux/macOS cron** (daily at 2 AM):
```
0 2 * * * /bin/bash /path/to/scripts/run-tests.sh --skip-build >> /var/log/localllms-tests.log 2>&1
```

---

## Package Version Validation

Use `Validate-PackageAssemblyVersions.ps1` before publishing NuGet packages to confirm every packed `lib/**/*.dll` carries the same assembly version as the package version.

```powershell
.\scripts\Validate-PackageAssemblyVersions.ps1 -PackageDirectory .\artifacts
```

The script reads each `.nupkg`, extracts the assemblies to a repo-local scratch folder, compares their assembly versions with the package version, and fails fast on any mismatch.

---

## Model Cache Management

Use `manage-models.ps1` to inspect and manage downloaded models in cache roots.

## Model Cache Management

Use `manage-models.ps1` to inspect and manage downloaded models in cache roots.

Default cache root:

- `%LOCALAPPDATA%\ElBruno\LocalLLMs\models`

### List downloaded models (default)

```powershell
.\manage-models.ps1
```

### Show model storage location(s)

```powershell
.\manage-models.ps1 -Locations
```

### Show model totals and sizes

```powershell
.\manage-models.ps1 -Report
```

### Preview delete-one without deleting anything (dry run)

```powershell
.\manage-models.ps1 -Delete -Model "phi-3.5" -DryRun
```

### Preview delete operations using native PowerShell WhatIf

```powershell
.\manage-models.ps1 -Delete -Model "phi-3.5" -WhatIf
.\manage-models.ps1 -DeleteAll -WhatIf
```

### Delete one model by name/id/path fragment

```powershell
.\manage-models.ps1 -Delete -Model "phi-3.5"
```

### Delete all models safely

```powershell
# Interactive safety prompt
.\manage-models.ps1 -DeleteAll

# Non-interactive force mode
.\manage-models.ps1 -DeleteAll -Force
```

### Optional empty-folder cleanup

```powershell
.\manage-models.ps1 -DeleteAll -Force -CleanupEmptyFolders
```

### Notes

- Delete actions require explicit `-Delete` or `-DeleteAll`.
- Use `-DryRun` or native `-WhatIf` to preview delete operations.
- Use `-CacheDirectory` to target custom cache root(s).
- `delete-models.ps1` remains available for legacy workflows.

## Prerequisites

- Python 3.10+
- pip

## Setup

```bash
pip install -r requirements.txt
```

## Usage

### Basic conversion (INT4 quantization by default)

```bash
python convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-0.5B-Instruct \
    --output-dir ./models/qwen2.5-0.5b
```

### With INT8 quantization

```bash
python convert_to_onnx.py \
    --model-id meta-llama/Llama-3.2-3B-Instruct \
    --output-dir ./models/llama-3.2-3b \
    --quantize int8
```

### No quantization (full precision)

```bash
python convert_to_onnx.py \
    --model-id microsoft/Phi-3.5-mini-instruct \
    --output-dir ./models/phi-3.5-mini \
    --quantize none
```

### Models requiring trust-remote-code

```bash
python convert_to_onnx.py \
    --model-id Qwen/Qwen2.5-3B-Instruct \
    --output-dir ./models/qwen2.5-3b \
    --trust-remote-code
```

## Notes

- **Phi-3.5 and Phi-4** already have native ONNX weights on HuggingFace — no conversion needed. Use them directly with `KnownModels.Phi35MiniInstruct` or `KnownModels.Phi4`.
- Conversion requires significant disk space and RAM. Expect 2-4x the model size during conversion.
- INT4 quantization produces the smallest models with minimal quality loss — recommended for local inference.
- Output files can be used directly with `LocalLLMsOptions.ModelPath`.

## Supported Models

| Model | HuggingFace ID | Native ONNX? |
|-------|---------------|--------------|
| Phi-3.5 mini | `microsoft/Phi-3.5-mini-instruct-onnx` | ✅ Yes |
| Phi-4 | `microsoft/phi-4-onnx` | ✅ Yes |
| Qwen2.5-0.5B | `Qwen/Qwen2.5-0.5B-Instruct` | ❌ Convert |
| Qwen2.5-3B | `Qwen/Qwen2.5-3B-Instruct` | ❌ Convert |
| Llama-3.2-3B | `meta-llama/Llama-3.2-3B-Instruct` | ❌ Convert |
