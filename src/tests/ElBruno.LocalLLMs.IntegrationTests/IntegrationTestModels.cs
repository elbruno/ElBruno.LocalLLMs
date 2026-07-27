using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Central repository of xUnit <see cref="TheoryData"/> sets used by the integration lifecycle tests.
/// Models are classified into groups based on <see cref="ModelDefinition.HasNativeOnnx"/>,
/// <see cref="ModelDefinition.ModelType"/>, and <see cref="ModelDefinition.SupportsToolCalling"/>.
/// </summary>
public static class IntegrationTestModels
{
    // ──────────────────────────────────────────────────────────────────────────
    // Model size estimates (approximate GB, INT4 quantized)
    // Models above the threshold are excluded from automated lifecycle tests
    // because they require too much disk space / bandwidth for CI or dev machines.
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Model IDs estimated to require more than 10 GB of storage (INT4 quantized).
    /// These models are excluded from per-model lifecycle download tests.
    /// </summary>
    public static readonly HashSet<string> LargeModelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        KnownModels.MistralSmall24BInstruct.Id,   // 24B → ~12 GB
        KnownModels.Qwen25_32BInstruct.Id,        // 32B → ~16 GB
        KnownModels.Llama33_70BInstruct.Id,       // 70B → ~35 GB
        KnownModels.MagenticBrain.Id,             // 14B INT4, confirmed 11 GB
        KnownModels.Mixtral8x7BInstructV01.Id,    // 47B → ~24 GB (also HasNativeOnnx=false)
        KnownModels.DeepSeekR1DistillLlama70B.Id, // 70B → ~35 GB (also HasNativeOnnx=false)
        KnownModels.CommandR35B.Id,               // 35B → ~18 GB (also HasNativeOnnx=false)
    };

    /// <summary>
    /// Model IDs excluded from lifecycle tests due to known model export issues that
    /// prevent successful loading with the current OnnxRuntimeGenAI version.
    ///
    /// Fara1.5-9B uses the <c>qwen3_5</c> architecture with <c>inputs_embeds</c> as the
    /// primary input tensor (no <c>input_ids</c>) and has no vision processor config.
    /// This combination is not loadable via OnnxVisionModel in ORT-GenAI 0.14.1.
    /// Re-export the model with proper vision processor files before re-enabling.
    /// </summary>
    public static readonly HashSet<string> KnownExportIssueModelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        KnownModels.Fara15_9B.Id, // qwen3_5 + inputs_embeds only, no processor_config.json
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Group A — Native ONNX text (GenAI) models: auto-download supported
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// All <see cref="OnnxModelType.GenAI"/> models with <see cref="ModelDefinition.HasNativeOnnx"/> = true
    /// and estimated storage &lt; 10 GB.
    /// These support the full download → cache hit → delete lifecycle.
    /// </summary>
    public static TheoryData<ModelDefinition> PracticalTextModels { get; } = Build(
        KnownModels.All
            .Where(m => m.ModelType == OnnxModelType.GenAI
                     && m.HasNativeOnnx
                     && !LargeModelIds.Contains(m.Id)));

    /// <summary>
    /// All native ONNX GenAI models including large ones (>10 GB).
    /// Use only when disk space is not a concern.
    /// </summary>
    public static TheoryData<ModelDefinition> AllNativeOnnxTextModels { get; } = Build(
        KnownModels.All
            .Where(m => m.ModelType == OnnxModelType.GenAI && m.HasNativeOnnx));

    // ──────────────────────────────────────────────────────────────────────────
    // Group C — Tool-calling subset
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Practical (&lt;10 GB) native ONNX GenAI models that also support tool calling.
    /// </summary>
    public static TheoryData<ModelDefinition> PracticalToolCallingModels { get; } = Build(
        KnownModels.All
            .Where(m => m.ModelType == OnnxModelType.GenAI
                     && m.HasNativeOnnx
                     && m.SupportsToolCalling
                     && !LargeModelIds.Contains(m.Id)));

    // ──────────────────────────────────────────────────────────────────────────
    // Group — Vision models
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// All <see cref="OnnxModelType.VisionGenAI"/> models with <see cref="ModelDefinition.HasNativeOnnx"/> = true,
    /// excluding models with known export issues (<see cref="KnownExportIssueModelIds"/>).
    /// Currently Fara1.5-9B is excluded because its qwen3_5 architecture uses <c>inputs_embeds</c>
    /// input only (no <c>input_ids</c>) and has no processor_config.json, which prevents loading
    /// via OnnxVisionModel in ORT-GenAI 0.14.1.
    /// </summary>
    public static TheoryData<ModelDefinition> NativeOnnxVisionModels { get; } = Build(
        KnownModels.All
            .Where(m => m.ModelType == OnnxModelType.VisionGenAI
                     && m.HasNativeOnnx
                     && !KnownExportIssueModelIds.Contains(m.Id)));

    // ──────────────────────────────────────────────────────────────────────────
    // Group B — Non-native ONNX models: require user-provided ModelPath
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Models with <see cref="ModelDefinition.HasNativeOnnx"/> = false.
    /// These can be tested for HuggingFace reachability, but full lifecycle tests
    /// require the user to set the <c>MODEL_PATH_{sanitized-id}</c> environment variable.
    /// </summary>
    public static TheoryData<ModelDefinition> NonNativeOnnxModels { get; } = Build(
        KnownModels.All.Where(m => !m.HasNativeOnnx));

    // ──────────────────────────────────────────────────────────────────────────
    // All 35 models — for reporter completeness
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>All 35 supported models.</summary>
    public static TheoryData<ModelDefinition> AllModels { get; } = Build(KnownModels.All);

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static TheoryData<ModelDefinition> Build(IEnumerable<ModelDefinition> models)
    {
        var data = new TheoryData<ModelDefinition>();
        foreach (var m in models)
            data.Add(m);
        return data;
    }
}
