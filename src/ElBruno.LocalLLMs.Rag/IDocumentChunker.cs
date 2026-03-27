namespace ElBruno.LocalLLMs.Rag;

public interface IDocumentChunker
{
    IEnumerable<string> ChunkDocument(Document document);
}
