namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// The outcome of a choice question: one selected label plus the full probability distribution.
/// </summary>
/// <param name="Choice">The highest-probability option label. Always a key of <paramref name="Probabilities"/>.</param>
/// <param name="Probabilities">Probability assigned to every option label, keyed exactly as supplied.</param>
/// <param name="Confidence">
/// The model's self-reported confidence. Treat as uncalibrated unless you have validated it
/// on your own labelled data; see <see cref="DecisionOptions.DecisionThreshold"/>.
/// </param>
/// <param name="CalibrationClamped">
/// <see langword="true"/> when the checkpoint's fitted temperature for this question's bucket was
/// outside its own valid range and had to be clamped, so <paramref name="Confidence"/> and
/// <paramref name="Probabilities"/> are not the checkpoint's fitted calibration. See
/// <see href="https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/decisions-guide.md">the guide</see>.
/// </param>
public sealed record ChoiceResult(
    string Choice,
    IReadOnlyDictionary<string, double> Probabilities,
    double Confidence,
    bool CalibrationClamped = false)
{
    /// <summary>
    /// Gets the probability assigned to the selected <see cref="Choice"/>.
    /// </summary>
    public double Probability => Probabilities[Choice];

    /// <summary>
    /// Returns the selected choice only when its probability meets <paramref name="minimumProbability"/>,
    /// and <c>null</c> otherwise. Use this to route ambiguous inputs to a fallback or to a larger model.
    /// </summary>
    /// <param name="minimumProbability">The inclusive probability floor, between 0 and 1.</param>
    /// <returns>The selected label, or <c>null</c> when the distribution is too flat to act on.</returns>
    public string? ChoiceOrNull(double minimumProbability) =>
        Probability >= minimumProbability ? Choice : null;
}

/// <summary>
/// The outcome of a score question against an ordered rubric.
/// </summary>
/// <param name="Score">
/// The expected value over the rubric levels, between <c>0</c> and <c>Legend.Count - 1</c>.
/// This is a continuous number, not a level index; a score of <c>1.6</c> sits between levels 1 and 2.
/// </param>
/// <param name="Probabilities">Probability of each zero-based rubric level.</param>
/// <param name="Legend">The rubric levels in their supplied order, echoed by the model.</param>
/// <param name="Confidence">The model's self-reported confidence. Treat as uncalibrated until validated.</param>
/// <param name="CalibrationClamped">
/// <see langword="true"/> when the checkpoint's fitted temperature for this question's bucket was
/// outside its own valid range and had to be clamped.
/// </param>
public sealed record ScoreResult(
    double Score,
    IReadOnlyDictionary<int, double> Probabilities,
    IReadOnlyList<string> Legend,
    double Confidence,
    bool CalibrationClamped = false)
{
    /// <summary>
    /// Gets the zero-based index of the single most likely rubric level.
    /// This can differ from rounding <see cref="Score"/> when the distribution is skewed or bimodal.
    /// </summary>
    public int MostLikelyLevel => Probabilities.OrderByDescending(p => p.Value).First().Key;

    /// <summary>
    /// Gets the rubric text of <see cref="MostLikelyLevel"/>.
    /// </summary>
    public string MostLikelyLabel => Legend[MostLikelyLevel];

    /// <summary>
    /// Gets the probability of <see cref="MostLikelyLevel"/>.
    /// </summary>
    public double Probability => Probabilities[MostLikelyLevel];
}

/// <summary>
/// The outcome of a yes/no proposition, expressed as a probability rather than a boolean.
/// </summary>
/// <param name="Probability">The probability that the proposition is true, between 0 and 1.</param>
/// <param name="Threshold">The threshold applied by <see cref="IsTrue"/>.</param>
/// <param name="CalibrationClamped">
/// <see langword="true"/> when the checkpoint's fitted temperature for this question's bucket was
/// outside its own valid range and had to be clamped.
/// </param>
public readonly record struct ProbabilityResult(
    double Probability,
    double Threshold,
    bool CalibrationClamped = false)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Probability"/> meets <see cref="Threshold"/>.
    /// </summary>
    /// <remarks>
    /// The default threshold of 0.5 is a starting point, not a calibrated decision boundary.
    /// Fit it against your own labelled examples before relying on it in production.
    /// </remarks>
    public bool IsTrue => Probability >= Threshold;

    /// <summary>
    /// Applies a different threshold to the same probability without re-running the model.
    /// </summary>
    /// <param name="threshold">The inclusive probability floor, between 0 and 1.</param>
    /// <returns><see langword="true"/> when the probability meets the supplied threshold.</returns>
    public bool AtThreshold(double threshold) => Probability >= threshold;
}
