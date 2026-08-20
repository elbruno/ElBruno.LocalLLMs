using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for <see cref="HarmonyFormatter"/> — the OpenAI Harmony prompt format used by
/// GPT-OSS. Expectations are derived from the official chat template shipped with
/// <c>openai/gpt-oss-20b</c> (chat_template.jinja).
/// </summary>
public class HarmonyFormatterTests
{
    private static HarmonyFormatter CreateFormatter(ReasoningEffort effort = ReasoningEffort.Medium) =>
        new(effort, () => new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));

    // ── System message ────────────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_EmitsSystemMessageWithMetadata()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.StartsWith("<|start|>system<|message|>", result);
        Assert.Contains("Knowledge cutoff: 2024-06", result);
        Assert.Contains("Current date: 2026-01-15", result);
        Assert.Contains("# Valid channels: analysis, commentary, final.", result);
    }

    [Theory]
    [InlineData(ReasoningEffort.Low, "Reasoning: low")]
    [InlineData(ReasoningEffort.Medium, "Reasoning: medium")]
    [InlineData(ReasoningEffort.High, "Reasoning: high")]
    public void FormatMessages_RendersReasoningEffort(ReasoningEffort effort, string expected)
    {
        var result = CreateFormatter(effort).FormatMessages([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Contains(expected, result);
    }

    /// <summary>
    /// Harmony defines only low/medium/high, but Microsoft.Extensions.AI.ReasoningEffort
    /// has five levels. The extremes must clamp onto the nearest supported level.
    /// </summary>
    [Theory]
    [InlineData(ReasoningEffort.None, "Reasoning: low")]
    [InlineData(ReasoningEffort.ExtraHigh, "Reasoning: high")]
    public void FormatMessages_ClampsUnsupportedReasoningLevels(ReasoningEffort effort, string expected)
    {
        var result = CreateFormatter(effort).FormatMessages([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Contains(expected, result);
    }

    [Fact]
    public void FormatMessages_WithoutTools_OmitsCommentaryChannelNotice()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.DoesNotContain("Calls to these tools must go to the commentary channel", result);
    }

    // ── Developer message ─────────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_SystemPrompt_BecomesDeveloperMessage()
    {
        var result = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.System, "You are a pirate."),
            new ChatMessage(ChatRole.User, "Hello")
        ]);

        Assert.Contains("<|start|>developer<|message|># Instructions\n\nYou are a pirate.", result);

        // The caller's prompt must NOT be rendered as a Harmony system message.
        Assert.DoesNotContain("<|start|>system<|message|>You are a pirate.", result);
    }

    [Fact]
    public void FormatMessages_NoSystemPromptNoTools_OmitsDeveloperMessage()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.DoesNotContain("<|start|>developer", result);
    }

    // ── Conversation turns ────────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_UserMessage_UsesUserTurn()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "What is 2+2?")]);

        Assert.Contains("<|start|>user<|message|>What is 2+2?<|end|>", result);
    }

    [Fact]
    public void FormatMessages_AssistantMessage_UsesFinalChannel()
    {
        var result = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.User, "Hi"),
            new ChatMessage(ChatRole.Assistant, "Hello!"),
            new ChatMessage(ChatRole.User, "Bye")
        ]);

        Assert.Contains("<|start|>assistant<|channel|>final<|message|>Hello!<|end|>", result);
    }

    [Fact]
    public void FormatMessages_AlwaysEndsWithGenerationPrompt()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.EndsWith("<|start|>assistant", result);
    }

    [Fact]
    public void FormatMessages_PreservesTurnOrder()
    {
        var result = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.User, "first"),
            new ChatMessage(ChatRole.Assistant, "second"),
            new ChatMessage(ChatRole.User, "third")
        ]);

        var firstIndex = result.IndexOf("first", StringComparison.Ordinal);
        var secondIndex = result.IndexOf("second", StringComparison.Ordinal);
        var thirdIndex = result.IndexOf("third", StringComparison.Ordinal);

        Assert.True(firstIndex < secondIndex);
        Assert.True(secondIndex < thirdIndex);
    }

    // ── Tool definitions ──────────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithTools_RendersTypeScriptNamespace()
    {
        var tool = AIFunctionFactory.Create(
            (string city) => $"sunny in {city}",
            "get_weather",
            "Get the weather for a city.");

        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Weather?")], [tool]);

        Assert.Contains("# Tools", result);
        Assert.Contains("## functions", result);
        Assert.Contains("namespace functions {", result);
        Assert.Contains("// Get the weather for a city.", result);
        Assert.Contains("type get_weather = (_: {", result);
        Assert.Contains("city: string", result);
        Assert.Contains("} // namespace functions", result);

        // Tools must NOT be rendered as JSON, unlike the Qwen3 formatter.
        Assert.DoesNotContain("<tools>", result);
    }

    [Fact]
    public void FormatMessages_WithTools_AddsCommentaryChannelNoticeToSystem()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "ping", "Ping.");

        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hi")], [tool]);

        Assert.Contains("Calls to these tools must go to the commentary channel: 'functions'.", result);
    }

    [Fact]
    public void FormatMessages_ToolWithNoParameters_RendersArrowType()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "ping", "Ping.");

        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hi")], [tool]);

        Assert.Contains("type ping = () => any;", result);
    }

    [Fact]
    public void FormatMessages_ToolsWithoutSystemPrompt_StillRendersDeveloperBlock()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "ping", "Ping.");

        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "Hi")], [tool]);

        Assert.Contains("<|start|>developer<|message|>", result);
        Assert.DoesNotContain("# Instructions", result);
    }

    // ── Tool calls and results ────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_AssistantToolCall_UsesCommentaryChannelWithRecipient()
    {
        var call = new FunctionCallContent("call_1", "get_weather", new Dictionary<string, object?> { ["city"] = "Paris" });
        var assistant = new ChatMessage(ChatRole.Assistant, [call]);

        var result = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.User, "Weather in Paris?"),
            assistant
        ]);

        Assert.Contains("<|start|>assistant to=functions.get_weather<|channel|>commentary json<|message|>", result);
        Assert.Contains("\"city\":\"Paris\"", result);
        Assert.Contains("<|call|>", result);
    }

    [Fact]
    public void FormatMessages_ToolResult_AuthoredByFunctionNamespace()
    {
        var call = new FunctionCallContent("call_1", "get_weather", new Dictionary<string, object?> { ["city"] = "Paris" });
        var result = new FunctionResultContent("call_1", "18C and sunny");

        var prompt = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.User, "Weather in Paris?"),
            new ChatMessage(ChatRole.Assistant, [call]),
            new ChatMessage(ChatRole.Tool, [result])
        ]);

        Assert.Contains("<|start|>functions.get_weather to=assistant<|channel|>commentary<|message|>", prompt);
        Assert.Contains("18C and sunny", prompt);
    }

    [Fact]
    public void FormatMessages_ToolResultError_RendersErrorText()
    {
        var call = new FunctionCallContent("call_1", "get_weather", new Dictionary<string, object?>());
        var failed = new FunctionResultContent("call_1", result: null)
        {
            Exception = new InvalidOperationException("service down")
        };

        var prompt = CreateFormatter().FormatMessages(
        [
            new ChatMessage(ChatRole.User, "Weather?"),
            new ChatMessage(ChatRole.Assistant, [call]),
            new ChatMessage(ChatRole.Tool, [failed])
        ]);

        Assert.Contains("Error: service down", prompt);
    }

    // ── Robustness ────────────────────────────────────────────────────────────

    [Fact]
    public void FormatMessages_NullMessages_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateFormatter().FormatMessages(null!));
    }

    [Fact]
    public void FormatMessages_EmptyMessages_StillProducesValidPrompt()
    {
        var result = CreateFormatter().FormatMessages([]);

        Assert.Contains("<|start|>system<|message|>", result);
        Assert.EndsWith("<|start|>assistant", result);
    }

    [Fact]
    public void FormatMessages_IsDeterministic()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Be brief."),
            new(ChatRole.User, "Hello")
        };

        Assert.Equal(
            CreateFormatter().FormatMessages(messages),
            CreateFormatter().FormatMessages(messages));
    }

    [Fact]
    public void FormatMessages_UnicodeContent_IsPreserved()
    {
        var result = CreateFormatter().FormatMessages([new ChatMessage(ChatRole.User, "¿Qué tal? 你好 🎉")]);

        Assert.Contains("¿Qué tal? 你好 🎉", result);
    }
}
