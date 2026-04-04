using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Local implementation of a RAG pipeline that uses document chunking, embeddings, and vector search.
/// </summary>
public sealed class LocalRagPipeline : IRagPipeline
{
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentStore _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    /// <summary>
    /// Initializes a new instance of the LocalRagPipeline.
    /// </summary>
    /// <param name="chunker">The document chunker to split documents.</param>
    /// <param name="store">The document store for persisting chunks.</param>
    /// <param name="embeddingGenerator">The embedding generator for creating vector embeddings.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public LocalRagPipeline(
        IDocumentChunker chunker,
        IDocumentStore store,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    }

    /// <summary>
    /// Indexes a collection of documents by chunking and embedding them.
    /// </summary>
    /// <param name="documents">The documents to index.</param>
    /// <param name="progress">Optional progress reporter for tracking indexing progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task IndexDocumentsAsync(
        IEnumerable<Document> documents,
        IProgress<RagIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var documentList = documents.ToList();
        var totalDocuments = documentList.Count;
        var processed = 0;

        foreach (var document in documentList)
        {
            var chunks = _chunker.ChunkDocument(document).ToList();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkText = chunks[i];
                var chunkId = $"{document.Id}_chunk_{i}";

                var embeddingResults = await _embeddingGenerator.GenerateAsync(
                    new[] { chunkText },
                    cancellationToken: cancellationToken);

                var embedding = embeddingResults[0].Vector;

                var chunk = new DocumentChunk(
                    Id: chunkId,
                    DocumentId: document.Id,
                    Content: chunkText,
                    Embedding: embedding,
                    Metadata: document.Metadata);

                await _store.AddChunkAsync(chunk, cancellationToken);
            }

            processed++;
            progress?.Report(new RagIndexProgress(processed, totalDocuments));
        }
    }

    /// <summary>
    /// Retrieves relevant document context for a query.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="topK">The maximum number of results to retrieve.</param>
    /// <param name="minSimilarity">The minimum similarity threshold.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A RAG context containing the query and retrieved chunks.</returns>
    public async Task<RagContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        var queryEmbeddingResults = await _embeddingGenerator.GenerateAsync(
            new[] { query },
            cancellationToken: cancellationToken);

        var queryEmbedding = queryEmbeddingResults[0].Vector;

        var chunks = await _store.SearchAsync(
            queryEmbedding,
            topK,
            minSimilarity,
            cancellationToken);

        return new RagContext(query, chunks);
    }

    /// <summary>
    /// Clears all indexed documents from the pipeline.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        return _store.ClearAsync(cancellationToken);
    }
}
