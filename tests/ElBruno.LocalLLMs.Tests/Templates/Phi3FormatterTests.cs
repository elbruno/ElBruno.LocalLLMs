using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Phi-3 template formatter.
/// Phi-3 format: <![CDATA[<|{role}|>\n{content}<|end|>]]>
/// </summary>
public class Phi3FormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Phi3);

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
            "<|system|>\nYou are helpful.<|end|>\n" +
            "<|user|>\nHello<|end|>\n" +
            "<|assistant|>\n";

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
            "<|user|>\nHello<|end|>\n" +
            "<|assistant|>\n";

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
            "<|system|>\nYou are helpful.<|end|>\n" +
            "<|user|>\nHi<|end|>\n" +
            "<|assistant|>\nHello! How can I help?<|end|>\n" +
            "<|user|>\nWhat is 2+2?<|end|>\n" +
            "<|assistant|>\n";

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
            "<|system|>\nYou are a translator.<|end|>\n" +
            "<|assistant|>\n";

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
            "<|user|>\n<|end|>\n" +
            "<|assistant|>\n";

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

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_AlwaysEndsWithAssistantTag()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Question?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|assistant|>\n", result);
    }

    [Fact]
    public void Format_EachNonLastMessageHasEndToken()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Sys."),
            new(ChatRole.User, "User."),
            new(ChatRole.Assistant, "Asst."),
            new(ChatRole.User, "User2.")
        };

        var result = _formatter.FormatMessages(messages);

        var endTokenCount = result.Split("<|end|>").Length - 1;
        Assert.Equal(messages.Count, endTokenCount);
    }

    [Fact]
    public void Format_EmptyMessageList_ReturnsAssistantPrompt()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|assistant|>\n", result);
    }

    // ──────────────────────────────────────────────
    // Phi-3 specific: verify NO ChatML tokens leak
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
}
