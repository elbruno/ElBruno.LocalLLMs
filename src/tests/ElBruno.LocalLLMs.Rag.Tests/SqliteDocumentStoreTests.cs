using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class SqliteDocumentStoreTests
{
    [TestMethod]
    public void Constructor_CreatesDatabase()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");

        // If constructor succeeds, database schema was created
        Assert.IsNotNull(store);
    }

    [TestMethod]
    public async Task AddChunkAsync_StoresChunk()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var chunk = new DocumentChunk("chunk1", "doc1", "Test content", embedding);

        await store.AddChunkAsync(chunk);

        var results = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("chunk1", results[0].Id);
        Assert.AreEqual("doc1", results[0].DocumentId);
        Assert.AreEqual("Test content", results[0].Content);
    }

    [TestMethod]
    public async Task AddChunkAsync_WithMetadata_StoresAndRetrievesMetadata()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var chunk = new DocumentChunk("chunk1", "doc1", "content", embedding, metadata);

        await store.AddChunkAsync(chunk);

        var results = await store.SearchAsync(embedding, topK: 1, minSimilarity: -1.0f);
        Assert.AreEqual(1, results.Count);
        Assert.IsNotNull(results[0].Metadata);
    }

    [TestMethod]
    public async Task SearchAsync_ReturnsResultsOrderedBySimilarity()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        
        // Create three chunks with different embeddings
        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };
        var similarEmbedding = new float[] { 0.9f, 0.1f, 0.1f };
        var lessEmbedding = new float[] { 0.5f, 0.5f, 0.5f };
        var dissimilarEmbedding = new float[] { 0.0f, 1.0f, 0.0f };

        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "similar", similarEmbedding));
        await store.AddChunkAsync(new DocumentChunk("chunk2", "doc2", "dissimilar", dissimilarEmbedding));
        await store.AddChunkAsync(new DocumentChunk("chunk3", "doc3", "less", lessEmbedding));

        var results = await store.SearchAsync(queryEmbedding, topK: 3, minSimilarity: -1.0f);

        Assert.AreEqual(3, results.Count);
        // First result should be most similar (chunk1)
        Assert.AreEqual("chunk1", results[0].Id);
    }

    [TestMethod]
    public async Task SearchAsync_RespectsTopK()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Add 5 chunks
        for (int i = 0; i < 5; i++)
        {
            await store.AddChunkAsync(new DocumentChunk($"chunk{i}", $"doc{i}", $"content{i}", embedding));
        }

        var results = await store.SearchAsync(embedding, topK: 3, minSimilarity: -1.0f);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_RespectsMinSimilarity()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");

        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };
        var similarEmbedding = new float[] { 0.99f, 0.01f, 0.01f };
        var dissimilarEmbedding = new float[] { 0.0f, 1.0f, 0.0f };

        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "similar", similarEmbedding));
        await store.AddChunkAsync(new DocumentChunk("chunk2", "doc2", "dissimilar", dissimilarEmbedding));

        // High similarity threshold should filter out dissimilar chunk
        var results = await store.SearchAsync(queryEmbedding, topK: 10, minSimilarity: 0.9f);

        Assert.IsTrue(results.Count >= 1, "Should return at least the similar chunk");
        Assert.IsTrue(results.All(r => r.Id != "chunk2" || r.Content == "similar"), 
            "Dissimilar chunk should be filtered by minSimilarity");
    }

    [TestMethod]
    public async Task ClearAsync_RemovesAllChunks()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "content1", embedding));
        await store.AddChunkAsync(new DocumentChunk("chunk2", "doc2", "content2", embedding));

        var beforeClear = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);
        Assert.AreEqual(2, beforeClear.Count);

        await store.ClearAsync();

        var afterClear = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);
        Assert.AreEqual(0, afterClear.Count);
    }

    [TestMethod]
    public void Dispose_CleansUpConnection()
    {
        var store = new SqliteDocumentStore("Data Source=:memory:");
        
        store.Dispose();

        // If dispose succeeds without exception, cleanup worked
        Assert.IsNotNull(store);
    }

    [TestMethod]
    public async Task AddChunkAsync_MultipleChunksFromSameDocument()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding1 = new float[] { 0.1f, 0.2f, 0.3f };
        var embedding2 = new float[] { 0.4f, 0.5f, 0.6f };

        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "content1", embedding1));
        await store.AddChunkAsync(new DocumentChunk("chunk2", "doc1", "content2", embedding2));

        var results = await store.SearchAsync(embedding1, topK: 10, minSimilarity: -1.0f);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.DocumentId == "doc1"));
    }

    [TestMethod]
    public async Task AddChunkAsync_MultipleChunksFromDifferentDocuments()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "content1", embedding));
        await store.AddChunkAsync(new DocumentChunk("chunk2", "doc2", "content2", embedding));
        await store.AddChunkAsync(new DocumentChunk("chunk3", "doc3", "content3", embedding));

        var results = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);

        Assert.AreEqual(3, results.Count);
        var documentIds = results.Select(r => r.DocumentId).Distinct().ToList();
        Assert.AreEqual(3, documentIds.Count);
    }

    [TestMethod]
    public async Task SearchAsync_EmptyStore_ReturnsEmpty()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        var results = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task AddChunkAsync_ReplaceExisting_UpdatesChunk()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding1 = new float[] { 0.1f, 0.2f, 0.3f };
        var embedding2 = new float[] { 0.4f, 0.5f, 0.6f };

        // Add chunk
        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "original content", embedding1));

        // Replace with same ID
        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "updated content", embedding2));

        var results = await store.SearchAsync(embedding2, topK: 10, minSimilarity: -1.0f);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("updated content", results[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_WithCancellationToken_Succeeds()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "content", embedding));

        using var cts = new CancellationTokenSource();
        var results = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f, cancellationToken: cts.Token);

        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task ClearAsync_WithCancellationToken_Succeeds()
    {
        using var store = new SqliteDocumentStore("Data Source=:memory:");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        await store.AddChunkAsync(new DocumentChunk("chunk1", "doc1", "content", embedding));

        using var cts = new CancellationTokenSource();
        await store.ClearAsync(cts.Token);

        var results = await store.SearchAsync(embedding, topK: 10, minSimilarity: -1.0f);
        Assert.AreEqual(0, results.Count);
    }
}
