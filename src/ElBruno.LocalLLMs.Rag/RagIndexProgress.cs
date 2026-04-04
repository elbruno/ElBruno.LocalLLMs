namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Represents the progress of a RAG indexing operation.
/// </summary>
/// <param name="Processed">The number of documents processed so far.</param>
/// <param name="Total">The total number of documents to process.</param>
public sealed record RagIndexProgress(int Processed, int Total);
