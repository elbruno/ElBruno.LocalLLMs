namespace ElBruno.LocalLLMs.Rag.Chunking;

public sealed class SlidingWindowChunker : IDocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;

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
