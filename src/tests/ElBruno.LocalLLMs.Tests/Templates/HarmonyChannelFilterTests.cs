using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for <see cref="HarmonyChannelFilter"/>. The critical guarantee is that
/// GPT-OSS chain-of-thought (the <c>analysis</c> channel) never reaches the caller,
/// including when channel markers are split across streaming token boundaries.
/// </summary>
public class HarmonyChannelFilterTests
{
    private const string AnalysisThenFinal =
        "<|channel|>analysis<|message|>The user wants the capital. It is Paris.<|end|>" +
        "<|start|>assistant<|channel|>final<|message|>The capital of France is Paris.<|return|>";

    // ── Whole-text extraction ─────────────────────────────────────────────────

    [Fact]
    public void ExtractFinal_ReturnsOnlyFinalChannel()
    {
        var result = HarmonyChannelFilter.ExtractFinal(AnalysisThenFinal);

        Assert.Equal("The capital of France is Paris.", result);
    }

    [Fact]
    public void ExtractFinal_DropsChainOfThought()
    {
        var result = HarmonyChannelFilter.ExtractFinal(AnalysisThenFinal);

        Assert.DoesNotContain("The user wants the capital", result);
    }

    [Fact]
    public void ExtractFinal_LeavesNoControlMarkers()
    {
        var result = HarmonyChannelFilter.ExtractFinal(AnalysisThenFinal);

        Assert.DoesNotContain("<|", result);
    }

    [Fact]
    public void ExtractFinal_AnalysisOnly_ReturnsEmpty()
    {
        var result = HarmonyChannelFilter.ExtractFinal("<|channel|>analysis<|message|>thinking hard<|end|>");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractFinal_CommentaryToolCall_IsNotSurfaced()
    {
        const string toolCall =
            "<|channel|>analysis<|message|>I should call a tool.<|end|>" +
            "<|start|>assistant to=functions.get_weather<|channel|>commentary json<|message|>{\"city\":\"Paris\"}<|call|>";

        var result = HarmonyChannelFilter.ExtractFinal(toolCall);

        Assert.Equal(string.Empty, result);
        Assert.DoesNotContain("get_weather", result);
    }

    [Fact]
    public void ExtractFinal_MultipleFinalSegments_AreConcatenated()
    {
        const string text =
            "<|channel|>final<|message|>Part one. <|end|>" +
            "<|start|>assistant<|channel|>final<|message|>Part two.<|return|>";

        var result = HarmonyChannelFilter.ExtractFinal(text);

        Assert.Equal("Part one. Part two.", result);
    }

    [Fact]
    public void ExtractFinal_NoChannelMarkers_ReturnsTextUnchanged()
    {
        // Defensive: a stub or misconfigured model may emit plain text.
        var result = HarmonyChannelFilter.ExtractFinal("Just plain text.");

        Assert.Equal("Just plain text.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractFinal_NullOrEmpty_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, HarmonyChannelFilter.ExtractFinal(input));
    }

    [Fact]
    public void ExtractFinal_UnterminatedFinal_StillReturnsText()
    {
        // Generation truncated by a token limit before the terminator arrived.
        var result = HarmonyChannelFilter.ExtractFinal("<|channel|>final<|message|>Partial answer");

        Assert.Equal("Partial answer", result);
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    [Fact]
    public void Streaming_CharByChar_ProducesSameResultAsWholeText()
    {
        var filter = new HarmonyChannelFilter();
        var output = string.Concat(AnalysisThenFinal.Select(c => filter.Push(c.ToString()))) + filter.Flush();

        Assert.Equal("The capital of France is Paris.", output);
    }

    [Fact]
    public void Streaming_MarkerSplitAcrossChunks_IsNotLeaked()
    {
        var filter = new HarmonyChannelFilter();

        // Deliberately split "<|channel|>" and "<|message|>" mid-marker.
        var chunks = new[]
        {
            "<|chan", "nel|>analy", "sis<|mess", "age|>secret reasoning<|e", "nd|>",
            "<|start|>assistant<|chan", "nel|>fin", "al<|message|>Vis", "ible answer<|ret", "urn|>"
        };

        var output = string.Concat(chunks.Select(filter.Push)) + filter.Flush();

        Assert.Equal("Visible answer", output);
        Assert.DoesNotContain("secret reasoning", output);
        Assert.DoesNotContain("<|", output);
    }

    [Fact]
    public void Streaming_AnalysisTokens_YieldNothingBeforeFinalChannel()
    {
        var filter = new HarmonyChannelFilter();

        filter.Push("<|channel|>analysis<|message|>");
        var duringAnalysis = filter.Push("I am thinking about this problem");

        Assert.Equal(string.Empty, duringAnalysis);
    }

    [Fact]
    public void Streaming_TextResemblingMarkerStart_IsEventuallyEmitted()
    {
        var filter = new HarmonyChannelFilter();
        filter.Push("<|channel|>final<|message|>");

        // A literal '<' in the answer must not be swallowed permanently.
        var output = filter.Push("5 < 10 and 3 > 1") + filter.Flush();

        Assert.Equal("5 < 10 and 3 > 1", output);
    }

    [Fact]
    public void Streaming_Flush_IsIdempotentAfterCompletion()
    {
        var filter = new HarmonyChannelFilter();
        var first = filter.Push(AnalysisThenFinal) + filter.Flush();
        var second = filter.Flush();

        Assert.Equal("The capital of France is Paris.", first);
        Assert.Equal(string.Empty, second);
    }

    [Fact]
    public void Streaming_PushNull_IsSafe()
    {
        var filter = new HarmonyChannelFilter();

        Assert.Equal(string.Empty, filter.Push(null));
    }

    [Fact]
    public void Streaming_UnicodeInFinalChannel_IsPreserved()
    {
        var filter = new HarmonyChannelFilter();
        var output = filter.Push("<|channel|>final<|message|>Bonjour 🎉 你好<|return|>") + filter.Flush();

        Assert.Equal("Bonjour 🎉 你好", output);
    }
}
