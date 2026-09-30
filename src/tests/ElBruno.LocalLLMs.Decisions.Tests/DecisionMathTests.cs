using ElBruno.LocalLLMs.Decisions.Internal;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class DecisionMathTests
{
    [Fact]
    public void Softmax_ProducesDistributionSummingToOne()
    {
        double[] probabilities = DecisionMath.Softmax([1.0f, 2.0f, 3.0f], 1.0);

        Assert.Equal(1.0, probabilities.Sum(), 10);
        Assert.All(probabilities, p => Assert.InRange(p, 0.0, 1.0));
    }

    [Fact]
    public void Softmax_MatchesLayaPublishedExample()
    {
        // The logits and temperature published on the English checkpoint's model card, which
        // reports 0.9696 / 0.0152 / 0.0152 for billing, technical and sales.
        double[] probabilities = DecisionMath.Softmax([4.2919f, -3.0214f, -3.0209f], 1.760152);

        Assert.Equal(0.9696, probabilities[0], 4);
        Assert.Equal(0.0152, probabilities[1], 4);
        Assert.Equal(0.0152, probabilities[2], 4);
    }

    [Fact]
    public void Softmax_LowerTemperatureSharpensDistribution()
    {
        double[] warm = DecisionMath.Softmax([2.0f, 1.0f], 2.0);
        double[] cold = DecisionMath.Softmax([2.0f, 1.0f], 0.5);

        Assert.True(cold[0] > warm[0]);
    }

    [Fact]
    public void Softmax_SurvivesLogitsThatWouldOverflowWithoutShifting()
    {
        // A large logit divided by a small temperature overflows exp() unless the maximum is
        // subtracted first, which would produce NaN rather than a distribution.
        double[] probabilities = DecisionMath.Softmax([900.0f, 1.0f], 0.5);

        Assert.Equal(1.0, probabilities[0], 10);
        Assert.Equal(0.0, probabilities[1], 10);
    }

    [Fact]
    public void Softmax_FallsBackToUniformWhenNormalizationFails()
    {
        double[] probabilities = DecisionMath.Softmax([float.NaN, float.NaN], 1.0);

        Assert.Equal(0.5, probabilities[0]);
        Assert.Equal(0.5, probabilities[1]);
    }

    [Fact]
    public void AnswerConfidence_ReturnsLargestProbability()
    {
        Assert.Equal(0.7, DecisionMath.AnswerConfidence([0.7, 0.2, 0.1]), 10);
    }

    [Fact]
    public void EntropyConfidence_IsZeroForUniformAndOneForCertain()
    {
        Assert.Equal(0.0, DecisionMath.EntropyConfidence([0.25, 0.25, 0.25, 0.25]), 6);
        Assert.Equal(1.0, DecisionMath.EntropyConfidence([1.0, 0.0, 0.0, 0.0]), 6);
    }

    [Fact]
    public void EntropyConfidence_ReturnsOneForSingleOption()
    {
        Assert.Equal(1.0, DecisionMath.EntropyConfidence([1.0]));
    }

    [Fact]
    public void EntropyConfidence_DiffersFromAnswerConfidence()
    {
        // The two numbers measure different things, so they must not be compared against the
        // same threshold.
        double[] probabilities = [0.6, 0.2, 0.2];

        Assert.NotEqual(
            DecisionMath.AnswerConfidence(probabilities),
            DecisionMath.EntropyConfidence(probabilities),
            2);
    }

    [Fact]
    public void ExpectedScore_WeightsLevelsByProbability()
    {
        Assert.Equal(1.5, DecisionMath.ExpectedScore([0.0, 0.5, 0.5]), 10);
    }

    [Fact]
    public void ExpectedScore_CanLandOnALevelTheModelNeverFavoured()
    {
        // A bimodal distribution over levels 0 and 2 reports level 1, which is exactly why
        // ScoreResult also exposes MostLikelyLevel.
        Assert.Equal(1.0, DecisionMath.ExpectedScore([0.5, 0.0, 0.5]), 10);
    }
}
