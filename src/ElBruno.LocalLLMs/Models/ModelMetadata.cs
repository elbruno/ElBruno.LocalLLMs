namespace ElBruno.LocalLLMs;

/// <summary>
/// Metadata about a loaded ONNX GenAI model, parsed from genai_config.json.
/// Provides context window limits, model identity, and vocabulary information.
/// </summary>
public sealed record ModelMetadata
{
    /// <summary>Maximum sequence length (context window) the model supports.</summary>
    public int MaxSequenceLength { get; init; }

    /// <summary>Model name from configuration or model path.</summary>
    public string? ModelName { get; init; }

    /// <summary>Vocabulary size of the model's tokenizer, when available.</summary>
    public int? VocabSize { get; init; }
}
