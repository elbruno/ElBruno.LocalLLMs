using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Gemma template formatter.
/// Gemma uses: &lt;start_of_turn&gt;role\ncontent&lt;end_of_turn&gt;
/// </summary>
public class GemmaFormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);

    // ──────────────────────────────────────────────
    // Standard message formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_SystemAndUser_PrependsSystemToUser()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<start_of_turn>user\nYou are helpful.\n\nHello<end_of_turn>\n" +
            "<start_of_turn>model\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_UserOnly_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<start_of_turn>user\nHello<end_of_turn>\n" +
            "<start_of_turn>model\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_MultiTurn_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!"),
            new(ChatRole.User, "What is 2+2?")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<start_of_turn>user\nHi<end_of_turn>\n" +
            "<start_of_turn>model\nHello!<end_of_turn>\n" +
            "<start_of_turn>user\nWhat is 2+2?<end_of_turn>\n" +
            "<start_of_turn>model\n";

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_EndsWithModelPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Question?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<start_of_turn>model\n", result);
    }

    [Fact]
    public void Format_AssistantMappedToModel()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<start_of_turn>model\nHello!", result);
    }

    [Fact]
    public void Format_EmptyContent_HandlesGracefully()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<start_of_turn>user\n<end_of_turn>\n" +
            "<start_of_turn>model\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_EmptyMessageList_ReturnsModelPrompt()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        Assert.Equal("<start_of_turn>model\n", result);
    }

    // ──────────────────────────────────────────────
    // No cross-contamination
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_DoesNotContainChatMLTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System."),
            new(ChatRole.User, "User.")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|im_start|>", result);
        Assert.DoesNotContain("<|im_end|>", result);
    }

    [Fact]
    public void Format_DoesNotContainPhi3Tokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|user|>", result);
        Assert.DoesNotContain("<|assistant|>", result);
        Assert.DoesNotContain("<|end|>", result);
    }

    [Fact]
    public void Format_DoesNotContainLlama3Tokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|begin_of_text|>", result);
        Assert.DoesNotContain("<|start_header_id|>", result);
        Assert.DoesNotContain("<|eot_id|>", result);
    }

    [Fact]
    public void Format_ContainsGemmaSpecificTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<start_of_turn>", result);
        Assert.Contains("<end_of_turn>", result);
    }

    // ──────────────────────────────────────────────
    // Tool calling support (Gemma 4 models)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithNullTools_SameAsOriginal()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?")
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
            new(ChatRole.User, "What's the weather?")
        };

        var withTools = _formatter.FormatMessages(messages, Array.Empty<AITool>());
        var withoutTools = _formatter.FormatMessages(messages);

        Assert.Equal(withoutTools, withTools);
    }

    [Fact]
    public void FormatMessages_WithOneTool_IncludesToolDescription()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What's the weather in Seattle?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("get_weather", result);
        Assert.Contains("Get current weather for a city", result);
    }

    [Fact]
    public void FormatMessages_WithMultipleTools_IncludesAllDescriptions()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Help me plan my day")
        };

        var weatherTool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather"
        );

        var calendarTool = AIFunctionFactory.Create(
            (string date) => $"Events on {date}",
            name: "get_calendar",
            description: "Get calendar events"
        );

        var result = _formatter.FormatMessages(messages, new AITool[] { weatherTool, calendarTool });

        Assert.Contains("get_weather", result);
        Assert.Contains("Get current weather", result);
        Assert.Contains("get_calendar", result);
        Assert.Contains("Get calendar events", result);
    }

    [Fact]
    public void FormatMessages_WithToolAndParameters_IncludesParameterSchema()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city, string unit) => $"Weather in {city} ({unit})",
            name: "get_weather",
            description: "Get weather for a city"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.Contains("get_weather", result);
        Assert.Contains("city", result);
        Assert.Contains("unit", result);
    }

    [Fact]
    public void FormatMessages_WithFunctionCallContent_FormatsCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Paris?"),
            new(ChatRole.Assistant, [
                new FunctionCallContent("call_123", "get_weather", new Dictionary<string, object?>
                {
                    ["city"] = "Paris"
                })
            ])
        };

        var result = _formatter.FormatMessages(messages);

        // Should contain the function call in formatted output
        Assert.Contains("get_weather", result);
        Assert.Contains("Paris", result);
    }

    [Fact]
    public void FormatMessages_WithFunctionResultContent_FormatsCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Paris?"),
            new(ChatRole.Assistant, [
                new FunctionCallContent("call_123", "get_weather", new Dictionary<string, object?>
                {
                    ["city"] = "Paris"
                })
            ]),
            new(ChatRole.User, [
                new FunctionResultContent("call_123", "Sunny, 22°C")
            ])
        };

        var result = _formatter.FormatMessages(messages);

        // Should contain the function result (formatted as "Tool result: ...")
        Assert.Contains("Sunny, 22°C", result);
    }

    [Fact]
    public void FormatMessages_MultiTurnWithToolCalls_MaintainsGemmaStructure()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Check Seattle weather"),
            new(ChatRole.Assistant, [
                new FunctionCallContent("call_1", "get_weather", new Dictionary<string, object?>
                {
                    ["city"] = "Seattle"
                })
            ]),
            new(ChatRole.Tool, [
                new FunctionResultContent("call_1", "Rainy, 15°C")
            ]),
            new(ChatRole.Assistant, "The weather in Seattle is rainy at 15°C."),
            new(ChatRole.User, "What about Paris?")
        };

        var result = _formatter.FormatMessages(messages);

        // Should maintain Gemma token structure throughout
        Assert.Contains("<start_of_turn>user", result);
        Assert.Contains("<start_of_turn>model", result);
        Assert.Contains("<end_of_turn>", result);
        Assert.EndsWith("<start_of_turn>model\n", result);
    }

    [Fact]
    public void FormatMessages_ToolCallWithGemma4Model_ProducesValidFormat()
    {
        // This test verifies Gemma formatter works for Gemma 4 models
        // All Gemma 4 models use ChatTemplateFormat.Gemma and support tool calling
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What tools can help me?")
        };

        var tool = AIFunctionFactory.Create(
            (string query) => "Search results",
            name: "web_search",
            description: "Search the web"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Should have proper Gemma structure with tools injected in system message
        Assert.Contains("<start_of_turn>user", result);
        Assert.Contains("web_search", result);
        Assert.Contains("Search the web", result);
        Assert.Contains("You have access to the following tools", result);
        Assert.EndsWith("<start_of_turn>model\n", result);
    }
}
