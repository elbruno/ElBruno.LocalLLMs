using ElBruno.LocalLLMs.Rag.Chunking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElBruno.LocalLLMs.Rag.Tests;

[TestClass]
public class ChunkerTests
{
    [TestMethod]
    public void ChunkDocument_EmptyContent_ReturnsNoChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(0, chunks.Count);
    }

    [TestMethod]
    public void ChunkDocument_WhitespaceContent_ReturnsNoChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "   \n\t  ");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(0, chunks.Count);
    }

    [TestMethod]
    public void ChunkDocument_SingleChar_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "a");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual("a", chunks[0]);
    }

    [TestMethod]
    public void ChunkDocument_SmallerThanChunkSize_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 100, overlap: 20);
        var document = new Document("doc1", "Hello world");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual("Hello world", chunks[0]);
    }

    [TestMethod]
    public void ChunkDocument_ExactlyChunkSize_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 1);
        var document = new Document("doc1", "Hello");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual("Hello", chunks[0]);
    }

    [TestMethod]
    public void ChunkDocument_NoOverlap_ReturnsSequentialChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 0);
        var document = new Document("doc1", "0123456789");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.AreEqual(2, chunks.Count);
        Assert.AreEqual("01234", chunks[0]);
        Assert.AreEqual("56789", chunks[1]);
    }

    [TestMethod]
    public void ChunkDocument_WithOverlap_ReturnsOverlappingChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 2);
        var document = new Document("doc1", "0123456789");

        var chunks = chunker.ChunkDocument(document).ToList();

        // With chunkSize=5, overlap=2, stride=3: 0-4, 3-7, 6-10 (but 10 is max, so 6-9) = 3 chunks
        Assert.AreEqual(3, chunks.Count);
        Assert.AreEqual("01234", chunks[0]);
        Assert.AreEqual("34567", chunks[1]);
        Assert.AreEqual("6789", chunks[2]);
    }

    [TestMethod]
    public void ChunkDocument_LargeDocument_ReturnsMultipleChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 3);
        var content = new string('a', 100);
        var document = new Document("doc1", content);

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.IsTrue(chunks.Count > 10);
        Assert.IsTrue(chunks.All(c => c.Length <= 10));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Constructor_NegativeChunkSize_ThrowsException()
    {
        _ = new SlidingWindowChunker(chunkSize: -1, overlap: 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Constructor_ZeroChunkSize_ThrowsException()
    {
        _ = new SlidingWindowChunker(chunkSize: 0, overlap: 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Constructor_NegativeOverlap_ThrowsException()
    {
        _ = new SlidingWindowChunker(chunkSize: 10, overlap: -1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_OverlapEqualToChunkSize_ThrowsException()
    {
        _ = new SlidingWindowChunker(chunkSize: 10, overlap: 10);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_OverlapGreaterThanChunkSize_ThrowsException()
    {
        _ = new SlidingWindowChunker(chunkSize: 10, overlap: 15);
    }
}
