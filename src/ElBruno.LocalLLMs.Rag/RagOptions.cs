namespace ElBruno.LocalLLMs.Rag;

public sealed class RagOptions
{
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 128;
    public int DefaultTopK { get; set; } = 5;
    public float DefaultMinSimilarity { get; set; } = 0.0f;
}
