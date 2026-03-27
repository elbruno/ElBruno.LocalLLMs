namespace ElBruno.LocalLLMs.Rag;

public sealed record Document(
    string Id,
    string Content,
    IDictionary<string, object>? Metadata = null);
