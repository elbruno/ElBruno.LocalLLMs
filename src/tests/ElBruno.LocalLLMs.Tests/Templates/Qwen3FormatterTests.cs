using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Qwen3 template formatter.
/// Qwen3 extends Qwen/Qwen2.5 ChatML format with:
///   - &lt;think&gt;\n\n&lt;/think&gt; prefix on every assistant generation prompt
///   - &lt;tools&gt;/&lt;/tools&gt; XML block for tool definitions (one JSON per line, no array)
///   - &lt;tool_call&gt;/&lt;/tool_call&gt; XML tags (no "id" field)
///   - &lt;tool_response&gt;/&lt;/tool_response&gt; XML tags for tool results
/// </summary>
public class Qwen3FormatterTests
{
    private readonly Qwen3Formatter _formatter = new();

    // ──────────────────────────────────────────────
    // Group 1: Basic message formatting (no tools)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_SimpleUserMessage_HasThinkPrefix()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|im_start|>assistant\n<think>\n\n</think>\n\n", result);
    }

    [Fact]
    public void FormatMessages_SimpleUserMessage_DoesNotEndWithBareAssistantPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        // Must not end with the bare assistant prompt (Qwen style); think block must follow
        Assert.DoesNotMatch(@"\<\|im_start\|\>assistant\n$", result);
        Assert.EndsWith("<think>\n\n</think>\n\n", result);
    }

    [Fact]
    public void FormatMessages_SystemAndUser_CorrectStructure()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>system\n", result);
        Assert.Contains("You are helpful.", result);
        Assert.Contains("<|im_end|>\n", result);
        Assert.Contains("<|im_start|>user\nHello<|im_end|>\n", result);
        Assert.EndsWith("<|im_start|>assistant\n<think>\n\n</think>\n\n", result);
    }

    [Fact]
    public void FormatMessages_MultiTurn_AllRolesFormatted()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Q1"),
            new(ChatRole.Assistant, "A1"),
            new(ChatRole.User, "Q2")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>system\n", result);
        Assert.Contains("<|im_start|>user\nQ1<|im_end|>\n", result);
        Assert.Contains("<|im_start|>assistant\n", result);
        Assert.Contains("A1", result);
        Assert.Contains("<|im_start|>user\nQ2<|im_end|>\n", result);
        Assert.EndsWith("<|im_start|>assistant\n<think>\n\n</think>\n\n", result);
    }

    // ──────────────────────────────────────────────
    // Group 2: Tool definition injection
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithTools_HasToolsXmlBlock()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Check weather")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("# Tools\n", result);
        Assert.Contains("<tools>\n", result);
        Assert.Contains("</tools>", result);
    }

    [Fact]
    public void FormatMessages_WithTools_ToolJsonNotAnArray()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Check weather")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // The tools block must not be a JSON array — each tool is a standalone JSON object on its own line.
        // A JSON array would start with "[\n" or "[ \n" immediately after the <tools> opening tag.
        var toolsTagIdx = result.LastIndexOf("<tools>\n", StringComparison.Ordinal);
        Assert.True(toolsTagIdx >= 0, "Expected <tools> opening tag in output");
        var afterTag = result[(toolsTagIdx + "<tools>\n".Length)..];
        Assert.False(afterTag.TrimStart().StartsWith("["), "Tool definitions should not be a JSON array");
    }

    [Fact]
    public void FormatMessages_WithTools_ToolJsonContainsExpectedKeys()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Check weather")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("\"type\"", result);
        Assert.Contains("\"function\"", result);
        Assert.Contains("get_weather", result);
    }

    [Fact]
    public void FormatMessages_WithTools_NoSystemMessage_InjectsToolBlock()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Check weather")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // A synthetic system message must be injected before the user turn
        Assert.Contains("<|im_start|>system\n", result);
        var systemIdx = result.IndexOf("<|im_start|>system\n", StringComparison.Ordinal);
        var toolsIdx = result.IndexOf("# Tools", StringComparison.Ordinal);
        Assert.True(toolsIdx > systemIdx, "# Tools block should appear inside/after the injected system turn");
    }

    [Fact]
    public void FormatMessages_WithMultipleTools_EachOnOwnLine()
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

        // Each tool should appear in the output as a separate JSON object (not a JSON array)
        var toolsTagIdx = result.LastIndexOf("<tools>\n", StringComparison.Ordinal);
        Assert.True(toolsTagIdx >= 0, "Expected <tools> opening tag in output");
        var afterTag = result[(toolsTagIdx + "<tools>\n".Length)..];
        Assert.False(afterTag.TrimStart().StartsWith("["), "Tools block content should not be a JSON array");

        // Both tool names must appear within the tools block
        Assert.Contains("get_weather", result);
        Assert.Contains("get_time", result);
    }

    [Fact]
    public void FormatMessages_ToolDefinition_ContainsExpectedInstruction()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Do something")
        };

        var tool = AIFunctionFactory.Create(
            (string x) => x,
            name: "do_thing",
            description: "Does a thing"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains(
            "For each function call, return a json object with function name and arguments within <tool_call></tool_call> XML tags",
            result);
    }

    // ──────────────────────────────────────────────
    // Group 3: Tool call in conversation history
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_AssistantWithFunctionCall_NoIdField()
    {
        var funcCall = new FunctionCallContent(
            callId: "123",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { ["city"] = "London" }
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in London?"),
            new(ChatRole.Assistant, [funcCall])
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<tool_call>", result);
        Assert.DoesNotContain("\"id\"", result);
        Assert.Contains("\"name\":\"get_weather\"", result.Replace(" ", "").Replace("\n", ""));
        Assert.Contains("\"arguments\"", result);
    }

    [Fact]
    public void FormatMessages_AssistantWithFunctionCall_WrappedInXmlTags()
    {
        var funcCall = new FunctionCallContent(
            callId: "abc",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { ["city"] = "Paris" }
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Weather in Paris?"),
            new(ChatRole.Assistant, [funcCall])
        };

        var result = _formatter.FormatMessages(messages);

        // Must be wrapped in <tool_call>...</tool_call>, not raw JSON
        Assert.Contains("<tool_call>\n", result);
        Assert.Contains("\n</tool_call>", result);
        var toolCallStart = result.IndexOf("<tool_call>\n", StringComparison.Ordinal);
        var toolCallEnd = result.IndexOf("\n</tool_call>", StringComparison.Ordinal);
        Assert.True(toolCallEnd > toolCallStart, "Closing </tool_call> must come after opening <tool_call>");
    }

    // ──────────────────────────────────────────────
    // Group 4: Tool result format (user turn with function result)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_UserWithFunctionResult_UsesXmlTags()
    {
        var funcCall = new FunctionCallContent(callId: "123", name: "get_weather");
        var funcResult = new FunctionResultContent(callId: "123", result: "25°C");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?"),
            new(ChatRole.Assistant, [funcCall]),
            new(ChatRole.User, [funcResult])
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<tool_response>\n25°C\n</tool_response>", result);
        Assert.DoesNotContain("Tool result for 123:", result);
    }

    [Fact]
    public void FormatMessages_UserWithFunctionResult_NotPlainText()
    {
        var funcCall = new FunctionCallContent(callId: "99", name: "do_thing");
        var funcResult = new FunctionResultContent(callId: "99", result: "done");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Do the thing"),
            new(ChatRole.Assistant, [funcCall]),
            new(ChatRole.User, [funcResult])
        };

        var result = _formatter.FormatMessages(messages);

        // Must use XML wrapper, not legacy plain-text format
        Assert.Contains("<tool_response>", result);
        Assert.DoesNotContain($"Tool result for 99:", result);
    }

    [Fact]
    public void FormatMessages_UserWithMultipleFunctionResults_EachWrapped()
    {
        var call1 = new FunctionCallContent(callId: "1", name: "fn_a");
        var call2 = new FunctionCallContent(callId: "2", name: "fn_b");
        var result1 = new FunctionResultContent(callId: "1", result: "result_a");
        var result2 = new FunctionResultContent(callId: "2", result: "result_b");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Run both"),
            new(ChatRole.Assistant, [call1, call2]),
            new(ChatRole.User, [result1, result2])
        };

        var result = _formatter.FormatMessages(messages);

        // Both results must appear in their own <tool_response> blocks
        var responseCount = CountOccurrences(result, "<tool_response>");
        Assert.Equal(2, responseCount);
        Assert.Contains("result_a", result);
        Assert.Contains("result_b", result);
    }

    // ──────────────────────────────────────────────
    // Group 5: Backward compatibility — QwenFormatter UNCHANGED
    // ──────────────────────────────────────────────

    [Fact]
    public void QwenFormatter_StillUsesOldFormat_NotThinkPrefix()
    {
        var qwenFormatter = new QwenFormatter();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = qwenFormatter.FormatMessages(messages);

        // The original Qwen formatter must end with the bare assistant prompt
        Assert.EndsWith("<|im_start|>assistant\n", result);
        Assert.DoesNotContain("<think>", result);
    }

    [Fact]
    public void QwenFormatter_ToolResult_StillUsesPlainText()
    {
        var qwenFormatter = new QwenFormatter();
        var funcCall = new FunctionCallContent(callId: "42", name: "tool_x");
        var funcResult = new FunctionResultContent(callId: "42", result: "output");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Do it"),
            new(ChatRole.Assistant, [funcCall]),
            new(ChatRole.User, [funcResult])
        };

        var result = qwenFormatter.FormatMessages(messages);

        Assert.Contains("Tool result for 42: output", result);
        Assert.DoesNotContain("<tool_response>", result);
    }

    [Fact]
    public void Qwen3Formatter_IsDistinctType_NotQwenFormatter()
    {
        Assert.IsNotType<QwenFormatter>(_formatter);
        Assert.IsType<Qwen3Formatter>(_formatter);
    }

    // ──────────────────────────────────────────────
    // Group 6: Submit protocol
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithSubmitTool_IncludedInToolDefinitions()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Complete the task")
        };

        var submitTool = AIFunctionFactory.Create(
            () => "submitted",
            name: "submit",
            description: "Submit the final answer"
        );

        var result = _formatter.FormatMessages(messages, new[] { submitTool });

        Assert.Contains("submit", result);
        Assert.Contains("<tools>", result);
    }

    [Fact]
    public void FormatMessages_SubmitToolCall_ParseableAsRegularToolCall()
    {
        var funcCall = new FunctionCallContent(
            callId: "s1",
            name: "submit",
            arguments: new Dictionary<string, object?>()
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Do the task"),
            new(ChatRole.Assistant, [funcCall])
        };

        var result = _formatter.FormatMessages(messages);

        // submit is just another tool call — normal <tool_call> wrapping, no id field
        Assert.Contains("<tool_call>", result);
        Assert.Contains("\"name\":\"submit\"".Replace(" ", ""),
            result.Replace(" ", "").Replace("\n", ""));
        Assert.Contains("\"arguments\"", result);
        Assert.DoesNotContain("\"id\"", result);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
