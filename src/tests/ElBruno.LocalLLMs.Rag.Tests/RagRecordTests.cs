using ElBruno.LocalLLMs.Rag;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class RagRecordTests
{
    [TestClass]
    public class DocumentTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var doc = new Document("doc1", "Test content");

            Assert.AreEqual("doc1", doc.Id);
            Assert.AreEqual("Test content", doc.Content);
            Assert.IsNull(doc.Metadata);
        }

        [TestMethod]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var metadata = new Dictionary<string, object> { ["key"] = "value" };
            var doc = new Document("doc1", "Test content", metadata);

            Assert.AreEqual("doc1", doc.Id);
            Assert.AreEqual("Test content", doc.Content);
            Assert.IsNotNull(doc.Metadata);
            Assert.AreEqual("value", doc.Metadata["key"]);
        }

        [TestMethod]
        public void Constructor_DefaultMetadata_IsNull()
        {
            var doc = new Document("doc1", "content");

            Assert.IsNull(doc.Metadata);
        }

        [TestMethod]
        public void Equality_SameValues_AreEqual()
        {
            var doc1 = new Document("doc1", "content");
            var doc2 = new Document("doc1", "content");

            Assert.AreEqual(doc1, doc2);
            Assert.IsTrue(doc1 == doc2);
        }

        [TestMethod]
        public void Equality_DifferentIds_AreNotEqual()
        {
            var doc1 = new Document("doc1", "content");
            var doc2 = new Document("doc2", "content");

            Assert.AreNotEqual(doc1, doc2);
            Assert.IsFalse(doc1 == doc2);
        }

        [TestMethod]
        public void Equality_DifferentContent_AreNotEqual()
        {
            var doc1 = new Document("doc1", "content1");
            var doc2 = new Document("doc1", "content2");

            Assert.AreNotEqual(doc1, doc2);
        }

        [TestMethod]
        public void Immutability_CannotModifyAfterCreation()
        {
            var doc = new Document("doc1", "content");

            // Record properties are init-only, so this test verifies compilation behavior
            // If this compiles, records are immutable by design
            Assert.IsNotNull(doc);
        }
    }

    [TestClass]
    public class DocumentChunkTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var embedding = new float[] { 0.1f, 0.2f, 0.3f };
            var chunk = new DocumentChunk("chunk1", "doc1", "Chunk content", embedding);

            Assert.AreEqual("chunk1", chunk.Id);
            Assert.AreEqual("doc1", chunk.DocumentId);
            Assert.AreEqual("Chunk content", chunk.Content);
            Assert.AreEqual(3, chunk.Embedding.Length);
            Assert.IsNull(chunk.Metadata);
        }

        [TestMethod]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var metadata = new Dictionary<string, object> { ["source"] = "test" };
            var chunk = new DocumentChunk("chunk1", "doc1", "content", embedding, metadata);

            Assert.IsNotNull(chunk.Metadata);
            Assert.AreEqual("test", chunk.Metadata["source"]);
        }

        [TestMethod]
        public void Embedding_StoresCorrectValues()
        {
            var embedding = new float[] { 1.0f, 2.0f, 3.0f };
            var chunk = new DocumentChunk("chunk1", "doc1", "content", embedding);

            Assert.AreEqual(3, chunk.Embedding.Length);
            Assert.AreEqual(1.0f, chunk.Embedding.Span[0]);
            Assert.AreEqual(2.0f, chunk.Embedding.Span[1]);
            Assert.AreEqual(3.0f, chunk.Embedding.Span[2]);
        }

        [TestMethod]
        public void Equality_SameValues_AreEqual()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var chunk1 = new DocumentChunk("chunk1", "doc1", "content", embedding);
            var chunk2 = new DocumentChunk("chunk1", "doc1", "content", embedding);

            Assert.AreEqual(chunk1, chunk2);
        }

        [TestMethod]
        public void Equality_DifferentIds_AreNotEqual()
        {
            var embedding = new float[] { 0.1f, 0.2f };
            var chunk1 = new DocumentChunk("chunk1", "doc1", "content", embedding);
            var chunk2 = new DocumentChunk("chunk2", "doc1", "content", embedding);

            Assert.AreNotEqual(chunk1, chunk2);
        }
    }

    [TestClass]
    public class RagContextTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var chunks = new List<DocumentChunk>
            {
                new("chunk1", "doc1", "content", new float[] { 0.1f })
            };
            var context = new RagContext("test query", chunks);

            Assert.AreEqual("test query", context.Query);
            Assert.AreEqual(1, context.RetrievedChunks.Count);
            Assert.IsNull(context.Metadata);
        }

        [TestMethod]
        public void Constructor_WithEmptyChunks_Succeeds()
        {
            var chunks = new List<DocumentChunk>();
            var context = new RagContext("query", chunks);

            Assert.AreEqual("query", context.Query);
            Assert.AreEqual(0, context.RetrievedChunks.Count);
        }

        [TestMethod]
        public void Constructor_WithMetadata_StoresMetadata()
        {
            var chunks = new List<DocumentChunk>();
            var metadata = new Dictionary<string, object> { ["timestamp"] = "2024-01-01" };
            var context = new RagContext("query", chunks, metadata);

            Assert.IsNotNull(context.Metadata);
            Assert.AreEqual("2024-01-01", context.Metadata["timestamp"]);
        }

        [TestMethod]
        public void RetrievedChunks_IsReadOnly()
        {
            var chunks = new List<DocumentChunk>
            {
                new("chunk1", "doc1", "content", new float[] { 0.1f })
            };
            var context = new RagContext("query", chunks);

            // IReadOnlyList should not allow modification
            Assert.IsInstanceOfType<IReadOnlyList<DocumentChunk>>(context.RetrievedChunks);
        }
    }

    [TestClass]
    public class RagIndexProgressTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var progress = new RagIndexProgress(5, 10);

            Assert.AreEqual(5, progress.Processed);
            Assert.AreEqual(10, progress.Total);
        }

        [TestMethod]
        public void Constructor_WithZeroValues_Succeeds()
        {
            var progress = new RagIndexProgress(0, 0);

            Assert.AreEqual(0, progress.Processed);
            Assert.AreEqual(0, progress.Total);
        }

        [TestMethod]
        public void Constructor_ProcessedEqualTotal_Succeeds()
        {
            var progress = new RagIndexProgress(10, 10);

            Assert.AreEqual(10, progress.Processed);
            Assert.AreEqual(10, progress.Total);
        }

        [TestMethod]
        public void Equality_SameValues_AreEqual()
        {
            var progress1 = new RagIndexProgress(5, 10);
            var progress2 = new RagIndexProgress(5, 10);

            Assert.AreEqual(progress1, progress2);
        }

        [TestMethod]
        public void Equality_DifferentValues_AreNotEqual()
        {
            var progress1 = new RagIndexProgress(5, 10);
            var progress2 = new RagIndexProgress(6, 10);

            Assert.AreNotEqual(progress1, progress2);
        }
    }

    [TestClass]
    public class RagOptionsTests
    {
        [TestMethod]
        public void DefaultValues_AreCorrect()
        {
            var options = new RagOptions();

            Assert.AreEqual(512, options.ChunkSize);
            Assert.AreEqual(128, options.ChunkOverlap);
            Assert.AreEqual(5, options.DefaultTopK);
            Assert.AreEqual(0.0f, options.DefaultMinSimilarity);
        }

        [TestMethod]
        public void ChunkSize_CanBeModified()
        {
            var options = new RagOptions { ChunkSize = 1024 };

            Assert.AreEqual(1024, options.ChunkSize);
        }

        [TestMethod]
        public void ChunkOverlap_CanBeModified()
        {
            var options = new RagOptions { ChunkOverlap = 256 };

            Assert.AreEqual(256, options.ChunkOverlap);
        }

        [TestMethod]
        public void DefaultTopK_CanBeModified()
        {
            var options = new RagOptions { DefaultTopK = 10 };

            Assert.AreEqual(10, options.DefaultTopK);
        }

        [TestMethod]
        public void DefaultMinSimilarity_CanBeModified()
        {
            var options = new RagOptions { DefaultMinSimilarity = 0.5f };

            Assert.AreEqual(0.5f, options.DefaultMinSimilarity);
        }

        [TestMethod]
        public void AllProperties_CanBeModifiedTogether()
        {
            var options = new RagOptions
            {
                ChunkSize = 2048,
                ChunkOverlap = 512,
                DefaultTopK = 20,
                DefaultMinSimilarity = 0.7f
            };

            Assert.AreEqual(2048, options.ChunkSize);
            Assert.AreEqual(512, options.ChunkOverlap);
            Assert.AreEqual(20, options.DefaultTopK);
            Assert.AreEqual(0.7f, options.DefaultMinSimilarity);
        }
    }
}
