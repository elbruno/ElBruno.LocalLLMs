using ElBruno.LocalLLMs.ToolCalling;

namespace ElBruno.LocalLLMs.Tests.ToolCalling;

/// <summary>
/// Tests for <see cref="HarmonyToolCallParser"/> — GPT-OSS tool calls on the
/// commentary channel with a <c>to=functions.NAME</c> recipient.
/// </summary>
public class HarmonyToolCallParserTests
{
    private readonly HarmonyToolCallParser _parser = new();

    [Fact]
    public void Parse_RecipientBeforeChannel_ExtractsCall()
    {
        const string text =
            "<|start|>assistant to=functions.get_weather<|channel|>commentary json<|message|>{\"city\":\"Paris\"}<|call|>";

        var calls = _parser.Parse(text);

        var call = Assert.Single(calls);
        Assert.Equal("get_weather", call.FunctionName);
        Assert.Equal("Paris", call.Arguments["city"]);
    }

    [Fact]
    public void Parse_RecipientAfterChannel_ExtractsCall()
    {
        const string text =
            "<|channel|>commentary to=functions.lookup <|constrain|>json<|message|>{\"id\":42}<|call|>";

        var calls = _parser.Parse(text);

        var call = Assert.Single(calls);
        Assert.Equal("lookup", call.FunctionName);
        Assert.Equal(42L, Assert.IsType<long>(call.Arguments["id"]));
    }

    [Fact]
    public void Parse_PrecedingAnalysisChannel_IsIgnored()
    {
        const string text =
            "<|channel|>analysis<|message|>I should look up the weather.<|end|>" +
            "<|start|>assistant to=functions.get_weather<|channel|>commentary json<|message|>{\"city\":\"Oslo\"}<|call|>";

        var call = Assert.Single(_parser.Parse(text));

        Assert.Equal("get_weather", call.FunctionName);
        Assert.Equal("Oslo", call.Arguments["city"]);
    }

    [Fact]
    public void Parse_NoToolCall_ReturnsEmpty()
    {
        const string text = "<|channel|>final<|message|>The capital is Paris.<|return|>";

        Assert.Empty(_parser.Parse(text));
    }

    [Fact]
    public void Parse_EmptyArguments_ReturnsCallWithNoArguments()
    {
        const string text = "<|start|>assistant to=functions.ping<|channel|>commentary json<|message|>{}<|call|>";

        var call = Assert.Single(_parser.Parse(text));

        Assert.Equal("ping", call.FunctionName);
        Assert.Empty(call.Arguments);
    }

    [Fact]
    public void Parse_MalformedArguments_StillReturnsCall()
    {
        // Truncated generation — the intent to call is preserved.
        const string text = "<|start|>assistant to=functions.ping<|channel|>commentary json<|message|>{\"a\":<|call|>";

        var call = Assert.Single(_parser.Parse(text));

        Assert.Equal("ping", call.FunctionName);
        Assert.Empty(call.Arguments);
    }

    [Fact]
    public void Parse_ComplexArgumentTypes_AreConverted()
    {
        const string text =
            "<|start|>assistant to=functions.search<|channel|>commentary json<|message|>" +
            "{\"q\":\"cats\",\"limit\":5,\"deep\":true,\"tags\":[\"a\",\"b\"]}<|call|>";

        var call = Assert.Single(_parser.Parse(text));

        Assert.Equal("cats", call.Arguments["q"]);
        Assert.Equal(5L, Assert.IsType<long>(call.Arguments["limit"]));
        Assert.Equal(true, call.Arguments["deep"]);
        Assert.Equal(new List<object?> { "a", "b" }, call.Arguments["tags"]);
    }

    [Fact]
    public void Parse_TwoCalls_ReturnsBoth()
    {
        const string text =
            "<|start|>assistant to=functions.first<|channel|>commentary json<|message|>{\"x\":1}<|call|>" +
            "<|start|>assistant to=functions.second<|channel|>commentary json<|message|>{\"y\":2}<|call|>";

        var calls = _parser.Parse(text);

        Assert.Equal(2, calls.Count);
        Assert.Equal("first", calls[0].FunctionName);
        Assert.Equal("second", calls[1].FunctionName);
    }

    [Fact]
    public void Parse_SingleCall_IsNotDuplicatedByOverlappingPatterns()
    {
        const string text =
            "<|start|>assistant to=functions.only<|channel|>commentary json<|message|>{\"a\":1}<|call|>";

        Assert.Single(_parser.Parse(text));
    }

    [Fact]
    public void Parse_AssignsUniqueCallIds()
    {
        const string text =
            "<|start|>assistant to=functions.first<|channel|>commentary json<|message|>{}<|call|>" +
            "<|start|>assistant to=functions.second<|channel|>commentary json<|message|>{}<|call|>";

        var calls = _parser.Parse(text);

        Assert.NotEqual(calls[0].CallId, calls[1].CallId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankInput_ReturnsEmpty(string input)
    {
        Assert.Empty(_parser.Parse(input));
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }

    [Fact]
    public void Factory_HarmonyFormat_ReturnsHarmonyParser()
    {
        var parser = ToolCallParserFactory.Create(ChatTemplateFormat.Harmony);

        Assert.IsType<HarmonyToolCallParser>(parser);
    }

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen3)]
    [InlineData(ChatTemplateFormat.Llama3)]
    public void Factory_NonHarmonyFormats_StillReturnJsonParser(ChatTemplateFormat format)
    {
        var parser = ToolCallParserFactory.Create(format);

        Assert.IsType<JsonToolCallParser>(parser);
    }
}
