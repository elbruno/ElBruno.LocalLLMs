using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class LocalRagPipelineConstructorTests
{
    [TestMethod]
    public void Constructor_WithNullChunker_ThrowsArgumentNullException()
    {
        var store = new InMemoryDocumentStore();
        var generator = new MockEmbeddingGenerator();

        var ex = Assert.ThrowsException<ArgumentNullException>(() =>
        {
            new LocalRagPipeline(null!, store, generator);
        });

        Assert.AreEqual("chunker", ex.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullStore_ThrowsArgumentNullException()
    {
        var chunker = new SlidingWindowChunker(512, 128);
        var generator = new MockEmbeddingGenerator();

        var ex = Assert.ThrowsException<ArgumentNullException>(() =>
        {
            new LocalRagPipeline(chunker, null!, generator);
        });

        Assert.AreEqual("store", ex.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullEmbeddingGenerator_ThrowsArgumentNullException()
    {
        var chunker = new SlidingWindowChunker(512, 128);
        var store = new InMemoryDocumentStore();

        var ex = Assert.ThrowsException<ArgumentNullException>(() =>
        {
            new LocalRagPipeline(chunker, store, null!);
        });

        Assert.AreEqual("embeddingGenerator", ex.ParamName);
    }

    [TestMethod]
    public void Constructor_WithValidParameters_Succeeds()
    {
        var chunker = new SlidingWindowChunker(512, 128);
        var store = new InMemoryDocumentStore();
        var generator = new MockEmbeddingGenerator();

        var pipeline = new LocalRagPipeline(chunker, store, generator);

        Assert.IsNotNull(pipeline);
    }

    [TestMethod]
    public void Constructor_WithDifferentChunkerOptions_Succeeds()
    {
        var chunker = new SlidingWindowChunker(1024, 256);
        var store = new InMemoryDocumentStore();
        var generator = new MockEmbeddingGenerator();

        var pipeline = new LocalRagPipeline(chunker, store, generator);

        Assert.IsNotNull(pipeline);
    }

    [TestMethod]
    public void Constructor_WithSqliteStore_Succeeds()
    {
        var chunker = new SlidingWindowChunker(512, 128);
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var generator = new MockEmbeddingGenerator();

        var pipeline = new LocalRagPipeline(chunker, store, generator);

        Assert.IsNotNull(pipeline);
    }
}
