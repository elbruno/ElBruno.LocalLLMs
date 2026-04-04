namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Represents a chunk of a document with its embedding vector.
/// </summary>
/// <param name="Id">The unique identifier for the chunk.</param>
/// <param name="DocumentId">The identifier of the parent document.</param>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="Embedding">The embedding vector for the chunk.</param>
/// <param name="Metadata">Optional metadata associated with the chunk.</param>
public sealed record DocumentChunk(
    string Id,
    string DocumentId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IDictionary<string, object>? Metadata = null);
