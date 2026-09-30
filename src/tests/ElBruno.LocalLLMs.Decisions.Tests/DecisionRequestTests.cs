using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class DecisionRequestTests
{
    [Fact]
    public void Constructor_RejectsEmptyState()
    {
        Assert.Throws<ArgumentException>(() => new DecisionRequest("  "));
        Assert.Throws<ArgumentException>(() => new DecisionRequest(string.Empty));
        Assert.Throws<ArgumentNullException>(() => new DecisionRequest(null!));
    }

    [Fact]
    public void Builders_AreImmutable()
    {
        var original = new DecisionRequest("text");
        DecisionRequest withQuestion = original.Ask("q", "proposition");

        Assert.Empty(original.Questions);
        Assert.Single(withQuestion.Questions);
    }

    [Fact]
    public void DuplicateQuestionName_IsRejected()
    {
        DecisionRequest request = new DecisionRequest("text").Ask("q", "proposition");

        Assert.Throws<ArgumentException>(() => request.Ask("q", "another"));
        Assert.Throws<ArgumentException>(() => request.Choose("q", new[] { "a", "b" }));
    }

    [Fact]
    public void Choose_RequiresAtLeastOneOption()
    {
        var request = new DecisionRequest("text");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => request.Choose("q", Array.Empty<string>()));
    }

    [Fact]
    public void Choose_RejectsMoreThan255Options()
    {
        var request = new DecisionRequest("text");
        string[] options = Enumerable.Range(0, 256).Select(i => $"option{i}").ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => request.Choose("q", options));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void Score_RejectsRubricOutsideTwoToTen(int levelCount)
    {
        var request = new DecisionRequest("text");
        string[] levels = Enumerable.Range(0, levelCount).Select(i => $"level{i}").ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => request.Score("q", levels));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void Score_AcceptsRubricBoundaries(int levelCount)
    {
        var request = new DecisionRequest("text");
        string[] levels = Enumerable.Range(0, levelCount).Select(i => $"level{i}").ToArray();

        DecisionRequest result = request.Score("q", levels);

        Assert.Equal(levelCount, result.Questions["q"].Levels!.Count);
    }

    [Fact]
    public void Ask_RejectsEmptyProposition()
    {
        var request = new DecisionRequest("text");

        Assert.Throws<ArgumentException>(() => request.Ask("q", "   "));
    }

    [Fact]
    public void Questions_RecordTheirType()
    {
        DecisionRequest request = new DecisionRequest("text")
            .Choose("a", new[] { "x", "y" })
            .Score("b", new[] { "low", "high" })
            .Ask("c", "proposition");

        Assert.Equal(DecisionQuestionType.Choice, request.Questions["a"].Type);
        Assert.Equal(DecisionQuestionType.Score, request.Questions["b"].Type);
        Assert.Equal(DecisionQuestionType.Probability, request.Questions["c"].Type);
    }

    [Fact]
    public void Choose_SnapshotsCallerDictionary()
    {
        var options = new Dictionary<string, string?> { ["a"] = "first" };
        DecisionRequest request = new DecisionRequest("text").Choose("q", options);

        options["b"] = "added later";

        Assert.Single(request.Questions["q"].Options!);
    }
}
