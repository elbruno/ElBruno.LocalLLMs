using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for ChatML template formatter.
/// ChatML format: <![CDATA[<|im_start|>{role}\n{content}<|im_end|>]]>
/// </summary>
public class ChatMLFormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);

    // ──────────────────────────────────────────────
    // Standard message formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_SystemAndUser_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>system\nYou are helpful.<|im_end|>\n" +
            "<|im_start|>user\nHello<|im_end|>\n" +
            "<|im_start|>assistant\n";

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
            "<|im_start|>user\nHello<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_MultiTurn_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello! How can I help?"),
            new(ChatRole.User, "What is 2+2?")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>system\nYou are helpful.<|im_end|>\n" +
            "<|im_start|>user\nHi<|im_end|>\n" +
            "<|im_start|>assistant\nHello! How can I help?<|im_end|>\n" +
            "<|im_start|>user\nWhat is 2+2?<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_SystemOnly_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a translator.")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>system\nYou are a translator.<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Content edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_EmptyContent_HandlesGracefully()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>user\n<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_MultilineContent_PreservesNewlines()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Line 1\nLine 2\nLine 3")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("Line 1\nLine 2\nLine 3", result);
    }

    [Fact]
    public void Format_ContentWithSpecialCharacters_PreservesContent()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What does <tag> mean in HTML? Use & and \"quotes\".")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("What does <tag> mean in HTML? Use & and \"quotes\".", result);
    }

    // ──────────────────────────────────────────────
    // Edge cases — empty list
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_EmptyMessageList_ReturnsAssistantPrompt()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        // Even with no messages, should end with assistant prompt
        Assert.Contains("<|im_start|>assistant\n", result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_AlwaysEndsWithAssistantPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System msg."),
            new(ChatRole.User, "User msg.")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    [Fact]
    public void Format_EachNonLastMessageHasEndToken()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System."),
            new(ChatRole.User, "User.")
        };

        var result = _formatter.FormatMessages(messages);

        // Count <|im_end|> — should match the number of messages
        var endTokenCount = result.Split("<|im_end|>").Length - 1;
        Assert.Equal(messages.Count, endTokenCount);
    }
}
