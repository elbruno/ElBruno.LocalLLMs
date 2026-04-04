using System.Collections.Concurrent;

namespace ElBruno.LocalLLMs.Rag.Storage;

/// <summary>
/// An in-memory implementation of a document store that stores chunks in RAM.
/// </summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentBag<DocumentChunk> _chunks = new();

    /// <summary>
    /// Adds a document chunk to the in-memory store.
    /// </summary>
    /// <param name="chunk">The document chunk to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        _chunks.Add(chunk);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Searches for document chunks similar to the query embedding using cosine similarity.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="minSimilarity">The minimum similarity threshold (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of document chunks ordered by similarity.</returns>
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

    /// <summary>
    /// Clears all document chunks from the store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
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
