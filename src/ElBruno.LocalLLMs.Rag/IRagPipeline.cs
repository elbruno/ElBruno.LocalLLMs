namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Interface for a Retrieval-Augmented Generation pipeline that indexes documents and retrieves relevant context.
/// </summary>
public interface IRagPipeline
{
    /// <summary>
    /// Indexes a collection of documents by chunking and embedding them.
    /// </summary>
    /// <param name="documents">The documents to index.</param>
    /// <param name="progress">Optional progress reporter for tracking indexing progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IndexDocumentsAsync(IEnumerable<Document> documents, IProgress<RagIndexProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves relevant document context for a query.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="topK">The maximum number of results to retrieve.</param>
    /// <param name="minSimilarity">The minimum similarity threshold.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A RAG context containing the query and retrieved chunks.</returns>
    Task<RagContext> RetrieveContextAsync(string query, int topK = 5, float minSimilarity = 0.0f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all indexed documents from the pipeline.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearIndexAsync(CancellationToken cancellationToken = default);
}
