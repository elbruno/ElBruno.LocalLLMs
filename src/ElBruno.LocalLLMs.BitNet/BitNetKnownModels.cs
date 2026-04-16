using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Pre-defined BitNet model catalog.
/// </summary>
public static class BitNetKnownModels
{
    /// <summary>
    /// Microsoft BitNet b1.58 2B-4T — the official flagship model.
    /// 2.4B params, trained on 4T tokens, MIT license.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet2B4T = new()
    {
        Id = "bitnet-b1.58-2b-4t",
        DisplayName = "BitNet b1.58 2B-4T",
        HuggingFaceRepoId = "microsoft/BitNet-b1.58-2B-4T-gguf",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 2.4,
        ContextLength = 4096,
        ApproximateSizeMB = 400
    };

    /// <summary>
    /// 1BitLLM bitnet_b1_58-large — community 0.7B model.
    /// Smallest BitNet model, good for testing/prototyping.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet07B = new()
    {
        Id = "bitnet-b1.58-0.7b",
        DisplayName = "BitNet b1.58 0.7B",
        HuggingFaceRepoId = "1bitLLM/bitnet_b1_58-large",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 0.7,
        ContextLength = 2048,
        ApproximateSizeMB = 150
    };

    /// <summary>
    /// 1BitLLM bitnet_b1_58-3B — community 3.3B model.
    /// Larger community model for better quality.
    /// </summary>
    public static readonly BitNetModelDefinition BitNet3B = new()
    {
        Id = "bitnet-b1.58-3b",
        DisplayName = "BitNet b1.58 3B",
        HuggingFaceRepoId = "1bitLLM/bitnet_b1_58-3B",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.Llama3,
        ParametersBillions = 3.3,
        ContextLength = 4096,
        ApproximateSizeMB = 650
    };

    /// <summary>
    /// Falcon3 1B Instruct 1.58-bit — instruction-tuned, smallest Falcon.
    /// </summary>
    public static readonly BitNetModelDefinition Falcon3_1B = new()
    {
        Id = "falcon3-1b-instruct-1.58bit",
        DisplayName = "Falcon3 1B Instruct 1.58-bit",
        HuggingFaceRepoId = "tiiuae/Falcon3-1B-Instruct-1.58bit",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.ChatML,
        ParametersBillions = 1.0,
        ContextLength = 8192,
        ApproximateSizeMB = 200
    };

    /// <summary>
    /// Falcon3 3B Instruct 1.58-bit — instruction-tuned, mid-tier Falcon.
    /// </summary>
    public static readonly BitNetModelDefinition Falcon3_3B = new()
    {
        Id = "falcon3-3b-instruct-1.58bit",
        DisplayName = "Falcon3 3B Instruct 1.58-bit",
        HuggingFaceRepoId = "tiiuae/Falcon3-3B-Instruct-1.58bit",
        GgufFileName = "ggml-model-i2_s.gguf",
        ChatTemplate = ChatTemplateFormat.ChatML,
        ParametersBillions = 3.0,
        ContextLength = 8192,
        ApproximateSizeMB = 600
    };

    /// <summary>Returns all known BitNet model definitions.</summary>
    public static IReadOnlyList<BitNetModelDefinition> All { get; } =
    [
        BitNet2B4T,
        BitNet07B,
        BitNet3B,
        Falcon3_1B,
        Falcon3_3B
    ];

    /// <summary>Finds a model by its ID string. Returns null if not found.</summary>
    public static BitNetModelDefinition? FindById(string modelId) =>
        All.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
}
