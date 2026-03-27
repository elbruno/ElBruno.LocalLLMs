namespace ElBruno.LocalLLMs.Rag;

public interface IRagPipeline
{
    Task IndexDocumentsAsync(IEnumerable<Document> documents, IProgress<RagIndexProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<RagContext> RetrieveContextAsync(string query, int topK = 5, float minSimilarity = 0.0f, CancellationToken cancellationToken = default);
    Task ClearIndexAsync(CancellationToken cancellationToken = default);
}
