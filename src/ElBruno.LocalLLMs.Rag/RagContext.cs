namespace ElBruno.LocalLLMs.Rag;

public sealed record RagContext(
    string Query,
    IReadOnlyList<DocumentChunk> RetrievedChunks,
    IDictionary<string, object>? Metadata = null);
