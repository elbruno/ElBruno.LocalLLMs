namespace ElBruno.LocalLLMs.Rag;

public sealed record DocumentChunk(
    string Id,
    string DocumentId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IDictionary<string, object>? Metadata = null);
