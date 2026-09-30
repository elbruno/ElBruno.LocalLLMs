namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Configuration for a local decision client. The model runs in-process through ONNX Runtime,
/// so there is no server to start and no Python to install.
/// </summary>
public sealed class DecisionOptions
{
    /// <summary>
    /// Gets or sets the HuggingFace repository the model is downloaded from.
    /// Defaults to an ONNX export of Laya's English checkpoint.
    /// </summary>
    /// <remarks>
    /// Laya's authors publish PyTorch weights only, so every ONNX export of Laya is
    /// community-produced. The default mirrors the <c>inferenceprince/laya-onnx</c> export
    /// unmodified, and was verified to reproduce the numbers that export's model card reports.
    /// </remarks>
    public string ModelRepository { get; set; } = "elbruno/laya-onnx";

    /// <summary>
    /// Gets or sets a local directory holding the model files. When set, nothing is downloaded
    /// and <see cref="ModelRepository"/> is ignored.
    /// </summary>
    /// <remarks>
    /// The directory must contain the ONNX graph, the checkpoint config carrying the fitted
    /// temperatures, and the tokenizer definition.
    /// </remarks>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Gets or sets where downloaded models are cached. Defaults to a folder under the user's
    /// local application data.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets the number of threads ONNX Runtime uses within a single operator.
    /// Leave <c>null</c> to let the runtime decide.
    /// </summary>
    /// <remarks>
    /// Worth pinning to a small number when many requests are served concurrently, because the
    /// default fills every core for one call and the batches here are short.
    /// </remarks>
    public int? IntraOpNumThreads { get; set; }

    /// <summary>
    /// Gets or sets the probability at or above which <see cref="ProbabilityResult.IsTrue"/> reports true.
    /// Defaults to 0.5.
    /// </summary>
    /// <remarks>
    /// This is a convenience default, not a calibrated boundary. Laya's public checkpoints are
    /// over-confident, and one shipped temperature is invalid enough that this package clamps it.
    /// Fit this threshold against your own labelled examples before depending on the boolean.
    /// </remarks>
    public double DecisionThreshold { get; set; } = 0.5;

    /// <summary>
    /// Gets the repository files that must be present for the model to run.
    /// </summary>
    internal IReadOnlyList<string> ModelFiles { get; } =
    [
        "model.onnx",
        "rl_agent_config.json",
        "tokenizer/tokenizer.json",
    ];

    /// <summary>
    /// Gets repository files that are downloaded when present. The weights of an externally
    /// stored graph live here, as do the alternative layouts other exports use.
    /// </summary>
    internal IReadOnlyList<string> OptionalModelFiles { get; } =
    [
        "model.onnx.data",
        "model.onnx_data",
        "config.json",
        "tokenizer.json",
        "tokenizer/tokenizer_config.json",
    ];

    internal string ResolveCacheDirectory() =>
        CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalLLMs", "decisions");

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModelPath) && string.IsNullOrWhiteSpace(ModelRepository))
        {
            throw new InvalidOperationException(
                "DecisionOptions needs either a ModelRepository to download from or a ModelPath to load from.");
        }

        if (IntraOpNumThreads is <= 0)
        {
            throw new InvalidOperationException(
                $"DecisionOptions.IntraOpNumThreads must be greater than zero, but was {IntraOpNumThreads}.");
        }

        if (DecisionThreshold is < 0 or > 1 || double.IsNaN(DecisionThreshold))
        {
            throw new InvalidOperationException(
                $"DecisionOptions.DecisionThreshold must be between 0 and 1, but was {DecisionThreshold}.");
        }
    }
}
