using ElBruno.LocalLLMs.Rag.Chunking;
using Xunit;

namespace ElBruno.LocalLLMs.Rag.Tests;

public class ChunkerTests
{
    [Fact]
    public void ChunkDocument_EmptyContent_ReturnsNoChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(0, chunks.Count);
    }

    [Fact]
    public void ChunkDocument_WhitespaceContent_ReturnsNoChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "   \n\t  ");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(0, chunks.Count);
    }

    [Fact]
    public void ChunkDocument_SingleChar_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 2);
        var document = new Document("doc1", "a");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(1, chunks.Count);
        Assert.Equal("a", chunks[0]);
    }

    [Fact]
    public void ChunkDocument_SmallerThanChunkSize_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 100, overlap: 20);
        var document = new Document("doc1", "Hello world");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(1, chunks.Count);
        Assert.Equal("Hello world", chunks[0]);
    }

    [Fact]
    public void ChunkDocument_ExactlyChunkSize_ReturnsSingleChunk()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 1);
        var document = new Document("doc1", "Hello");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(1, chunks.Count);
        Assert.Equal("Hello", chunks[0]);
    }

    [Fact]
    public void ChunkDocument_NoOverlap_ReturnsSequentialChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 0);
        var document = new Document("doc1", "0123456789");

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("01234", chunks[0]);
        Assert.Equal("56789", chunks[1]);
    }

    [Fact]
    public void ChunkDocument_WithOverlap_ReturnsOverlappingChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 5, overlap: 2);
        var document = new Document("doc1", "0123456789");

        var chunks = chunker.ChunkDocument(document).ToList();

        // With chunkSize=5, overlap=2, stride=3: 0-4, 3-7, 6-10 (but 10 is max, so 6-9) = 3 chunks
        Assert.Equal(3, chunks.Count);
        Assert.Equal("01234", chunks[0]);
        Assert.Equal("34567", chunks[1]);
        Assert.Equal("6789", chunks[2]);
    }

    [Fact]
    public void ChunkDocument_LargeDocument_ReturnsMultipleChunks()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 10, overlap: 3);
        var content = new string('a', 100);
        var document = new Document("doc1", content);

        var chunks = chunker.ChunkDocument(document).ToList();

        Assert.True(chunks.Count > 10);
        Assert.True(chunks.All(c => c.Length <= 10));
    }

    [Fact]
    public void Constructor_NegativeChunkSize_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowChunker(chunkSize: -1, overlap: 0));
    }

    [Fact]
    public void Constructor_ZeroChunkSize_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowChunker(chunkSize: 0, overlap: 0));
    }

    [Fact]
    public void Constructor_NegativeOverlap_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowChunker(chunkSize: 10, overlap: -1));
    }

    [Fact]
    public void Constructor_OverlapEqualToChunkSize_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new SlidingWindowChunker(chunkSize: 10, overlap: 10));
    }

    [Fact]
    public void Constructor_OverlapGreaterThanChunkSize_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new SlidingWindowChunker(chunkSize: 10, overlap: 15));
    }
}
