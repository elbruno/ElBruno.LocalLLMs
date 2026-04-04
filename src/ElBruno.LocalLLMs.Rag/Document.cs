namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Represents a document to be indexed in the RAG pipeline.
/// </summary>
/// <param name="Id">The unique identifier for the document.</param>
/// <param name="Content">The text content of the document.</param>
/// <param name="Metadata">Optional metadata associated with the document.</param>
public sealed record Document(
    string Id,
    string Content,
    IDictionary<string, object>? Metadata = null);
