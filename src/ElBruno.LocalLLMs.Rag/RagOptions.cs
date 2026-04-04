namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Configuration options for the RAG pipeline.
/// </summary>
public sealed class RagOptions
{
    /// <summary>
    /// Gets or sets the size of text chunks in characters. Default is 512.
    /// </summary>
    public int ChunkSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the number of overlapping characters between chunks. Default is 128.
    /// </summary>
    public int ChunkOverlap { get; set; } = 128;

    /// <summary>
    /// Gets or sets the default number of top results to retrieve. Default is 5.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default minimum similarity threshold for retrieval. Default is 0.0.
    /// </summary>
    public float DefaultMinSimilarity { get; set; } = 0.0f;
}
