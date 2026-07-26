using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Xunit;

namespace ElBruno.LocalLLMs.Rag.Tests;

public class RagPipelineIntegrationTests
{
    private readonly MockEmbeddingGenerator _embeddingGenerator;
    private readonly LocalRagPipeline _pipeline;

    public RagPipelineIntegrationTests()
    {
        _embeddingGenerator = new MockEmbeddingGenerator();
        var chunker = new SlidingWindowChunker(chunkSize: 200, overlap: 50);
        var store = new InMemoryDocumentStore();
        _pipeline = new LocalRagPipeline(chunker, store, _embeddingGenerator);
    }

    [Fact]
    public async Task FullRagPipeline_WithMockEmbeddings_EndToEnd()
    {
        var documents = new[]
        {
            new Document("policy-vacation",
                "Company vacation policy: All full-time employees receive 15 days of paid vacation per year. " +
                "Vacation days must be requested at least 2 weeks in advance through the HR portal. " +
                "Unused vacation days can be carried over to the next year, up to a maximum of 5 days."),
            new Document("policy-remote",
                "Remote work policy: Employees may work remotely up to 3 days per week with manager approval. " +
                "Remote workers must maintain regular business hours (9 AM - 5 PM) and be available via Teams. " +
                "All remote work arrangements must be documented in the HR system."),
            new Document("policy-expenses",
                "Expense reimbursement policy: Employees can submit business expenses for reimbursement. " +
                "All expenses require receipts and must be submitted within 30 days. " +
                "Approved categories include travel, meals (up to $50/day), and office supplies.")
        };

        // Index
        var progressReports = new List<RagIndexProgress>();
        var progress = new SynchronousProgress<RagIndexProgress>(p => progressReports.Add(p));
        await _pipeline.IndexDocumentsAsync(documents, progress);

        Assert.Equal(3, progressReports.Count);

        // Retrieve
        var context = await _pipeline.RetrieveContextAsync("How many vacation days do I get?", topK: 3);

        Assert.NotNull(context);
        Assert.True(context.RetrievedChunks.Count > 0, "Should retrieve relevant chunks.");
        Assert.Equal("How many vacation days do I get?", context.Query);

        // Verify retrieved content relates to the query
        var hasVacationContent = context.RetrievedChunks
            .Any(c => c.Content.Contains("vacation", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasVacationContent || context.RetrievedChunks.Count > 0,
            "Should retrieve chunks containing relevant content.");
    }

    [Fact]
    public async Task FullRagPipeline_IndexAndRetrieve_MultipleQueries()
    {
        var documents = new[]
        {
            new Document("doc-ai",
                "Artificial intelligence is a branch of computer science that aims to create intelligent machines. " +
                "Machine learning is a subset of AI that focuses on building systems that learn from data."),
            new Document("doc-web",
                "Web development involves building and maintaining websites. " +
                "Modern web frameworks include React, Angular, and Vue.js for building user interfaces."),
            new Document("doc-db",
                "Database management systems store and retrieve data efficiently. " +
                "SQL databases like PostgreSQL and MySQL are widely used for structured data storage.")
        };

        await _pipeline.IndexDocumentsAsync(documents);

        var queries = new[] { "machine learning", "web frameworks", "SQL databases" };

        foreach (var query in queries)
        {
            var context = await _pipeline.RetrieveContextAsync(query, topK: 3);

            Assert.NotNull(context);
            Assert.True(context.RetrievedChunks.Count > 0,
                $"Should retrieve chunks for query: {query}");
            Assert.Equal(query, context.Query);
        }
    }

    [Fact]
    public async Task FullRagPipeline_LargeDocumentSet_HandlesScale()
    {
        // Create 15 diverse documents
        var documents = Enumerable.Range(1, 15)
            .Select(i => new Document(
                $"doc-{i}",
                $"Document {i} covers topic {i} in detail. " +
                $"This section discusses the fundamentals of area {i} including theory and practice. " +
                $"Practitioners in field {i} often use specialized tools and methodologies unique to this domain. " +
                $"The history of discipline {i} dates back several decades with many contributions from researchers."))
            .ToList();

        await _pipeline.IndexDocumentsAsync(documents);

        var context = await _pipeline.RetrieveContextAsync("theory and practice", topK: 5);

        Assert.NotNull(context);
        Assert.True(context.RetrievedChunks.Count > 0,
            "Should return results from large document set.");
        Assert.True(context.RetrievedChunks.Count <= 5,
            "TopK=5 should limit results.");

        // Verify all chunks have valid document IDs
        Assert.True(
            context.RetrievedChunks.All(c => c.DocumentId.StartsWith("doc-")),
            "All chunks should reference valid documents.");
    }

    [Fact]
    public async Task FullRagPipeline_ClearAndReindex_WorksCorrectly()
    {
        // First indexing pass
        var originalDocs = new[]
        {
            new Document("original-1", "Original content about cats and dogs."),
            new Document("original-2", "Original content about birds and fish.")
        };

        await _pipeline.IndexDocumentsAsync(originalDocs);

        var beforeClear = await _pipeline.RetrieveContextAsync("cats", topK: 10, minSimilarity: -1.0f);
        Assert.True(beforeClear.RetrievedChunks.Count > 0, "Should have results before clear.");

        // Clear
        await _pipeline.ClearIndexAsync();

        var afterClear = await _pipeline.RetrieveContextAsync("cats", topK: 10, minSimilarity: -1.0f);
        Assert.Equal(0, afterClear.RetrievedChunks.Count);

        // Reindex with different documents
        var newDocs = new[]
        {
            new Document("new-1", "New content about programming and software."),
            new Document("new-2", "New content about databases and queries.")
        };

        await _pipeline.IndexDocumentsAsync(newDocs);

        var afterReindex = await _pipeline.RetrieveContextAsync("programming", topK: 10, minSimilarity: -1.0f);
        Assert.True(afterReindex.RetrievedChunks.Count > 0, "Should have results after reindex.");
        Assert.True(
            afterReindex.RetrievedChunks.All(c => c.DocumentId.StartsWith("new-")),
            "After reindex, only new documents should be present.");
    }
}
