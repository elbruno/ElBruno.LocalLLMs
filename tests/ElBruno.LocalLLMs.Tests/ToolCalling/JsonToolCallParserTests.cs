using ElBruno.LocalLLMs.ToolCalling;

namespace ElBruno.LocalLLMs.Tests.ToolCalling;

/// <summary>
/// Tests for <see cref="JsonToolCallParser"/> — parses tool calls from model output.
/// Covers Qwen-style tags, ChatML plain JSON, arrays, edge cases, and malformed input.
/// </summary>
public class JsonToolCallParserTests
{
    private readonly JsonToolCallParser _parser = new();

    // ──────────────────────────────────────────────
    // Happy path — single tool calls
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_SingleToolCallWithTags_ReturnsOneCall()
    {
        var input = """<tool_call>{"name": "get_weather", "arguments": {"city": "Seattle"}}</tool_call>""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].FunctionName);
        Assert.Equal("Seattle", result[0].Arguments["city"]);
        Assert.NotNull(result[0].CallId);
    }

    [Fact]
    public void Parse_SingleToolCallRawJson_ReturnsOneCall()
    {
        var input = """{"name": "get_weather", "arguments": {"city": "Seattle"}}""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].FunctionName);
        Assert.Equal("Seattle", result[0].Arguments["city"]);
    }

    [Fact]
    public void Parse_ToolCallWithNestedArguments_ParsesCorrectly()
    {
        var input = """
            <tool_call>
            {
                "name": "create_task",
                "arguments": {
                    "task": {
                        "title": "Review PR",
                        "metadata": {"priority": "high", "tags": ["backend", "api"]}
                    }
                }
            }
            </tool_call>
            """;

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("create_task", result[0].FunctionName);
        Assert.True(result[0].Arguments.ContainsKey("task"));
    }

    [Fact]
    public void Parse_ToolCallWithVariousArgumentTypes_ParsesCorrectly()
    {
        var input = """
            <tool_call>
            {
                "name": "test_fn",
                "arguments": {
                    "str_val": "hello",
                    "int_val": 42,
                    "bool_val": true,
                    "null_val": null,
                    "array_val": [1, 2, 3]
                }
            }
            </tool_call>
            """;

        var result = _parser.Parse(input);

        Assert.Single(result);
        var args = result[0].Arguments;
        Assert.Equal("hello", args["str_val"]?.ToString());
        // Note: JSON numbers may be parsed as different numeric types
        Assert.NotNull(args["int_val"]);
        Assert.NotNull(args["bool_val"]);
        Assert.Null(args["null_val"]);
        Assert.NotNull(args["array_val"]);
    }

    // ──────────────────────────────────────────────
    // Happy path — multiple tool calls
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleToolCallsInArray_ReturnsAll()
    {
        var input = """
            [
                {"name": "get_weather", "arguments": {"city": "Seattle"}},
                {"name": "get_time", "arguments": {"timezone": "PST"}}
            ]
            """;

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("get_weather", result[0].FunctionName);
        Assert.Equal("get_time", result[1].FunctionName);
    }

    [Fact]
    public void Parse_MultipleSeparateToolCallTags_ReturnsAll()
    {
        var input = """
            <tool_call>{"name": "fn1", "arguments": {"a": 1}}</tool_call>
            Some text in between
            <tool_call>{"name": "fn2", "arguments": {"b": 2}}</tool_call>
            """;

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("fn1", result[0].FunctionName);
        Assert.Equal("fn2", result[1].FunctionName);
    }

    // ──────────────────────────────────────────────
    // Edge cases — no tool calls
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_PlainTextNoToolCalls_ReturnsEmptyList()
    {
        var input = "Just a regular response from the model.";

        var result = _parser.Parse(input);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MixedTextAndToolCalls_ExtractsOnlyToolCalls()
    {
        var input = """
            Here's the weather info:
            <tool_call>{"name": "get_weather", "arguments": {"city": "Portland"}}</tool_call>
            And that's all!
            """;

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].FunctionName);
    }

    // ──────────────────────────────────────────────
    // Edge cases — malformed JSON
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedJson_ReturnsEmptyList()
    {
        var input = """<tool_call>{"name": "bad_fn", "arguments": {invalid json}}</tool_call>""";

        var result = _parser.Parse(input);

        // Parser should handle errors gracefully, not throw
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MalformedJsonNoTags_ReturnsEmptyList()
    {
        var input = """{"name": "bad_fn", this is not valid json}""";

        var result = _parser.Parse(input);

        Assert.Empty(result);
    }

    // ──────────────────────────────────────────────
    // Edge cases — arguments variations
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_ToolCallWithEmptyArguments_Succeeds()
    {
        var input = """<tool_call>{"name": "no_args_fn", "arguments": {}}</tool_call>""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("no_args_fn", result[0].FunctionName);
        Assert.Empty(result[0].Arguments);
    }

    [Fact]
    public void Parse_ToolCallMissingArgumentsKey_SucceedsWithEmptyDict()
    {
        var input = """<tool_call>{"name": "simple_fn"}</tool_call>""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("simple_fn", result[0].FunctionName);
        Assert.Empty(result[0].Arguments);
    }

    // ──────────────────────────────────────────────
    // Format-specific tests
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_QwenStyleWithNewlines_ParsesCorrectly()
    {
        var input = """
            <tool_call>
            {"name": "qwen_fn", "arguments": {"param": "value"}}
            </tool_call>
            """;

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("qwen_fn", result[0].FunctionName);
    }

    [Fact]
    public void Parse_ChatMLStylePlainJson_ParsesCorrectly()
    {
        var input = """{"name": "chatml_fn", "arguments": {"x": 10}}""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("chatml_fn", result[0].FunctionName);
        Assert.NotNull(result[0].Arguments["x"]);
    }

    [Fact]
    public void Parse_WhitespaceAroundTags_ParsesCorrectly()
    {
        var input = """
            
            
            <tool_call>  {"name": "ws_fn", "arguments": {}}  </tool_call>
            
            
            """;

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("ws_fn", result[0].FunctionName);
    }

    // ──────────────────────────────────────────────
    // CallId and RawText
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_GeneratesUniqueCallIds()
    {
        var input = """
            [
                {"name": "fn1", "arguments": {}},
                {"name": "fn2", "arguments": {}}
            ]
            """;

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].CallId, result[1].CallId);
        Assert.All(result, call => Assert.False(string.IsNullOrEmpty(call.CallId)));
    }

    [Fact]
    public void Parse_RawTextCapturesOriginal()
    {
        var input = """<tool_call>{"name": "fn", "arguments": {"a": 1}}</tool_call>""";

        var result = _parser.Parse(input);

        Assert.Single(result);
        // RawText should contain the original JSON or the full tag content
        Assert.NotNull(result[0].RawText);
    }

    // ──────────────────────────────────────────────
    // Null handling
    // ──────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }
}
