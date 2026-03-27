using System.Collections.Concurrent;

namespace ElBruno.LocalLLMs.Rag.Storage;

public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentBag<DocumentChunk> _chunks = new();

    public Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        _chunks.Add(chunk);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        var results = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Similarity = CosineSimilarity(queryEmbedding.Span, chunk.Embedding.Span)
            })
            .Where(x => x.Similarity >= minSimilarity)
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(results);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _chunks.Clear();
        return Task.CompletedTask;
    }

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        if (a.Length == 0)
            return 0.0f;

        float dotProduct = 0.0f;
        float normA = 0.0f;
        float normB = 0.0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        if (denominator == 0.0f)
            return 0.0f;

        return dotProduct / denominator;
    }
}
