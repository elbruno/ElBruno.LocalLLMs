using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class NeutralDecisionMapperTests
{
    [Fact]
    public void Choice_ClampsProbabilitiesOutsideUnitInterval()
    {
        var rich = new ChoiceResult(
            "a",
            new Dictionary<string, double> { ["a"] = 1.0000000002, ["b"] = -1e-12 },
            1.0000000002);

        ChoiceDecision decision = NeutralDecisionMapper.ToDecision(rich);

        Assert.Equal("a", decision.Choice);
        Assert.Equal(1.0, decision.Probabilities["a"]);
        Assert.Equal(0.0, decision.Probabilities["b"]);
        Assert.Equal(1.0, decision.Confidence);
    }

    [Fact]
    public void Score_OrdersLevelsAndClamps()
    {
        var rich = new ScoreResult(
            1.5,
            new Dictionary<int, double> { [2] = 0.5, [1] = 0.5000000001 + 0.5, [3] = -1e-13 },
            new[] { "low", "mid", "high" },
            0.9);

        ScoreDecision decision = NeutralDecisionMapper.ToDecision(rich);

        Assert.Equal(3, decision.LevelProbabilities.Count);
        Assert.Equal(1.0, decision.LevelProbabilities[0]);
        Assert.Equal(0.5, decision.LevelProbabilities[1]);
        Assert.Equal(0.0, decision.LevelProbabilities[2]);
    }

    [Fact]
    public void Assessment_ClampsProbability()
    {
        AssessmentDecision decision = NeutralDecisionMapper.ToDecision(new ProbabilityResult(1.0000001, 0.5));

        Assert.Equal(1.0, decision.Probability);
    }
}

public class SharedInterfaceRegistrationTests
{
    [Fact]
    public void AddLocalDecisions_ResolvesSharedAndRichInterfacesToSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLocalDecisions();

        using ServiceProvider provider = services.BuildServiceProvider();

        var shared = provider.GetRequiredService<IDecisionClient>();
        Assert.Same(shared, provider.GetRequiredService<ILayaDecisionClient>());
        Assert.Same(shared, provider.GetRequiredService<LayaOnnxDecisionClient>());
    }

    [Fact]
    public void AddLocalDecisions_DoesNotReplaceAnExistingSharedProvider()
    {
        var existing = new StubClient();
        var services = new ServiceCollection();
        services.AddSingleton<IDecisionClient>(existing);
        services.AddLocalDecisions();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(existing, provider.GetRequiredService<IDecisionClient>());
        Assert.IsType<LayaOnnxDecisionClient>(provider.GetRequiredService<ILayaDecisionClient>());
    }

    [Fact]
    public async Task SharedInterface_ValidatesBeforeLoadingTheModel()
    {
        IDecisionClient client = new LayaOnnxDecisionClient(new DecisionOptions());
        var options = new Dictionary<string, string?> { ["only"] = null };

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.ChooseAsync("text", "pick", options, CancellationToken.None));
    }

    private sealed class StubClient : IDecisionClient
    {
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions, IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}