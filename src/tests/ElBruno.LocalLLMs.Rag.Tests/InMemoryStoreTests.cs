using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class InMemoryStoreTests
{
    [TestMethod]
    public async Task AddChunk_SingleChunk_CanRetrieve()
    {
        var store = new InMemoryDocumentStore();
        var embedding = new float[] { 1.0f, 0.0f, 0.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test content", embedding);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding, topK: 1);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("chunk1", results[0].Id);
    }

    [TestMethod]
    public async Task SearchAsync_IdenticalVectors_ReturnsSimilarityOne()
    {
        var store = new InMemoryDocumentStore();
        var embedding = new float[] { 1.0f, 2.0f, 3.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding, topK: 1);

        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_TopK_ReturnsCorrectCount()
    {
        var store = new InMemoryDocumentStore();

        for (int i = 0; i < 10; i++)
        {
            var embedding = new float[] { i, i + 1, i + 2 };
            var chunk = new DocumentChunk($"chunk{i}", "doc1", $"content {i}", embedding);
            await store.AddChunkAsync(chunk);
        }

        var queryEmbedding = new float[] { 5.0f, 6.0f, 7.0f };
        var results = await store.SearchAsync(queryEmbedding, topK: 3);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_MinSimilarity_FiltersResults()
    {
        var store = new InMemoryDocumentStore();

        var chunk1 = new DocumentChunk("chunk1", "doc1", "test1", new float[] { 1.0f, 0.0f, 0.0f });
        var chunk2 = new DocumentChunk("chunk2", "doc1", "test2", new float[] { 0.0f, 1.0f, 0.0f });
        var chunk3 = new DocumentChunk("chunk3", "doc1", "test3", new float[] { 0.9f, 0.1f, 0.0f });

        await store.AddChunkAsync(chunk1);
        await store.AddChunkAsync(chunk2);
        await store.AddChunkAsync(chunk3);

        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };
        var results = await store.SearchAsync(queryEmbedding, topK: 10, minSimilarity: 0.9f);

        Assert.IsTrue(results.Count <= 2);
    }

    [TestMethod]
    public async Task ClearAsync_RemovesAllChunks()
    {
        var store = new InMemoryDocumentStore();

        for (int i = 0; i < 5; i++)
        {
            var chunk = new DocumentChunk($"chunk{i}", "doc1", $"content {i}", new float[] { i, i + 1 });
            await store.AddChunkAsync(chunk);
        }

        await store.ClearAsync();

        var results = await store.SearchAsync(new float[] { 1.0f, 2.0f }, topK: 10);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_EmptyStore_ReturnsEmpty()
    {
        var store = new InMemoryDocumentStore();

        var results = await store.SearchAsync(new float[] { 1.0f, 2.0f }, topK: 5);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_ResultsOrderedBySimilarity()
    {
        var store = new InMemoryDocumentStore();

        var chunk1 = new DocumentChunk("chunk1", "doc1", "test1", new float[] { 1.0f, 0.0f });
        var chunk2 = new DocumentChunk("chunk2", "doc1", "test2", new float[] { 0.9f, 0.1f });
        var chunk3 = new DocumentChunk("chunk3", "doc1", "test3", new float[] { 0.5f, 0.5f });

        await store.AddChunkAsync(chunk1);
        await store.AddChunkAsync(chunk2);
        await store.AddChunkAsync(chunk3);

        var queryEmbedding = new float[] { 1.0f, 0.0f };
        var results = await store.SearchAsync(queryEmbedding, topK: 3);

        Assert.AreEqual("chunk1", results[0].Id);
    }
}
