namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Interface for storing and retrieving document chunks.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Adds a document chunk to the store.
    /// </summary>
    /// <param name="chunk">The document chunk to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for document chunks similar to the query embedding.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="minSimilarity">The minimum similarity threshold.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of document chunks ordered by similarity.</returns>
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int topK = 5, float minSimilarity = 0.0f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all document chunks from the store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
