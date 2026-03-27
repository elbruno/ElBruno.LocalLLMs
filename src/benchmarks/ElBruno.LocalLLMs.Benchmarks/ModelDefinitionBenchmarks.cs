using BenchmarkDotNet.Attributes;
using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ModelDefinitionBenchmarks
{
    [Benchmark(Description = "KnownModels.All iteration")]
    public int IterateAll()
    {
        var count = 0;
        foreach (var model in KnownModels.All)
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "FindById (first model)")]
    public ModelDefinition? FindById_First()
    {
        return KnownModels.FindById("tinyllama-1.1b-chat");
    }

    [Benchmark(Description = "FindById (last model)")]
    public ModelDefinition? FindById_Last()
    {
        return KnownModels.FindById("command-r-35b");
    }

    [Benchmark(Description = "FindById (not found)")]
    public ModelDefinition? FindById_NotFound()
    {
        return KnownModels.FindById("nonexistent-model");
    }

    [Benchmark(Description = "FindById (case insensitive)")]
    public ModelDefinition? FindById_CaseInsensitive()
    {
        return KnownModels.FindById("PHI-3.5-MINI-INSTRUCT");
    }
}
