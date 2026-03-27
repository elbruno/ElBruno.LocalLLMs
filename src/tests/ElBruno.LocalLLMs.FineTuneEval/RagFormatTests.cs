using System.Text.RegularExpressions;

namespace ElBruno.LocalLLMs.FineTuneEval;

/// <summary>
/// Validates RAG (Retrieval Augmented Generation) output format patterns.
/// Tests that expected RAG responses include citations, context formatting,
/// and appropriate "I don't know" responses — all critical for fine-tuning data quality.
/// </summary>
public class RagFormatTests
{
    // ──────────────────────────────────────────────
    // Citation format validation
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("Based on the documentation [1], Qwen2.5-0.5B is the smallest model.", new[] { "[1]" })]
    [InlineData("The library supports ONNX [1] and GenAI [2] formats.", new[] { "[1]", "[2]" })]
    [InlineData("Sources [1], [2], and [3] confirm this.", new[] { "[1]", "[2]", "[3]" })]
    public void GroundedAnswer_IncludesCitationMarkers(string answer, string[] expectedCitations)
    {
        foreach (var citation in expectedCitations)
        {
            Assert.Contains(citation, answer);
        }
    }

    [Fact]
    public void GroundedAnswer_CitationMarkersAreNumericBrackets()
    {
        var answer = "Based on the documentation [1], Qwen2.5-0.5B-Instruct is the smallest model with tool calling support [2].";

        var citationPattern = new Regex(@"\[\d+\]");
        var matches = citationPattern.Matches(answer);

        Assert.True(matches.Count >= 1, "Grounded answers should contain at least one [N] citation marker");
        Assert.Equal(2, matches.Count);
    }

    // ──────────────────────────────────────────────
    // Context injection format
    // ──────────────────────────────────────────────

    [Fact]
    public void ContextInjectionFormat_IsParseable()
    {
        // The RAG context format from the plan: numbered sources in the user message
        var userMessage = """
            Context:
            [1] ElBruno.LocalLLMs supports 29 models across 5 tiers.
            [2] Qwen2.5-0.5B-Instruct is the smallest model with tool calling support.

            Question: Which is the smallest tool-calling model?
            """;

        // Parse the context entries
        var contextPattern = new Regex(@"\[(\d+)\]\s+(.+)");
        var matches = contextPattern.Matches(userMessage);

        Assert.Equal(2, matches.Count);
        Assert.Equal("1", matches[0].Groups[1].Value);
        Assert.Contains("29 models", matches[0].Groups[2].Value);
        Assert.Equal("2", matches[1].Groups[1].Value);
        Assert.Contains("Qwen2.5-0.5B", matches[1].Groups[2].Value);

        // Parse the question
        var questionPattern = new Regex(@"Question:\s*(.+)");
        var questionMatch = questionPattern.Match(userMessage);
        Assert.True(questionMatch.Success);
        Assert.Contains("smallest tool-calling model", questionMatch.Groups[1].Value);
    }

    // ──────────────────────────────────────────────
    // Insufficient context handling
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("I don't have enough information to answer that question based on the provided context.")]
    [InlineData("Based on the provided context, I cannot determine the answer to this question.")]
    [InlineData("The provided context does not contain information about this topic.")]
    public void IDontKnowResponse_WhenContextInsufficient(string response)
    {
        // RAG models should respond with refusal when context doesn't contain the answer
        var refusalPatterns = new[]
        {
            "don't have enough information",
            "cannot determine",
            "does not contain"
        };

        var matchesRefusal = refusalPatterns.Any(pattern =>
            response.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        Assert.True(matchesRefusal, "Response should contain a refusal pattern when context is insufficient");
    }

    // ──────────────────────────────────────────────
    // Multiple citation references
    // ──────────────────────────────────────────────

    [Fact]
    public void MultipleCitations_AllReferenceValidSources()
    {
        var context = """
            Context:
            [1] The library supports Phi-3.5, Qwen2.5, and Llama-3.2 models.
            [2] All models are available in ONNX INT4 format.
            [3] GPU acceleration is supported via CUDA and DirectML.
            """;

        var answer = "The library supports multiple model families [1] in ONNX INT4 format [2], with GPU acceleration via CUDA and DirectML [3].";

        // Extract source IDs from context
        var sourceIds = new Regex(@"\[(\d+)\]").Matches(context)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToHashSet();

        // Extract citation IDs from answer
        var citationIds = new Regex(@"\[(\d+)\]").Matches(answer)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        // All citations in the answer should reference valid sources
        foreach (var citationId in citationIds)
        {
            Assert.Contains(citationId, sourceIds);
        }
    }

    [Fact]
    public void ContextWithNumberedSources_ParsesAllEntries()
    {
        var context = """
            Context:
            [1] ElBruno.LocalLLMs v0.1.0 supports local inference.
            [2] Models are downloaded from HuggingFace automatically.
            [3] The library implements IChatClient from Microsoft.Extensions.AI.
            [4] Supported formats include ONNX GenAI.
            [5] Fine-tuned models are available for tool calling and RAG.
            """;

        var entryPattern = new Regex(@"\[(\d+)\]\s+(.+)");
        var entries = entryPattern.Matches(context);

        Assert.Equal(5, entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            Assert.Equal((i + 1).ToString(), entries[i].Groups[1].Value);
            Assert.False(string.IsNullOrWhiteSpace(entries[i].Groups[2].Value));
        }
    }
}
