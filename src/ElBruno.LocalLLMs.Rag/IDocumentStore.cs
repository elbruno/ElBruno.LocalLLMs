namespace ElBruno.LocalLLMs.Rag;

public interface IDocumentStore
{
    Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int topK = 5, float minSimilarity = 0.0f, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
