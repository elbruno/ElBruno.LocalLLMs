using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.FineTuneEval;

/// <summary>
/// Tests for chat template compliance with the Qwen ChatML format.
/// Ensures QwenFormatter output adheres to the expected token structure
/// that fine-tuned models are trained against.
/// </summary>
public class ChatTemplateAdherenceTests
{
    private readonly QwenFormatter _formatter = new();

    // ──────────────────────────────────────────────
    // ChatML token structure
    // ──────────────────────────────────────────────

    [Fact]
    public void QwenFormatter_ProducesCorrectChatMLTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        // Must contain ChatML special tokens
        Assert.Contains("<|im_start|>", result);
        Assert.Contains("<|im_end|>", result);

        // Must contain proper role labels
        Assert.Contains("<|im_start|>system\n", result);
        Assert.Contains("<|im_start|>user\n", result);
        Assert.Contains("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void QwenFormatter_EachMessageHasStartAndEndTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System."),
            new(ChatRole.User, "User."),
            new(ChatRole.Assistant, "Assistant."),
            new(ChatRole.User, "Follow-up.")
        };

        var result = _formatter.FormatMessages(messages);

        // Count start and end tokens — each message gets one start, completed messages get end
        var startCount = result.Split("<|im_start|>").Length - 1;
        var endCount = result.Split("<|im_end|>").Length - 1;

        // 4 messages + 1 trailing assistant prompt = 5 starts
        Assert.Equal(5, startCount);
        // 4 completed messages (not the trailing assistant prompt) = 4 ends
        Assert.Equal(4, endCount);
    }

    // ──────────────────────────────────────────────
    // Multi-turn conversation format
    // ──────────────────────────────────────────────

    [Fact]
    public void MultiTurnConversation_MaintainsProperFormat()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a coding assistant."),
            new(ChatRole.User, "Write a hello world in C#"),
            new(ChatRole.Assistant, "Console.WriteLine(\"Hello World\");"),
            new(ChatRole.User, "Now in Python"),
            new(ChatRole.Assistant, "print(\"Hello World\")"),
            new(ChatRole.User, "Now in JavaScript")
        };

        var result = _formatter.FormatMessages(messages);

        // Verify ordering — each role appearance should be in correct sequence
        var systemIndex = result.IndexOf("<|im_start|>system\n");
        var firstUserIndex = result.IndexOf("<|im_start|>user\n");
        var firstAssistantEnd = result.IndexOf("<|im_start|>assistant\n");

        Assert.True(systemIndex < firstUserIndex, "System should come before first user message");
        Assert.True(firstUserIndex < firstAssistantEnd, "First user should come before first assistant");

        // The final token should be the assistant prompt
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void SystemUserAssistant_TurnOrderingIsCorrect()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Question?"),
            new(ChatRole.Assistant, "Answer.")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>system\nYou are helpful.<|im_end|>\n" +
            "<|im_start|>user\nQuestion?<|im_end|>\n" +
            "<|im_start|>assistant\nAnswer.<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Trailing assistant prompt
    // ──────────────────────────────────────────────

    [Fact]
    public void AssistantPrompt_AlwaysEndsFormattedOutput()
    {
        // Every formatted output should end with assistant prompt to signal generation
        var testCases = new List<List<ChatMessage>>
        {
            new() { new(ChatRole.User, "Hello") },
            new() { new(ChatRole.System, "Sys"), new(ChatRole.User, "Usr") },
            new()
            {
                new(ChatRole.User, "Q1"),
                new(ChatRole.Assistant, "A1"),
                new(ChatRole.User, "Q2")
            }
        };

        foreach (var messages in testCases)
        {
            var result = _formatter.FormatMessages(messages);
            Assert.EndsWith("<|im_start|>assistant\n", result);
        }
    }

    [Fact]
    public void ChatMLTokens_ProperlyPaired()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System prompt."),
            new(ChatRole.User, "User message."),
            new(ChatRole.Assistant, "Assistant response.")
        };

        var result = _formatter.FormatMessages(messages);

        // Split by <|im_start|> to get each message block
        var blocks = result.Split("<|im_start|>", StringSplitOptions.RemoveEmptyEntries);

        // Last block is the trailing "assistant\n" (no end token)
        // All other blocks should contain exactly one <|im_end|>
        for (int i = 0; i < blocks.Length - 1; i++)
        {
            var endCount = blocks[i].Split("<|im_end|>").Length - 1;
            Assert.Equal(1, endCount);
        }

        // Last block (trailing assistant prompt) should have no end token
        var lastBlock = blocks[^1];
        Assert.DoesNotContain("<|im_end|>", lastBlock);
    }

    // ──────────────────────────────────────────────
    // Tool-aware template adherence
    // ──────────────────────────────────────────────

    [Fact]
    public void ToolAwareTemplate_StillMaintainsChatMLFormat()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What's the time?")
        };

        var tool = AIFunctionFactory.Create(
            (string timezone) => "12:00",
            name: "get_time",
            description: "Get current time"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        // Even with tools, ChatML structure must be maintained
        Assert.Contains("<|im_start|>system", result);
        Assert.Contains("<|im_end|>", result);
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void EmptyConversation_ProducesValidOutput()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        // Even empty conversation should produce a valid assistant prompt
        Assert.Equal("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void ConversationWithToolCallTurn_MaintainsFormat()
    {
        var functionCall = new FunctionCallContent(
            callId: "call_001",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { { "city", "Tokyo" } }
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a weather assistant."),
            new(ChatRole.User, "What's the weather in Tokyo?"),
            new(ChatRole.Assistant, [functionCall]),
            new(ChatRole.User, "Tool result for call_001: Sunny, 25°C"),
            new(ChatRole.User, "Thanks!")
        };

        var result = _formatter.FormatMessages(messages);

        // Verify structural integrity even with tool call content
        Assert.Contains("<|im_start|>system", result);
        Assert.Contains("<|im_start|>assistant", result);
        Assert.Contains("<tool_call>", result);
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }
}
