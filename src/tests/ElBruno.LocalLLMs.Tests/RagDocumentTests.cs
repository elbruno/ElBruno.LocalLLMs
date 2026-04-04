using ElBruno.LocalLLMs.Rag;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for the RAG Document record — creation, properties, and validation.
/// </summary>
public class RagDocumentTests
{
    [Fact]
    public void Document_Creation_SetsIdAndContent()
    {
        var doc = new Document("doc-1", "Test content");

        Assert.Equal("doc-1", doc.Id);
        Assert.Equal("Test content", doc.Content);
    }

    [Fact]
    public void Document_Creation_DefaultMetadataIsNull()
    {
        var doc = new Document("doc-1", "Test content");

        Assert.Null(doc.Metadata);
    }

    [Fact]
    public void Document_Creation_WithMetadata()
    {
        var metadata = new Dictionary<string, object>
        {
            ["source"] = "test",
            ["page"] = 1
        };
        var doc = new Document("doc-1", "Content", metadata);

        Assert.NotNull(doc.Metadata);
        Assert.Equal("test", doc.Metadata!["source"]);
        Assert.Equal(1, doc.Metadata["page"]);
    }

    [Fact]
    public void Document_RecordEquality_SameValues()
    {
        var doc1 = new Document("doc-1", "Same content");
        var doc2 = new Document("doc-1", "Same content");

        Assert.Equal(doc1, doc2);
    }

    [Fact]
    public void Document_RecordEquality_DifferentIds()
    {
        var doc1 = new Document("doc-1", "Content");
        var doc2 = new Document("doc-2", "Content");

        Assert.NotEqual(doc1, doc2);
    }

    [Fact]
    public void Document_RecordEquality_DifferentContent()
    {
        var doc1 = new Document("doc-1", "Content A");
        var doc2 = new Document("doc-1", "Content B");

        Assert.NotEqual(doc1, doc2);
    }

    [Fact]
    public void Document_EmptyContent_IsValid()
    {
        var doc = new Document("doc-1", "");

        Assert.Equal("", doc.Content);
    }

    [Fact]
    public void DocumentChunk_Creation_SetsAllProperties()
    {
        var embedding = new float[] { 1.0f, 0.5f, 0.0f };
        var chunk = new DocumentChunk("chunk-1", "doc-1", "Chunk text", embedding);

        Assert.Equal("chunk-1", chunk.Id);
        Assert.Equal("doc-1", chunk.DocumentId);
        Assert.Equal("Chunk text", chunk.Content);
        Assert.Equal(3, chunk.Embedding.Length);
    }

    [Fact]
    public void DocumentChunk_DefaultMetadataIsNull()
    {
        var chunk = new DocumentChunk("c1", "d1", "text", new float[] { 1.0f });

        Assert.Null(chunk.Metadata);
    }

    [Fact]
    public void DocumentChunk_WithMetadata()
    {
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var chunk = new DocumentChunk("c1", "d1", "text", new float[] { 1.0f }, metadata);

        Assert.NotNull(chunk.Metadata);
        Assert.Equal("value", chunk.Metadata!["key"]);
    }

    [Fact]
    public void RagContext_Creation_SetsQueryAndChunks()
    {
        var chunks = new List<DocumentChunk>
        {
            new("c1", "d1", "content1", new float[] { 1.0f }),
            new("c2", "d1", "content2", new float[] { 0.5f })
        };

        var context = new RagContext("test query", chunks);

        Assert.Equal("test query", context.Query);
        Assert.Equal(2, context.RetrievedChunks.Count);
    }

    [Fact]
    public void RagContext_EmptyChunks_IsValid()
    {
        var context = new RagContext("query", new List<DocumentChunk>());

        Assert.Empty(context.RetrievedChunks);
    }

    [Fact]
    public void RagIndexProgress_ReportsCorrectValues()
    {
        var progress = new RagIndexProgress(3, 10);

        Assert.Equal(3, progress.Processed);
        Assert.Equal(10, progress.Total);
    }
}
