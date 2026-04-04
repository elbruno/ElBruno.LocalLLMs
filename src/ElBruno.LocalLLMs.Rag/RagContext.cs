namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Represents the context retrieved from a RAG query containing the original query, retrieved document chunks, and optional metadata.
/// </summary>
/// <param name="Query">The original query string used for retrieval.</param>
/// <param name="RetrievedChunks">The list of document chunks retrieved for this query.</param>
/// <param name="Metadata">Optional metadata associated with this context.</param>
public sealed record RagContext(
    string Query,
    IReadOnlyList<DocumentChunk> RetrievedChunks,
    IDictionary<string, object>? Metadata = null);
