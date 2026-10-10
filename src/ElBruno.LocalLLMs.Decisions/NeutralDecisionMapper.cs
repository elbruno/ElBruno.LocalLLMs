namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Converts Laya's rich results into the provider-neutral <c>ElBruno.AI.Decisions</c> types.
/// </summary>
internal static class NeutralDecisionMapper
{
    internal static ChoiceDecision ToDecision(ChoiceResult result) =>
        new(result.Choice, ClampDistribution(result.Probabilities), Clamp(result.Confidence));

    internal static ScoreDecision ToDecision(ScoreResult result) =>
        new(result.Probabilities.OrderBy(pair => pair.Key).Select(pair => Clamp(pair.Value)).ToArray());

    internal static AssessmentDecision ToDecision(ProbabilityResult result) =>
        new(Clamp(result.Probability));

    // Softmax output can land a few ulps outside [0,1]; the shared types reject that outright.
    private static double Clamp(double value) => Math.Clamp(value, 0.0, 1.0);

    private static Dictionary<string, double> ClampDistribution(IReadOnlyDictionary<string, double> probabilities) =>
        probabilities.ToDictionary(pair => pair.Key, pair => Clamp(pair.Value), StringComparer.Ordinal);
}