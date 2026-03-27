using System.Text.Json;
using ElBruno.LocalLLMs.Internal;
using ElBruno.LocalLLMs.ToolCalling;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.FineTuneEval;

/// <summary>
/// Validates tool calling output format for fine-tuning data quality.
/// Tests that expected tool call patterns are parseable by JsonToolCallParser
/// and that QwenFormatter produces correct output for tool scenarios.
/// </summary>
public class ToolCallingFormatTests
{
    private readonly JsonToolCallParser _parser = new();
    private readonly QwenFormatter _formatter = new();

    // ──────────────────────────────────────────────
    // Parser validation — single tool calls
    // ──────────────────────────────────────────────

    [Fact]
    public void ValidToolCallJson_ParseableByJsonToolCallParser()
    {
        var toolCallOutput = """<tool_call>{"name": "get_weather", "arguments": {"city": "Paris"}}</tool_call>""";

        var result = _parser.Parse(toolCallOutput);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].FunctionName);
        Assert.Equal("Paris", result[0].Arguments["city"]);
    }

    [Fact]
    public void MultiToolCallOutput_ParsesCorrectly()
    {
        var toolCallOutput = """
            <tool_call>{"name": "get_weather", "arguments": {"city": "Seattle"}}</tool_call>
            <tool_call>{"name": "get_time", "arguments": {"timezone": "PST"}}</tool_call>
            """;

        var result = _parser.Parse(toolCallOutput);

        Assert.Equal(2, result.Count);
        Assert.Equal("get_weather", result[0].FunctionName);
        Assert.Equal("get_time", result[1].FunctionName);
    }

    [Fact]
    public void ToolCallWithNestedArguments_ParsesCorrectly()
    {
        var toolCallOutput = """
            <tool_call>
            {
                "name": "search_products",
                "arguments": {
                    "filters": {
                        "category": "electronics",
                        "price_range": {"min": 100, "max": 500},
                        "tags": ["wireless", "bluetooth"]
                    }
                }
            }
            </tool_call>
            """;

        var result = _parser.Parse(toolCallOutput);

        Assert.Single(result);
        Assert.Equal("search_products", result[0].FunctionName);
        Assert.True(result[0].Arguments.ContainsKey("filters"));
    }

    // ──────────────────────────────────────────────
    // QwenFormatter tool result format
    // ──────────────────────────────────────────────

    [Fact]
    public void ToolResultFormat_MatchesQwenFormatterExpectations()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Tool result for call_123: {\"temp\": 18, \"condition\": \"cloudy\"}")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>user", result);
        Assert.Contains("Tool result for call_123", result);
        Assert.Contains("<|im_end|>", result);
    }

    [Fact]
    public void SystemPromptWithToolDefinitions_FormatsCorrectlyViaQwenFormatter()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("<|im_start|>system", result);
        Assert.Contains("You are a helpful assistant.", result);
        Assert.Contains("get_weather", result);
        Assert.Contains("Get current weather for a city", result);
        Assert.Contains("tool_name", result);
    }

    [Fact]
    public void FunctionCallContent_FormattedCorrectlyForQwen()
    {
        var functionCall = new FunctionCallContent(
            callId: "call_abc123",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { { "city", "Seattle" } }
        );

        var assistantMessage = new ChatMessage(ChatRole.Assistant, [functionCall]);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Seattle?"),
            assistantMessage
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>assistant", result);
        Assert.Contains("<tool_call>", result);
        Assert.Contains("</tool_call>", result);
        Assert.Contains("get_weather", result);
    }

    [Fact]
    public void FunctionResultContent_FormattedCorrectlyForQwen()
    {
        var functionResult = new FunctionResultContent(
            callId: "call_abc123",
            result: "Sunny, 72°F"
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, [functionResult])
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>user", result);
        Assert.Contains("Tool result for call_abc123", result);
        Assert.Contains("Sunny, 72°F", result);
    }

    // ──────────────────────────────────────────────
    // Argument type coverage
    // ──────────────────────────────────────────────

    [Fact]
    public void ToolCallWithStringArgs_ParsesCorrectly()
    {
        var output = """<tool_call>{"name": "send_email", "arguments": {"to": "user@example.com", "subject": "Hello", "body": "World"}}</tool_call>""";

        var result = _parser.Parse(output);

        Assert.Single(result);
        Assert.Equal("send_email", result[0].FunctionName);
        Assert.Equal("user@example.com", result[0].Arguments["to"]);
        Assert.Equal("Hello", result[0].Arguments["subject"]);
        Assert.Equal("World", result[0].Arguments["body"]);
    }

    [Fact]
    public void ToolCallWithNumericArgs_ParsesCorrectly()
    {
        var output = """<tool_call>{"name": "calculate", "arguments": {"x": 42, "y": 3.14}}</tool_call>""";

        var result = _parser.Parse(output);

        Assert.Single(result);
        Assert.Equal("calculate", result[0].FunctionName);
        Assert.NotNull(result[0].Arguments["x"]);
        Assert.NotNull(result[0].Arguments["y"]);
    }

    [Fact]
    public void ToolCallWithBooleanAndNullArgs_ParsesCorrectly()
    {
        var output = """<tool_call>{"name": "set_config", "arguments": {"enabled": true, "verbose": false, "label": null}}</tool_call>""";

        var result = _parser.Parse(output);

        Assert.Single(result);
        Assert.Equal("set_config", result[0].FunctionName);
        Assert.Equal(true, result[0].Arguments["enabled"]);
        Assert.Equal(false, result[0].Arguments["verbose"]);
        Assert.Null(result[0].Arguments["label"]);
    }

    [Fact]
    public void ToolCallOutput_ContainsToolCallTags()
    {
        // Validates the expected training data format: tool calls must be wrapped in tags
        var validFormat = """<tool_call>{"name": "fn", "arguments": {}}</tool_call>""";

        Assert.Contains("<tool_call>", validFormat);
        Assert.Contains("</tool_call>", validFormat);

        var result = _parser.Parse(validFormat);
        Assert.Single(result);
    }

    [Fact]
    public void EmptyArguments_ToolCallParsesSuccessfully()
    {
        var output = """<tool_call>{"name": "get_current_time", "arguments": {}}</tool_call>""";

        var result = _parser.Parse(output);

        Assert.Single(result);
        Assert.Equal("get_current_time", result[0].FunctionName);
        Assert.Empty(result[0].Arguments);
    }

    // ──────────────────────────────────────────────
    // Full round-trip: formatter → parser
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatterOutput_RoundTripsToParser()
    {
        // Simulate what a fine-tuned model would produce:
        // 1. QwenFormatter formats the FunctionCallContent
        // 2. Parser extracts it back from the formatted output
        var functionCall = new FunctionCallContent(
            callId: "call_rt1",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { { "city", "Berlin" } }
        );

        var assistantMessage = new ChatMessage(ChatRole.Assistant, [functionCall]);
        var result = _formatter.FormatMessages(new List<ChatMessage> { assistantMessage });

        // Extract the <tool_call>...</tool_call> portion and parse it
        var parsed = _parser.Parse(result);

        Assert.Single(parsed);
        Assert.Equal("get_weather", parsed[0].FunctionName);
    }

    [Fact]
    public void MultipleToolsInSystemPrompt_AllIncluded()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Help me plan a trip")
        };

        var tool1 = AIFunctionFactory.Create(
            (string city) => "weather",
            name: "get_weather",
            description: "Get weather"
        );
        var tool2 = AIFunctionFactory.Create(
            (string from, string to) => "flights",
            name: "search_flights",
            description: "Search flights"
        );
        var tool3 = AIFunctionFactory.Create(
            (string city) => "hotels",
            name: "find_hotels",
            description: "Find hotels"
        );

        var result = _formatter.FormatMessages(messages, new AITool[] { tool1, tool2, tool3 });

        Assert.Contains("get_weather", result);
        Assert.Contains("search_flights", result);
        Assert.Contains("find_hotels", result);
    }
}
