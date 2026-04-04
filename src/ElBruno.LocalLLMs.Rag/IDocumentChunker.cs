namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Interface for document chunking strategies.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Splits a document into text chunks.
    /// </summary>
    /// <param name="document">The document to chunk.</param>
    /// <returns>An enumerable of text chunks.</returns>
    IEnumerable<string> ChunkDocument(Document document);
}
