using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.Rag.Tests;

public class RagServiceExtensionsTests
{
    private readonly MockEmbeddingGenerator _mockGenerator;

    public RagServiceExtensionsTests()
    {
        _mockGenerator = new MockEmbeddingGenerator();
    }

    [Fact]
    public void AddLocalRagPipeline_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IRagPipeline>());
        Assert.NotNull(provider.GetService<IDocumentStore>());
        Assert.NotNull(provider.GetService<IDocumentChunker>());
        Assert.NotNull(provider.GetService<RagOptions>());
    }

    [Fact]
    public void AddLocalRagPipeline_WithCustomOptions_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator, options =>
        {
            options.ChunkSize = 1024;
            options.ChunkOverlap = 256;
            options.DefaultTopK = 10;
            options.DefaultMinSimilarity = 0.5f;
        });

        var provider = services.BuildServiceProvider();
        var ragOptions = provider.GetRequiredService<RagOptions>();

        Assert.Equal(1024, ragOptions.ChunkSize);
        Assert.Equal(256, ragOptions.ChunkOverlap);
        Assert.Equal(10, ragOptions.DefaultTopK);
        Assert.Equal(0.5f, ragOptions.DefaultMinSimilarity);
    }

    [Fact]
    public void AddLocalRagPipeline_WithoutOptions_UsesDefaults()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var ragOptions = provider.GetRequiredService<RagOptions>();

        Assert.Equal(512, ragOptions.ChunkSize);
        Assert.Equal(128, ragOptions.ChunkOverlap);
        Assert.Equal(5, ragOptions.DefaultTopK);
        Assert.Equal(0.0f, ragOptions.DefaultMinSimilarity);
    }

    [Fact]
    public void AddLocalRagPipeline_WithEmbeddingGenerator_RegistersGenerator()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(generator);
        Assert.Same(_mockGenerator, generator);
    }

    [Fact]
    public void AddLocalRagPipeline_WithEmbeddingGeneratorAndOptions_AppliesBoth()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator, options =>
        {
            options.ChunkSize = 2048;
        });

        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var ragOptions = provider.GetRequiredService<RagOptions>();

        Assert.Same(_mockGenerator, generator);
        Assert.Equal(2048, ragOptions.ChunkSize);
    }

    [Fact]
    public void AddLocalRagPipeline_RegistersInMemoryStore()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();

        Assert.IsAssignableFrom<InMemoryDocumentStore>(store);
    }

    [Fact]
    public void AddLocalRagPipeline_RegistersLocalRagPipeline()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IRagPipeline>();

        Assert.IsAssignableFrom<LocalRagPipeline>(pipeline);
    }

    [Fact]
    public void AddLocalRagPipeline_RegistersSlidingWindowChunker()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var chunker = provider.GetRequiredService<IDocumentChunker>();

        Assert.IsAssignableFrom<SlidingWindowChunker>(chunker);
    }

    [Fact]
    public void AddLocalRagPipeline_ChunkerUsesOptions()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator, options =>
        {
            options.ChunkSize = 1024;
            options.ChunkOverlap = 256;
        });

        var provider = services.BuildServiceProvider();
        var chunker = provider.GetRequiredService<IDocumentChunker>() as SlidingWindowChunker;

        Assert.NotNull(chunker);
    }

    [Fact]
    public void AddSqliteDocumentStore_RegistersSqliteStore()
    {
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";

        services.AddSqliteDocumentStore(connectionString);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();

        Assert.IsAssignableFrom<SqliteDocumentStore>(store);
    }

    [Fact]
    public void AddSqliteDocumentStore_UsesProvidedConnectionString()
    {
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";

        services.AddSqliteDocumentStore(connectionString);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>() as SqliteDocumentStore;

        Assert.NotNull(store);
    }

    [Fact]
    public void AddLocalRagPipeline_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredService<IRagPipeline>();
        var pipeline2 = provider.GetRequiredService<IRagPipeline>();

        Assert.Same(pipeline1, pipeline2);
    }

    [Fact]
    public void AddSqliteDocumentStore_ReplacesDefaultInMemoryStore()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);
        services.AddSqliteDocumentStore("Data Source=:memory:");

        var provider = services.BuildServiceProvider();

        // Get all registered IDocumentStore services
        var stores = provider.GetServices<IDocumentStore>().ToList();

        // The last registered store should be SqliteDocumentStore
        var store = provider.GetRequiredService<IDocumentStore>();
        Assert.IsAssignableFrom<SqliteDocumentStore>(store);
    }
}
