namespace ElBruno.LocalLLMs.Rag.Chunking;

/// <summary>
/// A document chunker that uses a sliding window approach with configurable chunk size and overlap.
/// </summary>
public sealed class SlidingWindowChunker : IDocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    /// <summary>
    /// Initializes a new instance of the SlidingWindowChunker.
    /// </summary>
    /// <param name="chunkSize">The size of each chunk in characters. Default is 512.</param>
    /// <param name="overlap">The number of overlapping characters between chunks. Default is 128.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when chunkSize is not positive or overlap is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when overlap is greater than or equal to chunkSize.</exception>
    public SlidingWindowChunker(int chunkSize = 512, int overlap = 128)
    {
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive");
        if (overlap < 0)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be non-negative");
        if (overlap >= chunkSize)
            throw new ArgumentException("Overlap must be less than chunk size", nameof(overlap));

        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    /// <summary>
    /// Splits a document into text chunks using a sliding window approach.
    /// </summary>
    /// <param name="document">The document to chunk.</param>
    /// <returns>An enumerable of text chunks.</returns>
    public IEnumerable<string> ChunkDocument(Document document)
    {
        if (string.IsNullOrWhiteSpace(document.Content))
        {
            yield break;
        }

        var content = document.Content;
        var stride = _chunkSize - _overlap;

        for (int start = 0; start < content.Length; start += stride)
        {
            var end = Math.Min(start + _chunkSize, content.Length);
            var chunk = content.Substring(start, end - start);

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            if (end >= content.Length)
            {
                break;
            }
        }
    }
}
