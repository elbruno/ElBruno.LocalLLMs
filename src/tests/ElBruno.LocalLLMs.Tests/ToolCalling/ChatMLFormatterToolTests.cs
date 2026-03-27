using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.ToolCalling;

/// <summary>
/// Tests for <see cref="ChatMLFormatter"/> tool-aware formatting.
/// Ensures tools are properly included in prompts and backwards compatibility is maintained.
/// </summary>
public class ChatMLFormatterToolTests
{
    private readonly ChatMLFormatter _formatter = new();

    // ──────────────────────────────────────────────
    // Backwards compatibility
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithNullTools_SameAsOriginal()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var withTools = _formatter.FormatMessages(messages, null);
        var withoutTools = _formatter.FormatMessages(messages);

        Assert.Equal(withoutTools, withTools);
    }

    [Fact]
    public void FormatMessages_WithEmptyToolsList_SameAsOriginal()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var withTools = _formatter.FormatMessages(messages, Array.Empty<AITool>());
        var withoutTools = _formatter.FormatMessages(messages);

        Assert.Equal(withoutTools, withTools);
    }

    // ──────────────────────────────────────────────
    // Single tool formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithOneTool_IncludesToolDescription()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Tool description should appear in the formatted output
        Assert.Contains("get_weather", result);
        Assert.Contains("Get current weather for a city", result);
    }

    [Fact]
    public void FormatMessages_WithOneTool_IncludesParameters()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city, string unit) => $"Weather in {city} ({unit})",
            name: "get_weather",
            description: "Get weather"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Parameters should appear in the formatted output (JSON schema)
        Assert.Contains("get_weather", result);
        Assert.Contains("city", result);
        Assert.Contains("unit", result);
    }

    // ──────────────────────────────────────────────
    // Multiple tools formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithMultipleTools_IncludesAllDescriptions()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Help me")
        };

        var tool1 = AIFunctionFactory.Create(
            (string city) => "weather",
            name: "get_weather",
            description: "Get weather info"
        );

        var tool2 = AIFunctionFactory.Create(
            (string timezone) => "time",
            name: "get_time",
            description: "Get current time"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool1, tool2 });

        Assert.Contains("get_weather", result);
        Assert.Contains("Get weather info", result);
        Assert.Contains("get_time", result);
        Assert.Contains("Get current time", result);
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_ToolWithNoDescription_StillFormats()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var tool = AIFunctionFactory.Create(
            () => "result",
            name: "simple_fn"
            // No description provided
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should still include tool name even without description
        Assert.Contains("simple_fn", result);
    }

    [Fact]
    public void FormatMessages_ToolWithNoParameters_StillFormats()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var tool = AIFunctionFactory.Create(
            () => "result",
            name: "no_params",
            description: "Function with no parameters"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("no_params", result);
        Assert.Contains("Function with no parameters", result);
    }

    // ──────────────────────────────────────────────
    // Integration with chat messages
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_ToolsWithSystemMessage_FormatsCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => "weather",
            name: "get_weather",
            description: "Get weather"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should contain both system message and tool info
        Assert.Contains("helpful assistant", result);
        Assert.Contains("get_weather", result);
        Assert.Contains("<|im_start|>system", result);
    }

    [Fact]
    public void FormatMessages_ToolsWithMultipleMessages_FormatsCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there!"),
            new(ChatRole.User, "What can you do?")
        };

        var tool = AIFunctionFactory.Create(
            (string x) => "y",
            name: "test_tool",
            description: "A test tool"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should contain all messages and tool info
        Assert.Contains("Hello", result);
        Assert.Contains("Hi there", result);
        Assert.Contains("What can you do", result);
        Assert.Contains("test_tool", result);
    }

    // ──────────────────────────────────────────────
    // Output structure validation
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithTools_EndsWithAssistantPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var tool = AIFunctionFactory.Create(
            () => "result",
            name: "fn"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should end with assistant prompt like the original formatter
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void FormatMessages_WithTools_UsesCorrectChatMLFormat()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var tool = AIFunctionFactory.Create(
            (string x) => "y",
            name: "test_fn"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should use ChatML format markers
        Assert.Contains("<|im_start|>", result);
        Assert.Contains("<|im_end|>", result);
    }
}
