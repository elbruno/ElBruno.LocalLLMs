using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class LocalRagPipelineTests
{
    private MockEmbeddingGenerator _embeddingGenerator = null!;
    private SlidingWindowChunker _chunker = null!;
    private InMemoryDocumentStore _store = null!;
    private LocalRagPipeline _pipeline = null!;

    [TestInitialize]
    public void Setup()
    {
        _embeddingGenerator = new MockEmbeddingGenerator();
        _chunker = new SlidingWindowChunker(chunkSize: 200, overlap: 50);
        _store = new InMemoryDocumentStore();
        _pipeline = new LocalRagPipeline(_chunker, _store, _embeddingGenerator);
    }

    [TestMethod]
    public async Task IndexDocuments_EmptyCollection_Succeeds()
    {
        var documents = Enumerable.Empty<Document>();

        await _pipeline.IndexDocumentsAsync(documents);

        var context = await _pipeline.RetrieveContextAsync("anything");
        Assert.AreEqual(0, context.RetrievedChunks.Count);
    }

    [TestMethod]
    public async Task IndexDocuments_SingleDocument_ChunksAndStores()
    {
        var doc = new Document("doc1", "This is a short test document for indexing.");

        await _pipeline.IndexDocumentsAsync(new[] { doc });

        // Use minSimilarity=-1 to return all chunks regardless of cosine similarity
        var context = await _pipeline.RetrieveContextAsync("test document", topK: 10, minSimilarity: -1.0f);
        Assert.IsTrue(context.RetrievedChunks.Count > 0, "Expected at least one chunk after indexing.");
        Assert.IsTrue(
            context.RetrievedChunks.All(c => c.DocumentId == "doc1"),
            "All chunks should reference the original document.");
    }

    [TestMethod]
    public async Task IndexDocuments_MultipleDocuments_ReportsProgress()
    {
        var documents = new[]
        {
            new Document("doc1", "First document content about vacations and time off."),
            new Document("doc2", "Second document content about remote work policies."),
            new Document("doc3", "Third document content about expense reimbursement.")
        };

        // Use a synchronous IProgress implementation to avoid thread-pool ordering issues with Progress<T>
        var progressReports = new List<RagIndexProgress>();
        var progress = new SynchronousProgress<RagIndexProgress>(p => progressReports.Add(p));

        await _pipeline.IndexDocumentsAsync(documents, progress);

        Assert.AreEqual(3, progressReports.Count, "Should report progress for each document.");
        Assert.AreEqual(1, progressReports[0].Processed);
        Assert.AreEqual(3, progressReports[0].Total);
        Assert.AreEqual(2, progressReports[1].Processed);
        Assert.AreEqual(3, progressReports[1].Total);
        Assert.AreEqual(3, progressReports[2].Processed);
        Assert.AreEqual(3, progressReports[2].Total);
    }

    [TestMethod]
    public async Task RetrieveContext_AfterIndexing_ReturnsRelevantChunks()
    {
        var documents = new[]
        {
            new Document("vacation", "Employees receive 15 days of paid vacation per year."),
            new Document("remote", "Remote work is allowed up to 3 days per week.")
        };

        await _pipeline.IndexDocumentsAsync(documents);

        var context = await _pipeline.RetrieveContextAsync("vacation days", topK: 5, minSimilarity: -1.0f);

        Assert.IsNotNull(context);
        Assert.IsTrue(context.RetrievedChunks.Count > 0, "Should return at least one chunk.");
        Assert.AreEqual("vacation days", context.Query);
    }

    [TestMethod]
    public async Task RetrieveContext_EmptyIndex_ReturnsEmptyContext()
    {
        var context = await _pipeline.RetrieveContextAsync("any query");

        Assert.IsNotNull(context);
        Assert.AreEqual(0, context.RetrievedChunks.Count);
    }

    [TestMethod]
    public async Task RetrieveContext_TopKLimitsResults()
    {
        // Index enough documents to produce many chunks
        var documents = Enumerable.Range(0, 10)
            .Select(i => new Document($"doc{i}", $"Document number {i} with unique content about topic {i}."))
            .ToList();

        await _pipeline.IndexDocumentsAsync(documents);

        var context = await _pipeline.RetrieveContextAsync("document topic", topK: 3);

        Assert.IsTrue(
            context.RetrievedChunks.Count <= 3,
            $"TopK=3 should return at most 3 results, got {context.RetrievedChunks.Count}.");
    }

    [TestMethod]
    public async Task RetrieveContext_MinSimilarityFilters()
    {
        var documents = new[]
        {
            new Document("doc1", "Content about machine learning and artificial intelligence."),
            new Document("doc2", "Content about cooking recipes and food preparation.")
        };

        await _pipeline.IndexDocumentsAsync(documents);

        // Very high similarity threshold — should return few or no results
        var context = await _pipeline.RetrieveContextAsync("completely unrelated query xyz", topK: 10, minSimilarity: 0.99f);

        Assert.IsTrue(
            context.RetrievedChunks.Count < 2,
            "High minSimilarity should filter most results.");
    }

    [TestMethod]
    public async Task ClearIndex_RemovesAllChunks()
    {
        var doc = new Document("doc1", "Some content to index and then clear.");

        await _pipeline.IndexDocumentsAsync(new[] { doc });

        // Verify something was indexed
        var beforeClear = await _pipeline.RetrieveContextAsync("content", topK: 10, minSimilarity: -1.0f);
        Assert.IsTrue(beforeClear.RetrievedChunks.Count > 0, "Should have chunks before clear.");

        await _pipeline.ClearIndexAsync();

        var afterClear = await _pipeline.RetrieveContextAsync("content", topK: 10, minSimilarity: -1.0f);
        Assert.AreEqual(0, afterClear.RetrievedChunks.Count, "Should have no chunks after clear.");
    }

    [TestMethod]
    public async Task IndexDocuments_CancellationToken_Respected()
    {
        var documents = new[]
        {
            new Document("doc1", "First document."),
            new Document("doc2", "Second document."),
            new Document("doc3", "Third document.")
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await _pipeline.IndexDocumentsAsync(documents, cancellationToken: cts.Token);
        });
    }

    [TestMethod]
    public async Task RagContext_RetrievedChunks_ContainsChunkContent()
    {
        var expectedContent = "The quick brown fox jumps over the lazy dog.";
        var doc = new Document("doc1", expectedContent);

        await _pipeline.IndexDocumentsAsync(new[] { doc });

        var context = await _pipeline.RetrieveContextAsync("quick brown fox", topK: 5, minSimilarity: -1.0f);

        Assert.IsTrue(context.RetrievedChunks.Count > 0, "Should have retrieved chunks.");

        // Verify the retrieved chunks contain the original document content
        var allContent = string.Join(" ", context.RetrievedChunks.Select(c => c.Content));
        Assert.IsTrue(
            allContent.Contains("quick brown fox") || allContent.Contains("lazy dog"),
            "Retrieved chunk content should contain text from the indexed document.");
    }
}

/// <summary>
/// Deterministic mock embedding generator for testing.
/// Produces consistent 384-dimensional vectors based on text hash,
/// ensuring the same text always gets the same embedding.
/// </summary>
internal sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = values
            .Select(v => new Embedding<float>(GenerateEmbedding(v)))
            .ToList();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public EmbeddingGeneratorMetadata Metadata => new("mock-test-embedder");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private static ReadOnlyMemory<float> GenerateEmbedding(string text)
    {
        var hash = text.GetHashCode();
        var rng = new Random(hash);
        var vector = new float[384];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        // Normalize to unit vector
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}

/// <summary>
/// Synchronous IProgress implementation that invokes the callback inline,
/// avoiding thread-pool scheduling issues with <see cref="Progress{T}"/>.
/// </summary>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
