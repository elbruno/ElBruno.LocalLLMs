using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Rag;

public sealed class LocalRagPipeline : IRagPipeline
{
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentStore _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public LocalRagPipeline(
        IDocumentChunker chunker,
        IDocumentStore store,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    }

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

    public Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        return _store.ClearAsync(cancellationToken);
    }
}
