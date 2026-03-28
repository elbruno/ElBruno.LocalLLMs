namespace ElBruno.LocalLLMs;

/// <summary>
/// Metadata about a loaded ONNX GenAI model, parsed from genai_config.json.
/// Provides context window limits, model identity, and vocabulary information.
/// </summary>
public sealed record ModelMetadata
{
    /// <summary>
    /// The effective maximum sequence length that the ONNX Runtime GenAI Generator will enforce.
    /// This is the minimum of <see cref="ConfigMaxSequenceLength"/> and
    /// <see cref="LocalLLMsOptions.MaxSequenceLength"/>, reflecting the actual runtime limit.
    /// </summary>
    public int MaxSequenceLength { get; init; }

    /// <summary>
    /// The raw maximum sequence length read from genai_config.json
    /// (resolved from <c>search.max_length</c>, <c>model.context_length</c>, or <c>model.max_length</c>).
    /// This is the model's theoretical context window and may be much larger than
    /// what the runtime actually enforces via <see cref="MaxSequenceLength"/>.
    /// </summary>
    public int ConfigMaxSequenceLength { get; init; }

    /// <summary>Model name from configuration or model path.</summary>
    public string? ModelName { get; init; }

    /// <summary>Vocabulary size of the model's tokenizer, when available.</summary>
    public int? VocabSize { get; init; }
}
