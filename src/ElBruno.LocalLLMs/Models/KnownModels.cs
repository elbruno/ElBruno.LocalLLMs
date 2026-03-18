namespace ElBruno.LocalLLMs;

/// <summary>
/// Pre-defined model definitions for supported LLMs.
/// These are the models the library knows how to download, configure, and run.
/// </summary>
public static class KnownModels
{
    // ────────────────────────────────────────────────────────
    // ⚪ Tiny tier — edge/mobile, IoT, fast prototyping
    // ────────────────────────────────────────────────────────

    /// <summary>TinyLlama 1.1B Chat — smallest Llama-based chat model.</summary>
    public static readonly ModelDefinition TinyLlama11BChat = new()
    {
        Id = "tinyllama-1.1b-chat",
        DisplayName = "TinyLlama-1.1B-Chat",
        HuggingFaceRepoId = "elbruno/TinyLlama-1.1B-Chat-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = true
    };

    /// <summary>SmolLM2 1.7B Instruct — compact instruct model from HuggingFace.</summary>
    public static readonly ModelDefinition SmolLM2_17BInstruct = new()
    {
        Id = "smollm2-1.7b-instruct",
        DisplayName = "SmolLM2-1.7B-Instruct",
        HuggingFaceRepoId = "elbruno/SmolLM2-1.7B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = true
    };

    /// <summary>Qwen2.5-0.5B-Instruct — tiny edge model.</summary>
    public static readonly ModelDefinition Qwen25_05BInstruct = new()
    {
        Id = "qwen2.5-0.5b-instruct",
        DisplayName = "Qwen2.5-0.5B-Instruct",
        HuggingFaceRepoId = "elbruno/Qwen2.5-0.5B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = true
    };

    /// <summary>Qwen2.5-1.5B-Instruct — small Qwen model.</summary>
    public static readonly ModelDefinition Qwen25_15BInstruct = new()
    {
        Id = "qwen2.5-1.5b-instruct",
        DisplayName = "Qwen2.5-1.5B-Instruct",
        HuggingFaceRepoId = "elbruno/Qwen2.5-1.5B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = true
    };

    /// <summary>Gemma 2B IT — Google's tiny instruction-tuned model.</summary>
    public static readonly ModelDefinition Gemma2BIT = new()
    {
        Id = "gemma-2b-it",
        DisplayName = "Gemma-2B-IT",
        HuggingFaceRepoId = "google/gemma-2b-it",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Gemma,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = false
    };

    /// <summary>StableLM 2 Zephyr 1.6B — Stability AI's compact chat model.</summary>
    public static readonly ModelDefinition StableLM2_16BChat = new()
    {
        Id = "stablelm-2-1.6b-chat",
        DisplayName = "StableLM-2-1.6B-Chat",
        HuggingFaceRepoId = "stabilityai/stablelm-2-zephyr-1_6b",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML,
        Tier = ModelTier.Tiny,
        HasNativeOnnx = false
    };

    // ────────────────────────────────────────────────────────
    // 🟢 Small tier — best quality-to-size ratio
    // ────────────────────────────────────────────────────────

    /// <summary>Phi-3.5 mini instruct — recommended starting point (small).</summary>
    public static readonly ModelDefinition Phi35MiniInstruct = new()
    {
        Id = "phi-3.5-mini-instruct",
        DisplayName = "Phi-3.5 mini instruct",
        HuggingFaceRepoId = "microsoft/Phi-3.5-mini-instruct-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Small,
        HasNativeOnnx = true
    };

    /// <summary>Qwen2.5-3B-Instruct — small Qwen model, good balance.</summary>
    public static readonly ModelDefinition Qwen25_3BInstruct = new()
    {
        Id = "qwen2.5-3b-instruct",
        DisplayName = "Qwen2.5-3B-Instruct",
        HuggingFaceRepoId = "elbruno/Qwen2.5-3B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Small,
        HasNativeOnnx = true
    };

    /// <summary>Llama 3.2 3B Instruct — Meta's compact instruct model.</summary>
    public static readonly ModelDefinition Llama32_3BInstruct = new()
    {
        Id = "llama-3.2-3b-instruct",
        DisplayName = "Llama-3.2-3B-Instruct",
        HuggingFaceRepoId = "meta-llama/Llama-3.2-3B-Instruct",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Llama3,
        Tier = ModelTier.Small,
        HasNativeOnnx = false
    };

    /// <summary>Gemma 2 2B IT — Google's improved tiny model.</summary>
    public static readonly ModelDefinition Gemma2_2BIT = new()
    {
        Id = "gemma-2-2b-it",
        DisplayName = "Gemma-2-2B-IT",
        HuggingFaceRepoId = "google/gemma-2-2b-it",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Gemma,
        Tier = ModelTier.Small,
        HasNativeOnnx = false
    };

    // ────────────────────────────────────────────────────────
    // 🟡 Medium tier — production-quality local inference
    // ────────────────────────────────────────────────────────

    /// <summary>Qwen2.5-7B-Instruct — mid-size Qwen model.</summary>
    public static readonly ModelDefinition Qwen25_7BInstruct = new()
    {
        Id = "qwen2.5-7b-instruct",
        DisplayName = "Qwen2.5-7B-Instruct",
        HuggingFaceRepoId = "elbruno/Qwen2.5-7B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>Llama 3.1 8B Instruct — Meta's mainstream instruct model.</summary>
    public static readonly ModelDefinition Llama31_8BInstruct = new()
    {
        Id = "llama-3.1-8b-instruct",
        DisplayName = "Llama-3.1-8B-Instruct",
        HuggingFaceRepoId = "elbruno/Llama-3.1-8B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Llama3,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>Mistral 7B Instruct v0.3 — Mistral AI's flagship 7B.</summary>
    public static readonly ModelDefinition Mistral7BInstructV03 = new()
    {
        Id = "mistral-7b-instruct-v0.3",
        DisplayName = "Mistral-7B-Instruct-v0.3",
        HuggingFaceRepoId = "elbruno/Mistral-7B-Instruct-v0.3-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Mistral,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>Gemma 2 9B IT — Google's mid-size instruct model.</summary>
    public static readonly ModelDefinition Gemma2_9BIT = new()
    {
        Id = "gemma-2-9b-it",
        DisplayName = "Gemma-2-9B-IT",
        HuggingFaceRepoId = "google/gemma-2-9b-it",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Gemma,
        Tier = ModelTier.Medium,
        HasNativeOnnx = false
    };

    /// <summary>Phi-4 — Microsoft's medium-sized production model.</summary>
    public static readonly ModelDefinition Phi4 = new()
    {
        Id = "phi-4",
        DisplayName = "Phi-4",
        HuggingFaceRepoId = "microsoft/phi-4-onnx",
        RequiredFiles = ["cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Phi3,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>DeepSeek-R1-Distill-Qwen-14B — reasoning-focused distilled model.</summary>
    public static readonly ModelDefinition DeepSeekR1DistillQwen14B = new()
    {
        Id = "deepseek-r1-distill-qwen-14b",
        DisplayName = "DeepSeek-R1-Distill-Qwen-14B",
        HuggingFaceRepoId = "elbruno/DeepSeek-R1-Distill-Qwen-14B-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.DeepSeek,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    /// <summary>Mistral Small 24B Instruct — Mistral AI's mid-large model.</summary>
    public static readonly ModelDefinition MistralSmall24BInstruct = new()
    {
        Id = "mistral-small-24b-instruct",
        DisplayName = "Mistral-Small-24B-Instruct",
        HuggingFaceRepoId = "elbruno/Mistral-Small-24B-Instruct-onnx",
        RequiredFiles = ["*"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Mistral,
        Tier = ModelTier.Medium,
        HasNativeOnnx = true
    };

    // ────────────────────────────────────────────────────────
    // 🔴 Large tier — heavy workloads, multi-GPU
    // ────────────────────────────────────────────────────────

    /// <summary>Qwen2.5-14B-Instruct — large Qwen model.</summary>
    public static readonly ModelDefinition Qwen25_14BInstruct = new()
    {
        Id = "qwen2.5-14b-instruct",
        DisplayName = "Qwen2.5-14B-Instruct",
        HuggingFaceRepoId = "Qwen/Qwen2.5-14B-Instruct",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>Qwen2.5-32B-Instruct — extra-large Qwen model.</summary>
    public static readonly ModelDefinition Qwen25_32BInstruct = new()
    {
        Id = "qwen2.5-32b-instruct",
        DisplayName = "Qwen2.5-32B-Instruct",
        HuggingFaceRepoId = "Qwen/Qwen2.5-32B-Instruct",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Qwen,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>Llama 3.3 70B Instruct — Meta's flagship large model.</summary>
    public static readonly ModelDefinition Llama33_70BInstruct = new()
    {
        Id = "llama-3.3-70b-instruct",
        DisplayName = "Llama-3.3-70B-Instruct",
        HuggingFaceRepoId = "meta-llama/Llama-3.3-70B-Instruct",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Llama3,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>Mixtral 8x7B Instruct — Mistral AI's MoE model.</summary>
    public static readonly ModelDefinition Mixtral8x7BInstructV01 = new()
    {
        Id = "mixtral-8x7b-instruct-v0.1",
        DisplayName = "Mixtral-8x7B-Instruct-v0.1",
        HuggingFaceRepoId = "mistralai/Mixtral-8x7B-Instruct-v0.1",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.Mistral,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>DeepSeek-R1-Distill-Llama-70B — large reasoning model.</summary>
    public static readonly ModelDefinition DeepSeekR1DistillLlama70B = new()
    {
        Id = "deepseek-r1-distill-llama-70b",
        DisplayName = "DeepSeek-R1-Distill-Llama-70B",
        HuggingFaceRepoId = "deepseek-ai/DeepSeek-R1-Distill-Llama-70B",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.DeepSeek,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>Command-R 35B — Cohere's large command model.</summary>
    public static readonly ModelDefinition CommandR35B = new()
    {
        Id = "command-r-35b",
        DisplayName = "Command-R (35B)",
        HuggingFaceRepoId = "CohereForAI/c4ai-command-r-v01",
        RequiredFiles = ["onnx/model.onnx", "onnx/model.onnx.data"],
        ModelType = OnnxModelType.GenAI,
        ChatTemplate = ChatTemplateFormat.ChatML,
        Tier = ModelTier.Large,
        HasNativeOnnx = false
    };

    /// <summary>
    /// Returns all known model definitions.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> All { get; } =
    [
        // Tiny
        TinyLlama11BChat,
        SmolLM2_17BInstruct,
        Qwen25_05BInstruct,
        Qwen25_15BInstruct,
        Gemma2BIT,
        StableLM2_16BChat,
        // Small
        Phi35MiniInstruct,
        Qwen25_3BInstruct,
        Llama32_3BInstruct,
        Gemma2_2BIT,
        // Medium
        Qwen25_7BInstruct,
        Llama31_8BInstruct,
        Mistral7BInstructV03,
        Gemma2_9BIT,
        Phi4,
        DeepSeekR1DistillQwen14B,
        MistralSmall24BInstruct,
        // Large
        Qwen25_14BInstruct,
        Qwen25_32BInstruct,
        Llama33_70BInstruct,
        Mixtral8x7BInstructV01,
        DeepSeekR1DistillLlama70B,
        CommandR35B,
    ];

    /// <summary>
    /// Finds a model by its ID string. Returns null if not found.
    /// </summary>
    public static ModelDefinition? FindById(string modelId) =>
        All.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
}
