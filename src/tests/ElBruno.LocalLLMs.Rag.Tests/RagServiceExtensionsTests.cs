using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class RagServiceExtensionsTests
{
    private MockEmbeddingGenerator _mockGenerator = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGenerator = new MockEmbeddingGenerator();
    }

    [TestMethod]
    public void AddLocalRagPipeline_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<IRagPipeline>());
        Assert.IsNotNull(provider.GetService<IDocumentStore>());
        Assert.IsNotNull(provider.GetService<IDocumentChunker>());
        Assert.IsNotNull(provider.GetService<RagOptions>());
    }

    [TestMethod]
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

        Assert.AreEqual(1024, ragOptions.ChunkSize);
        Assert.AreEqual(256, ragOptions.ChunkOverlap);
        Assert.AreEqual(10, ragOptions.DefaultTopK);
        Assert.AreEqual(0.5f, ragOptions.DefaultMinSimilarity);
    }

    [TestMethod]
    public void AddLocalRagPipeline_WithoutOptions_UsesDefaults()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var ragOptions = provider.GetRequiredService<RagOptions>();

        Assert.AreEqual(512, ragOptions.ChunkSize);
        Assert.AreEqual(128, ragOptions.ChunkOverlap);
        Assert.AreEqual(5, ragOptions.DefaultTopK);
        Assert.AreEqual(0.0f, ragOptions.DefaultMinSimilarity);
    }

    [TestMethod]
    public void AddLocalRagPipeline_WithEmbeddingGenerator_RegistersGenerator()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.IsNotNull(generator);
        Assert.AreSame(_mockGenerator, generator);
    }

    [TestMethod]
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

        Assert.AreSame(_mockGenerator, generator);
        Assert.AreEqual(2048, ragOptions.ChunkSize);
    }

    [TestMethod]
    public void AddLocalRagPipeline_RegistersInMemoryStore()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();

        Assert.IsInstanceOfType<InMemoryDocumentStore>(store);
    }

    [TestMethod]
    public void AddLocalRagPipeline_RegistersLocalRagPipeline()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IRagPipeline>();

        Assert.IsInstanceOfType<LocalRagPipeline>(pipeline);
    }

    [TestMethod]
    public void AddLocalRagPipeline_RegistersSlidingWindowChunker()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var chunker = provider.GetRequiredService<IDocumentChunker>();

        Assert.IsInstanceOfType<SlidingWindowChunker>(chunker);
    }

    [TestMethod]
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

        Assert.IsNotNull(chunker);
    }

    [TestMethod]
    public void AddSqliteDocumentStore_RegistersSqliteStore()
    {
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";

        services.AddSqliteDocumentStore(connectionString);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();

        Assert.IsInstanceOfType<SqliteDocumentStore>(store);
    }

    [TestMethod]
    public void AddSqliteDocumentStore_UsesProvidedConnectionString()
    {
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";

        services.AddSqliteDocumentStore(connectionString);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>() as SqliteDocumentStore;

        Assert.IsNotNull(store);
    }

    [TestMethod]
    public void AddLocalRagPipeline_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddLocalRagPipeline(_mockGenerator);

        var provider = services.BuildServiceProvider();
        var pipeline1 = provider.GetRequiredService<IRagPipeline>();
        var pipeline2 = provider.GetRequiredService<IRagPipeline>();

        Assert.AreSame(pipeline1, pipeline2);
    }

    [TestMethod]
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
        Assert.IsInstanceOfType<SqliteDocumentStore>(store);
    }
}
