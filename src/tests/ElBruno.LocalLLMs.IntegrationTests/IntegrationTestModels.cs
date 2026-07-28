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
    /// Model IDs excluded from per-model lifecycle download tests because they have no native
    /// ONNX package yet. These are non-native models that are also very large.
    /// See linked issues for ONNX conversion tasks.
    /// </summary>
    public static readonly HashSet<string> LargeModelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        // Non-native ONNX models that are also large — excluded until ONNX conversion is published
        KnownModels.Mixtral8x7BInstructV01.Id,    // 47B → ~24 GB (HasNativeOnnx=false, see issue #32)
        KnownModels.DeepSeekR1DistillLlama70B.Id, // 70B → ~35 GB (HasNativeOnnx=false, see issue #33)
        KnownModels.CommandR35B.Id,               // 35B → ~18 GB (HasNativeOnnx=false, see issue #34)
        // 70B native ONNX model — requires CUDA/GPU; CPU-only machines fail with DLL missing error
        KnownModels.Llama33_70BInstruct.Id,       // 70B → ~35 GB, requires GPU execution provider
    };

    /// <summary>
    /// Model IDs excluded from lifecycle tests due to known model export issues that
    /// prevent successful loading with the current OnnxRuntimeGenAI version.
    /// </summary>
    public static readonly HashSet<string> KnownExportIssueModelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        // Fara1.5-9B: processor_config.json was missing from elbruno/Fara1.5-9B-onnx (now added).
        // VisionGenAI path needs validation — remove this entry after a successful VisionLifecycleTest run.
        // GenAI/text path will never work (ONNX uses inputs_embeds not input_ids — correct for VLMs).
        // See GitHub issue #35 for history.
        KnownModels.Fara15_9B.Id,
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
