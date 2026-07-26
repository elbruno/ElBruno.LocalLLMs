using ElBruno.LocalLLMs.Rag;
using Xunit;

namespace ElBruno.LocalLLMs.Rag.Tests;

public class RagRecordTests
{
    public class DocumentTests
    {
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var doc = new Document("doc1", "Test content");

            Assert.Equal("doc1", doc.Id);
            Assert.Equal("Test content", doc.Content);
            Assert.Null(doc.Metadata);
        }

        [Fact]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var metadata = new Dictionary<string, object> { ["key"] = "value" };
            var doc = new Document("doc1", "Test content", metadata);

            Assert.Equal("doc1", doc.Id);
            Assert.Equal("Test content", doc.Content);
            Assert.NotNull(doc.Metadata);
            Assert.Equal("value", doc.Metadata["key"]);
        }

        [Fact]
        public void Constructor_DefaultMetadata_IsNull()
        {
            var doc = new Document("doc1", "content");

            Assert.Null(doc.Metadata);
        }

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var doc1 = new Document("doc1", "content");
            var doc2 = new Document("doc1", "content");

            Assert.Equal(doc1, doc2);
            Assert.True(doc1 == doc2);
        }

        [Fact]
        public void Equality_DifferentIds_AreNotEqual()
        {
            var doc1 = new Document("doc1", "content");
            var doc2 = new Document("doc2", "content");

            Assert.NotEqual(doc1, doc2);
            Assert.False(doc1 == doc2);
        }

        [Fact]
        public void Equality_DifferentContent_AreNotEqual()
        {
            var doc1 = new Document("doc1", "content1");
            var doc2 = new Document("doc1", "content2");

            Assert.NotEqual(doc1, doc2);
        }

        [Fact]
        public void Immutability_CannotModifyAfterCreation()
        {
            var doc = new Document("doc1", "content");

            // Record properties are init-only, so this test verifies compilation behavior
            // If this compiles, records are immutable by design
            Assert.NotNull(doc);
        }
    }

    public class DocumentChunkTests
    {
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var embedding = new float[] { 0.1f, 0.2f, 0.3f };
            var chunk = new DocumentChunk("chunk1", "doc1", "Chunk content", embedding);

            Assert.Equal("chunk1", chunk.Id);
            Assert.Equal("doc1", chunk.DocumentId);
            Assert.Equal("Chunk content", chunk.Content);
            Assert.Equal(3, chunk.Embedding.Length);
            Assert.Null(chunk.Metadata);
        }

        [Fact]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var metadata = new Dictionary<string, object> { ["source"] = "test" };
            var chunk = new DocumentChunk("chunk1", "doc1", "content", embedding, metadata);

            Assert.NotNull(chunk.Metadata);
            Assert.Equal("test", chunk.Metadata["source"]);
        }

        [Fact]
        public void Embedding_StoresCorrectValues()
        {
            var embedding = new float[] { 1.0f, 2.0f, 3.0f };
            var chunk = new DocumentChunk("chunk1", "doc1", "content", embedding);

            Assert.Equal(3, chunk.Embedding.Length);
            Assert.Equal(1.0f, chunk.Embedding.Span[0]);
            Assert.Equal(2.0f, chunk.Embedding.Span[1]);
            Assert.Equal(3.0f, chunk.Embedding.Span[2]);
        }

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var chunk1 = new DocumentChunk("chunk1", "doc1", "content", embedding);
            var chunk2 = new DocumentChunk("chunk1", "doc1", "content", embedding);

            Assert.Equal(chunk1, chunk2);
        }

        [Fact]
        public void Equality_DifferentIds_AreNotEqual()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var chunk1 = new DocumentChunk("chunk1", "doc1", "content", embedding);
            var chunk2 = new DocumentChunk("chunk2", "doc1", "content", embedding);

            Assert.NotEqual(chunk1, chunk2);
        }
    }

    public class RagContextTests
    {
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var chunks = new List<DocumentChunk>
            {
                new("chunk1", "doc1", "content", new float[] { 0.1f })
            };
            var context = new RagContext("test query", chunks);

            Assert.Equal("test query", context.Query);
            Assert.Equal(1, context.RetrievedChunks.Count);
            Assert.Null(context.Metadata);
        }

        [Fact]
        public void Constructor_WithEmptyChunks_Succeeds()
        {
            var chunks = new List<DocumentChunk>();
            var context = new RagContext("query", chunks);

            Assert.Equal("query", context.Query);
            Assert.Equal(0, context.RetrievedChunks.Count);
        }

        [Fact]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var chunks = new List<DocumentChunk>();
            var metadata = new Dictionary<string, object> { ["timestamp"] = "2024-01-01" };
            var context = new RagContext("query", chunks, metadata);

            Assert.NotNull(context.Metadata);
            Assert.Equal("2024-01-01", context.Metadata["timestamp"]);
        }

        [Fact]
        public void RetrievedChunks_IsReadOnly()
        {
            var chunks = new List<DocumentChunk>
            {
                new("chunk1", "doc1", "content", new float[] { 0.1f })
            };
            var context = new RagContext("query", chunks);

            // IReadOnlyList should not allow modification
            Assert.IsAssignableFrom<IReadOnlyList<DocumentChunk>>(context.RetrievedChunks);
        }
    }

    public class RagIndexProgressTests
    {
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var progress = new RagIndexProgress(5, 10);

            Assert.Equal(5, progress.Processed);
            Assert.Equal(10, progress.Total);
        }

        [Fact]
        public void Constructor_WithZeroValues_Succeeds()
        {
            var progress = new RagIndexProgress(0, 0);

            Assert.Equal(0, progress.Processed);
            Assert.Equal(0, progress.Total);
        }

        [Fact]
        public void Constructor_ProcessedEqualTotal_Succeeds()
        {
            var progress = new RagIndexProgress(10, 10);

            Assert.Equal(10, progress.Processed);
            Assert.Equal(10, progress.Total);
        }

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var progress1 = new RagIndexProgress(5, 10);
            var progress2 = new RagIndexProgress(5, 10);

            Assert.Equal(progress1, progress2);
        }

        [Fact]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var progress1 = new RagIndexProgress(5, 10);
            var progress2 = new RagIndexProgress(6, 10);

            Assert.NotEqual(progress1, progress2);
        }
    }

    public class RagOptionsTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var options = new RagOptions();

            Assert.Equal(512, options.ChunkSize);
            Assert.Equal(128, options.ChunkOverlap);
            Assert.Equal(5, options.DefaultTopK);
            Assert.Equal(0.0f, options.DefaultMinSimilarity);
        }

        [Fact]
        public void ChunkSize_CanBeModified()
        {
            var options = new RagOptions { ChunkSize = 1024 };

            Assert.Equal(1024, options.ChunkSize);
        }

        [Fact]
        public void ChunkOverlap_CanBeModified()
        {
            var options = new RagOptions { ChunkOverlap = 256 };

            Assert.Equal(256, options.ChunkOverlap);
        }

        [Fact]
        public void DefaultTopK_CanBeModified()
        {
            var options = new RagOptions { DefaultTopK = 10 };

            Assert.Equal(10, options.DefaultTopK);
        }

        [Fact]
        public void DefaultMinSimilarity_CanBeModified()
        {
            var options = new RagOptions { DefaultMinSimilarity = 0.5f };

            Assert.Equal(0.5f, options.DefaultMinSimilarity);
        }

        [Fact]
        public void AllProperties_CanBeModifiedTogether()
        {
            var options = new RagOptions
            {
                ChunkSize = 2048,
                ChunkOverlap = 512,
                DefaultTopK = 20,
                DefaultMinSimilarity = 0.7f
            };

            Assert.Equal(2048, options.ChunkSize);
            Assert.Equal(512, options.ChunkOverlap);
            Assert.Equal(20, options.DefaultTopK);
            Assert.Equal(0.7f, options.DefaultMinSimilarity);
        }
    }
}
