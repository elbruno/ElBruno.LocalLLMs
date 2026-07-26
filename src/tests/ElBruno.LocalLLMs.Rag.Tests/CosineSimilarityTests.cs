using ElBruno.LocalLLMs.Rag.Storage;
using Xunit;

namespace ElBruno.LocalLLMs.Rag.Tests;

public class CosineSimilarityTests
{
    [Fact]
    public async Task CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var store = new InMemoryDocumentStore();
        var embedding = new float[] { 1.0f, 2.0f, 3.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding, topK: 1);

        Assert.Equal(1, results.Count);
        Assert.Equal("chunk1", results[0].Id);
    }

    [Fact]
    public async Task CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var store = new InMemoryDocumentStore();
        var embedding1 = new float[] { 1.0f, 0.0f, 0.0f };
        var embedding2 = new float[] { 0.0f, 1.0f, 0.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding1);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding2, topK: 1, minSimilarity: 0.01f);

        Assert.Equal(0, results.Count);
    }

    [Fact]
    public async Task CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        var store = new InMemoryDocumentStore();
        var embedding1 = new float[] { 1.0f, 0.0f, 0.0f };
        var embedding2 = new float[] { -1.0f, 0.0f, 0.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding1);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding2, topK: 1, minSimilarity: -2.0f);

        Assert.Equal(1, results.Count);
    }

    [Fact]
    public async Task CosineSimilarity_ScaledVectors_SameDirection()
    {
        var store = new InMemoryDocumentStore();
        var embedding1 = new float[] { 1.0f, 2.0f, 3.0f };
        var embedding2 = new float[] { 2.0f, 4.0f, 6.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding1);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding2, topK: 1);

        Assert.Equal(1, results.Count);
        Assert.Equal("chunk1", results[0].Id);
    }

    [Fact]
    public async Task CosineSimilarity_ZeroVector_ReturnsZero()
    {
        var store = new InMemoryDocumentStore();
        var embedding1 = new float[] { 0.0f, 0.0f, 0.0f };
        var embedding2 = new float[] { 1.0f, 2.0f, 3.0f };
        var chunk = new DocumentChunk("chunk1", "doc1", "test", embedding1);

        await store.AddChunkAsync(chunk);
        var results = await store.SearchAsync(embedding2, topK: 1, minSimilarity: 0.01f);

        Assert.Equal(0, results.Count);
    }
}
