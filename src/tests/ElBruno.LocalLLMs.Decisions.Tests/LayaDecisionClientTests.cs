using System.Text.Json;
using ElBruno.AI.Jev;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class LayaDecisionClientTests
{
    private static LayaDecisionClient Client(
        out StubJevClient stub,
        Action<DecisionOptions>? configure = null)
    {
        stub = new StubJevClient(StubJevClient.LayaLike);
        var options = new DecisionOptions();
        configure?.Invoke(options);
        return new LayaDecisionClient(stub, options);
    }

    [Fact]
    public async Task ChooseAsync_ReturnsSelectedLabelAndFullDistribution()
    {
        var client = Client(out _);

        ChoiceResult result = await client.ChooseAsync(
            "My invoice charged me twice.",
            new Dictionary<string, string?>
            {
                ["billing"] = "Payment problems",
                ["technical"] = "Bugs and outages",
                ["sales"] = "Pricing questions"
            });

        Assert.Equal("billing", result.Choice);
        Assert.Equal(3, result.Probabilities.Count);
        Assert.Equal(0.9824, result.Probability, 4);
        Assert.Equal(0.9085, result.Confidence, 4);
    }

    [Fact]
    public async Task ChooseAsync_WithoutDescriptions_SendsAllLabels()
    {
        var client = Client(out StubJevClient stub);

        await client.ChooseAsync("text", new[] { "yes", "no", "maybe" });

        var question = Assert.IsType<JevChoiceQuestion>(stub.LastRequest!.Questions["result"]);
        Assert.Equal(3, question.Criteria.Count);
        Assert.Contains("maybe", question.Criteria.Keys);
    }

    [Fact]
    public async Task ChoiceOrNull_GatesOnProbability()
    {
        var client = Client(out _);

        ChoiceResult result = await client.ChooseAsync("text", new[] { "a", "b" });

        Assert.Equal("a", result.ChoiceOrNull(0.9));
        Assert.Null(result.ChoiceOrNull(0.99));
    }

    [Fact]
    public async Task ScoreAsync_MapsLegendAndLevelsByIndex()
    {
        var client = Client(out _);
        string[] levels = { "not urgent", "somewhat urgent", "urgent", "critical" };

        ScoreResult result = await client.ScoreAsync("text", levels);

        Assert.Equal(4, result.Probabilities.Count);
        Assert.Equal(levels, result.Legend);
        Assert.Equal(2, result.MostLikelyLevel);
        Assert.Equal("urgent", result.MostLikelyLabel);
        Assert.Equal(0.7, result.Probability, 4);
    }

    [Fact]
    public async Task ScoreAsync_PreservesFractionalScore()
    {
        var client = Client(out _);

        ScoreResult result = await client.ScoreAsync("text", new[] { "low", "mid", "high" });

        // The stub returns count - 2, proving the value is passed through rather than rounded.
        Assert.Equal(1.0, result.Score, 4);
    }

    [Fact]
    public async Task AskAsync_ReturnsProbabilityAndAppliesDefaultThreshold()
    {
        var client = Client(out _);

        ProbabilityResult result = await client.AskAsync("text", "Is this a refund request?");

        Assert.Equal(0.7321, result.Probability, 4);
        Assert.Equal(0.5, result.Threshold);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public async Task AskAsync_HonoursConfiguredThreshold()
    {
        var client = Client(out _, options => options.DecisionThreshold = 0.8);

        ProbabilityResult result = await client.AskAsync("text", "proposition");

        Assert.False(result.IsTrue);
        Assert.True(result.AtThreshold(0.7));
    }

    [Fact]
    public async Task EvaluateAsync_AnswersEveryQuestionInOneCall()
    {
        var client = Client(out StubJevClient stub);

        var request = new DecisionRequest("My invoice charged me twice and I want a refund.")
            .Choose("team", new[] { "billing", "technical" }, "Which team?")
            .Score("urgency", new[] { "low", "medium", "high" }, "How urgent?")
            .Ask("refund", "Is the customer asking for a refund?");

        DecisionResult result = await client.EvaluateAsync(request);

        Assert.Equal(1, stub.CallCount);
        Assert.Equal(3, result.Names.Count);
        Assert.Equal("billing", result.Choice("team").Choice);
        Assert.Equal(1.0, result.Score("urgency").Score, 4);
        Assert.True(result.Probability("refund").IsTrue);
        Assert.Equal("laya-rl-agent", result.Model);
        Assert.Equal(166, result.InputTokens);
    }

    [Fact]
    public async Task EvaluateAsync_OmitsModelWhenNotConfigured()
    {
        var client = Client(out StubJevClient stub);

        await client.AskAsync("text", "proposition");

        Assert.Null(stub.LastRequest!.Model);
    }

    [Fact]
    public async Task EvaluateAsync_ForwardsConfiguredModel()
    {
        var client = Client(out StubJevClient stub, options => options.Model = "english");

        await client.AskAsync("text", "proposition");

        Assert.Equal("english", stub.LastRequest!.Model);
    }

    [Fact]
    public async Task EvaluateAsync_MapsQuestionTypesOntoJevQuestions()
    {
        var client = Client(out StubJevClient stub);

        var request = new DecisionRequest("text")
            .Choose("a", new[] { "x", "y" })
            .Score("b", new[] { "low", "high" })
            .Ask("c", "proposition");

        await client.EvaluateAsync(request);

        Assert.IsType<JevChoiceQuestion>(stub.LastRequest!.Questions["a"]);
        Assert.IsType<JevScoreQuestion>(stub.LastRequest.Questions["b"]);
        Assert.IsType<JevNoulQuestion>(stub.LastRequest.Questions["c"]);
    }

    [Fact]
    public async Task EvaluateAsync_RejectsEmptyRequest()
    {
        var client = Client(out _);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.EvaluateAsync(new DecisionRequest("text")));
    }

    [Fact]
    public async Task Result_ReadingWrongAnswerTypeThrows()
    {
        var client = Client(out _);

        DecisionResult result = await client.EvaluateAsync(
            new DecisionRequest("text").Ask("q", "proposition"));

        Assert.Throws<InvalidOperationException>(() => result.Choice("q"));
    }

    [Fact]
    public async Task Result_ReadingUnknownNameThrowsWithAvailableNames()
    {
        var client = Client(out _);

        DecisionResult result = await client.EvaluateAsync(
            new DecisionRequest("text").Ask("q", "proposition"));

        var ex = Assert.Throws<KeyNotFoundException>(() => result.Probability("missing"));
        Assert.Contains("q", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsDecisionExceptionWhenAnswerIsMissing()
    {
        var stub = new StubJevClient(_ => new JevDecisionResponse(
            "laya-rl-agent",
            new Dictionary<string, JevAnswer>(StringComparer.Ordinal)));

        var client = new LayaDecisionClient(stub);

        var ex = await Assert.ThrowsAsync<DecisionException>(
            () => client.EvaluateAsync(new DecisionRequest("text").Ask("q", "proposition")));

        Assert.Contains("'q'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsDecisionExceptionOnAnswerTypeMismatch()
    {
        var stub = new StubJevClient(_ => new JevDecisionResponse(
            "laya-rl-agent",
            new Dictionary<string, JevAnswer>(StringComparer.Ordinal)
            {
                ["q"] = new JevNoulAnswer(0.5)
            }));

        var client = new LayaDecisionClient(stub);

        DecisionRequest request = new DecisionRequest("text").Choose("q", new[] { "a", "b" });

        var ex = await Assert.ThrowsAsync<DecisionException>(() => client.EvaluateAsync(request));
        Assert.Contains("choice", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsWhenLegendDoesNotCoverEveryLevel()
    {
        var stub = new StubJevClient(_ => new JevDecisionResponse(
            "laya-rl-agent",
            new Dictionary<string, JevAnswer>(StringComparer.Ordinal)
            {
                ["q"] = new JevScoreAnswer(
                    1.0,
                    new Dictionary<string, double> { ["0"] = 0.2, ["1"] = 0.5, ["2"] = 0.3 },
                    new Dictionary<string, JsonElement>
                    {
                        ["0"] = JsonDocument.Parse("\"low\"").RootElement.Clone()
                    },
                    0.5)
            }));

        var client = new LayaDecisionClient(stub);
        DecisionRequest request = new DecisionRequest("text")
            .Score("q", new[] { "low", "mid", "high" });

        var ex = await Assert.ThrowsAsync<DecisionException>(() => client.EvaluateAsync(request));
        Assert.Contains("legend", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispose_PreventsFurtherCalls()
    {
        var client = Client(out _);
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.EvaluateAsync(new DecisionRequest("text").Ask("q", "p")));
    }

    [Fact]
    public void Dispose_DoesNotDisposeCallerOwnedClient()
    {
        var stub = new StubJevClient(StubJevClient.LayaLike);
        var client = new LayaDecisionClient(stub);

        client.Dispose();
        client.Dispose();
    }
}
