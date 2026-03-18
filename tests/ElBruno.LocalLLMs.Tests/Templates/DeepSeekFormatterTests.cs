using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for DeepSeek template formatter.
/// DeepSeek-R1 uses special sentence boundary tokens.
/// </summary>
public class DeepSeekFormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.DeepSeek);

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

        Assert.StartsWith("<｜begin▁of▁sentence｜>", result);
        Assert.Contains("<｜system｜>\nYou are helpful.", result);
        Assert.Contains("<｜user｜>\nHello", result);
        Assert.EndsWith("<｜assistant｜>\n", result);
    }

    [Fact]
    public void Format_UserOnly_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.StartsWith("<｜begin▁of▁sentence｜>", result);
        Assert.Contains("<｜user｜>\nHello", result);
        Assert.EndsWith("<｜assistant｜>\n", result);
    }

    [Fact]
    public void Format_MultiTurn_ProducesCorrectOutput()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!"),
            new(ChatRole.User, "What is 2+2?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.StartsWith("<｜begin▁of▁sentence｜>", result);
        Assert.Contains("<｜system｜>", result);
        Assert.Contains("<｜user｜>\nHi", result);
        Assert.Contains("<｜assistant｜>\nHello!", result);
        Assert.Contains("<｜user｜>\nWhat is 2+2?", result);
        Assert.EndsWith("<｜assistant｜>\n", result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_StartsWithBeginOfSentence()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.StartsWith("<｜begin▁of▁sentence｜>", result);
    }

    [Fact]
    public void Format_EndsWithAssistantPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<｜assistant｜>\n", result);
    }

    [Fact]
    public void Format_ContainsEndOfSentenceTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System."),
            new(ChatRole.User, "User.")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<｜end▁of▁sentence｜>", result);
    }

    [Fact]
    public void Format_EmptyContent_HandlesGracefully()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.StartsWith("<｜begin▁of▁sentence｜>", result);
        Assert.Contains("<｜user｜>", result);
        Assert.EndsWith("<｜assistant｜>\n", result);
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
    public void Format_DoesNotContainGemmaTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<start_of_turn>", result);
        Assert.DoesNotContain("<end_of_turn>", result);
    }

    [Fact]
    public void Format_ContainsDeepSeekSpecificTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<｜begin▁of▁sentence｜>", result);
        Assert.Contains("<｜user｜>", result);
    }
}
