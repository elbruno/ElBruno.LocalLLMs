namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// The numeric steps that turn raw marker logits into the answers Laya reports.
/// </summary>
/// <remarks>
/// These run outside the ONNX graph because the option count varies per call. Keeping them here,
/// rather than baking them into the export, is also what lets the temperature be corrected
/// without re-exporting the model.
/// </remarks>
internal static class DecisionMath
{
    /// <summary>
    /// Divides logits by a temperature and normalizes them into a probability distribution.
    /// </summary>
    /// <param name="logits">The raw marker logits, one per option.</param>
    /// <param name="temperature">The fitted temperature for this question kind and size.</param>
    /// <returns>Probabilities summing to 1.</returns>
    public static double[] Softmax(ReadOnlySpan<float> logits, double temperature)
    {
        var probabilities = new double[logits.Length];
        if (logits.Length == 0)
        {
            return probabilities;
        }

        var maximum = double.NegativeInfinity;
        for (var i = 0; i < logits.Length; i++)
        {
            probabilities[i] = logits[i] / temperature;
            if (probabilities[i] > maximum)
            {
                maximum = probabilities[i];
            }
        }

        var total = 0.0;
        for (var i = 0; i < probabilities.Length; i++)
        {
            // Subtract the maximum first: the exported logits can exceed 80 once divided by a
            // temperature below 1, which overflows to infinity without this shift.
            probabilities[i] = Math.Exp(probabilities[i] - maximum);
            total += probabilities[i];
        }

        if (total <= 0 || double.IsNaN(total) || double.IsInfinity(total))
        {
            Array.Fill(probabilities, 1.0 / probabilities.Length);
            return probabilities;
        }

        for (var i = 0; i < probabilities.Length; i++)
        {
            probabilities[i] /= total;
        }

        return probabilities;
    }

    /// <summary>
    /// Returns the probability mass on the answer being reported, which is <c>max(p)</c>.
    /// </summary>
    /// <param name="probabilities">The distribution over options.</param>
    /// <returns>A value between 0 and 1.</returns>
    /// <remarks>
    /// This is the quantity temperature scaling fits, so it is the one to gate on. It still only
    /// carries its calibration guarantee on a checkpoint whose temperatures were fitted and
    /// validated for your option counts.
    /// </remarks>
    public static double AnswerConfidence(ReadOnlySpan<double> probabilities)
    {
        if (probabilities.Length < 1)
        {
            return 1.0;
        }

        var maximum = 0.0;
        foreach (var probability in probabilities)
        {
            if (probability > maximum)
            {
                maximum = probability;
            }
        }

        return Math.Clamp(maximum, 0.0, 1.0);
    }

    /// <summary>
    /// Returns normalized Shannon entropy confidence, <c>1 - H(p) / log(k)</c>.
    /// </summary>
    /// <param name="probabilities">The distribution over options.</param>
    /// <returns>A value between 0 and 1.</returns>
    /// <remarks>
    /// This measures how concentrated the whole distribution is. It is not what temperature
    /// scaling fits, so it must not be compared against the same threshold as
    /// <see cref="AnswerConfidence"/>.
    /// </remarks>
    public static double EntropyConfidence(ReadOnlySpan<double> probabilities)
    {
        if (probabilities.Length < 2)
        {
            return 1.0;
        }

        var entropy = 0.0;
        foreach (var probability in probabilities)
        {
            entropy -= probability * Math.Log(Math.Clamp(probability, 1e-12, 1.0));
        }

        return Math.Clamp(1.0 - (entropy / Math.Log(probabilities.Length)), 0.0, 1.0);
    }

    /// <summary>
    /// Returns the expected rubric level under the distribution.
    /// </summary>
    /// <param name="probabilities">The distribution over ordered levels.</param>
    /// <returns>A continuous value between 0 and <c>probabilities.Length - 1</c>.</returns>
    /// <remarks>
    /// This is an expectation, not an argmax, so a bimodal distribution over levels 0 and 2
    /// reports 1 — a level the model never actually favoured. Read
    /// <see cref="ScoreResult.MostLikelyLevel"/> when that distinction matters.
    /// </remarks>
    public static double ExpectedScore(ReadOnlySpan<double> probabilities)
    {
        var score = 0.0;
        for (var i = 0; i < probabilities.Length; i++)
        {
            score += i * probabilities[i];
        }

        return score;
    }
}
