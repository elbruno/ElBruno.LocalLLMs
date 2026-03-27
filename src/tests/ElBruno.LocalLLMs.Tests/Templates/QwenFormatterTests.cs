using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Qwen template formatter.
/// Qwen2.5 uses ChatML-style tokens: <![CDATA[<|im_start|>{role}\n{content}<|im_end|>]]>
/// </summary>
public class QwenFormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Qwen);

    // ──────────────────────────────────────────────
    // Standard message formatting (ChatML-style)
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
            new(ChatRole.Assistant, "Hello!"),
            new(ChatRole.User, "What is 2+2?")
        };

        var result = _formatter.FormatMessages(messages);

        var expected =
            "<|im_start|>system\nYou are helpful.<|im_end|>\n" +
            "<|im_start|>user\nHi<|im_end|>\n" +
            "<|im_start|>assistant\nHello!<|im_end|>\n" +
            "<|im_start|>user\nWhat is 2+2?<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_EndsWithAssistantPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Question?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|im_start|>assistant\n", result);
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
            "<|im_start|>user\n<|im_end|>\n" +
            "<|im_start|>assistant\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_EmptyMessageList_ReturnsAssistantPrompt()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        Assert.Equal("<|im_start|>assistant\n", result);
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
    public void Format_SpecialCharacters_PreservesContent()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "代码是什么? (What is code?)")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("代码是什么? (What is code?)", result);
    }

    [Fact]
    public void Format_EachNonLastMessageHasEndToken()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Sys."),
            new(ChatRole.User, "User.")
        };

        var result = _formatter.FormatMessages(messages);

        var endTokenCount = result.Split("<|im_end|>").Length - 1;
        Assert.Equal(messages.Count, endTokenCount);
    }
}
