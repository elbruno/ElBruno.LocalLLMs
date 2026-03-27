# Benchmarks

Performance benchmarks for `ElBruno.LocalLLMs` using [BenchmarkDotNet](https://benchmarkdotnet.org/). The benchmark project measures the internal overhead of the library — chat template formatting and model registry operations.

## What's Measured

### Chat Template Benchmarks (`ChatTemplateBenchmarks`)

Measures the time and memory allocation for formatting chat messages into model-specific prompt formats. Each model family uses a different template (ChatML, Phi3, Llama3, Qwen, Mistral, Gemma, DeepSeek).

**Benchmarks:**

| Benchmark | Description |
|-----------|-------------|
| ChatML - 3 messages | Format system + user + assistant with ChatML tokens |
| Phi3 - 3 messages | Format 3 messages with Phi-3 special tokens |
| Llama3 - 3 messages | Format 3 messages with Llama 3 header/eot tokens |
| Qwen - 3 messages | Format 3 messages with Qwen im_start/im_end tokens |
| Mistral - 3 messages | Format 3 messages with Mistral [INST] tokens |
| Gemma - 3 messages | Format 3 messages with Gemma turn tokens |
| DeepSeek - 3 messages | Format 3 messages with DeepSeek tokens |
| ChatML - 10 messages | Format a 10-message multi-turn conversation (ChatML) |
| Gemma - 10 messages | Format a 10-message multi-turn conversation (Gemma) |

These benchmarks verify that template formatting adds negligible overhead compared to actual model inference (which takes seconds, not microseconds).

### Model Definition Benchmarks (`ModelDefinitionBenchmarks`)

Measures the performance of the `KnownModels` registry — iterating all models and looking up models by ID.

**Benchmarks:**

| Benchmark | Description |
|-----------|-------------|
| KnownModels.All iteration | Enumerate all 23 registered models |
| FindById (first model) | Look up `tinyllama-1.1b-chat` (first in the list) |
| FindById (last model) | Look up `command-r-35b` (last in the list) |
| FindById (not found) | Search for a non-existent model ID |
| FindById (case insensitive) | Look up `PHI-3.5-MINI-INSTRUCT` with wrong casing |

These benchmarks ensure the model registry stays fast even as more models are added.

## How to Run

### Run all benchmarks

```bash
dotnet run -c Release --project src/benchmarks/ElBruno.LocalLLMs.Benchmarks
```

### Run a specific benchmark class

```bash
dotnet run -c Release --project src/benchmarks/ElBruno.LocalLLMs.Benchmarks -- --filter "*ChatTemplate*"
dotnet run -c Release --project src/benchmarks/ElBruno.LocalLLMs.Benchmarks -- --filter "*ModelDefinition*"
```

### Run a specific benchmark method

```bash
dotnet run -c Release --project src/benchmarks/ElBruno.LocalLLMs.Benchmarks -- --filter "*ChatML_3Messages*"
```

> **Important:** Always use `-c Release`. BenchmarkDotNet requires Release configuration for accurate results and will warn or refuse to run in Debug mode.

## Interpreting Results

BenchmarkDotNet outputs a table like this:

```
| Method             | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------------------- |----------:|---------:|---------:|-------:|----------:|
| ChatML - 3 msgs    |  1.234 μs | 0.012 μs | 0.010 μs | 0.0153 |     128 B |
| Phi3 - 3 msgs      |  1.456 μs | 0.015 μs | 0.013 μs | 0.0172 |     144 B |
| FindById (first)   |  0.089 μs | 0.001 μs | 0.001 μs |      - |         - |
```

**Key columns:**

| Column | What it means |
|--------|---------------|
| **Mean** | Average execution time per operation |
| **Error** | Half-width of the 99.9% confidence interval |
| **StdDev** | Standard deviation across iterations |
| **Gen0** | GC Gen0 collections per 1000 operations |
| **Allocated** | Memory allocated per operation |

**What to look for:**

- **Template formatting** should be in the low microsecond range (μs). If any template takes >100 μs, it may indicate a performance regression.
- **Memory allocation** should be minimal. The formatters use `StringBuilder` internally, so some allocation is expected. Watch for unexpected growth.
- **FindById** should be sub-microsecond for the current list size (23 models). If it grows significantly with more models, consider switching from linear scan to a dictionary.

## Configuration

The benchmarks use these BenchmarkDotNet settings:

- **`[MemoryDiagnoser]`** — Tracks GC and memory allocation
- **`[SimpleJob(warmupCount: 3, iterationCount: 10)]`** — 3 warmup iterations, 10 measured iterations (fast runs for development)
- **Target framework:** `net8.0`

## Adding New Benchmarks

1. Create a new class in `src/benchmarks/ElBruno.LocalLLMs.Benchmarks/`
2. Add `[MemoryDiagnoser]` and `[SimpleJob]` attributes
3. Use `[Benchmark]` on each method to measure
4. Run with `--filter` to test just your new benchmarks

The benchmark project references the main `ElBruno.LocalLLMs` project, so all public and internal APIs are available.

## See Also

- [Supported Models](supported-models.md) — performance expectations by model tier
- [Architecture](architecture.md) — how chat templates and model registry work internally
