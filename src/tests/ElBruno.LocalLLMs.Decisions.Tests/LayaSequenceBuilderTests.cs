using ElBruno.LocalLLMs.Decisions.Internal;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class LayaSequenceBuilderTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("laya-sequence-tests").FullName;

    private readonly LayaTokenizer _tokenizer;

    public LayaSequenceBuilderTests()
    {
        _tokenizer = SyntheticTokenizer.Create(_directory);
    }

    private LayaSequenceBuilder Builder(int maxLength = 512, int headMaxLength = 192)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""{"max_len": {{maxLength}}, "head_max_len": {{headMaxLength}}}""");
        return new LayaSequenceBuilder(_tokenizer, LayaRuntimeConfig.Load(path));
    }

    [Fact]
    public void Build_ProducesOneSequencePerQuestion()
    {
        var request = new DecisionRequest("a support ticket")
            .Choose("route", ["billing", "technical"])
            .Score("urgency", ["low", "high"])
            .Ask("angry", "the customer is angry");

        IReadOnlyList<LayaSequence> sequences = Builder().Build(request);

        Assert.Equal(3, sequences.Count);
        Assert.Equal(["route", "urgency", "angry"], sequences.Select(s => s.Name));
    }

    [Fact]
    public void Build_AssignsTheQuestionTypeTheModelExpects()
    {
        var request = new DecisionRequest("text")
            .Choose("a", ["x", "y"])
            .Score("b", ["low", "high"])
            .Ask("c", "something holds");

        IReadOnlyList<LayaSequence> sequences = Builder().Build(request);

        Assert.Equal(0, sequences[0].QuestionType);
        Assert.Equal(1, sequences[1].QuestionType);
        Assert.Equal(2, sequences[2].QuestionType);
    }

    [Fact]
    public void Build_WrapsTheSequenceInTheExpectedSpecialTokens()
    {
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Choose("route", ["billing", "technical"]))[0];

        Assert.Equal(SyntheticTokenizer.ClsId, sequence.TokenIds[0]);
        Assert.Equal(SyntheticTokenizer.SepId, sequence.TokenIds[^1]);
    }

    [Fact]
    public void Build_PlacesOneMaskMarkerPerOption()
    {
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Choose("route", ["billing", "technical", "sales"]))[0];

        Assert.Equal(3, sequence.MarkerPositions.Count);
        Assert.All(
            sequence.MarkerPositions,
            position => Assert.Equal(SyntheticTokenizer.MaskId, sequence.TokenIds[position]));
    }

    [Fact]
    public void Build_KeepsMarkersInTheOrderTheLabelsWereSupplied()
    {
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Choose("route", ["billing", "technical", "sales"]))[0];

        Assert.Equal(["billing", "technical", "sales"], sequence.Labels);
        Assert.Equal(
            sequence.MarkerPositions.OrderBy(position => position),
            sequence.MarkerPositions);
    }

    [Fact]
    public void Build_AlwaysRendersPropositionsAsFalseThenTrue()
    {
        // The decoder reads index 1 as the probability of true, so this order is load-bearing.
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Ask("angry", "the customer is angry"))[0];

        Assert.Equal(["false", "true"], sequence.Labels);
    }

    [Fact]
    public void Build_StripsLiteralMaskTokensFromCallerText()
    {
        // A [MASK] in caller text would create a phantom marker the head would try to score.
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("please [MASK] this").Choose("route", ["a", "b"]))[0];

        Assert.Equal(2, sequence.TokenIds.Count(id => id == SyntheticTokenizer.MaskId));
    }

    [Fact]
    public void Build_StripsLiteralMaskTokensFromInstructionsAndOptions()
    {
        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Choose(
                "route",
                new Dictionary<string, string?> { ["a"] = "[MASK] description", ["b"] = null },
                "where should [MASK] go?"))[0];

        Assert.Equal(2, sequence.TokenIds.Count(id => id == SyntheticTokenizer.MaskId));
    }

    [Fact]
    public void Build_NeverExceedsTheConfiguredWindow()
    {
        var state = string.Join(' ', Enumerable.Repeat("some fairly long supporting text", 400));

        LayaSequence sequence = Builder(maxLength: 256).Build(
            new DecisionRequest(state).Choose("route", ["billing", "technical"]))[0];

        Assert.True(sequence.TokenIds.Count <= 256);
        Assert.All(sequence.MarkerPositions, position => Assert.True(position < 256));
    }

    [Fact]
    public void Build_KeepsEveryOptionScorableWhenOptionsExceedTheirBudget()
    {
        // Long descriptions are shrunk evenly rather than dropped, so every label keeps a marker.
        var options = Enumerable.Range(0, 10).ToDictionary(
            i => $"option{i}",
            i => (string?)string.Join(' ', Enumerable.Repeat("a very long option description", 20)));

        LayaSequence sequence = Builder().Build(
            new DecisionRequest("text").Choose("route", options))[0];

        Assert.Equal(10, sequence.MarkerPositions.Count);
        Assert.Equal(10, sequence.Labels.Count);
    }

    [Fact]
    public void Build_ThrowsWhenOptionsCannotAllBeScored()
    {
        var options = Enumerable.Range(0, 60).ToDictionary(i => $"option{i}", _ => (string?)null);

        var exception = Assert.Throws<DecisionException>(() =>
            Builder(maxLength: 64, headMaxLength: 48).Build(
                new DecisionRequest("text").Choose("route", options)));

        Assert.Contains("fewer or shorter options", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReusesTheSameStateTokensForEveryQuestion()
    {
        var request = new DecisionRequest("a shared support ticket")
            .Choose("a", ["x", "y"])
            .Choose("b", ["x", "y"]);

        IReadOnlyList<LayaSequence> sequences = Builder().Build(request);

        Assert.Equal(sequences[0].TokenIds, sequences[1].TokenIds);
    }

    [Fact]
    public void Build_DistinguishesOptionsThatCarryDescriptions()
    {
        LayaSequence bare = Builder().Build(
            new DecisionRequest("text").Choose("route", ["billing", "technical"]))[0];

        LayaSequence described = Builder().Build(
            new DecisionRequest("text").Choose(
                "route",
                new Dictionary<string, string?>
                {
                    ["billing"] = "invoices and payments",
                    ["technical"] = "bugs and outages",
                }))[0];

        Assert.True(described.TokenIds.Count > bare.TokenIds.Count);
        Assert.Equal(bare.Labels, described.Labels);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
