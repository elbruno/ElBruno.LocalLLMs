using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Llama-3 template formatter.
/// Llama-3 uses header IDs and double newlines after header.
/// </summary>
public class Llama3FormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Llama3);

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
            "<|begin_of_text|>" +
            "<|start_header_id|>system<|end_header_id|>\n\n" +
            "You are helpful.<|eot_id|>" +
            "<|start_header_id|>user<|end_header_id|>\n\n" +
            "Hello<|eot_id|>" +
            "<|start_header_id|>assistant<|end_header_id|>\n\n";

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
            "<|begin_of_text|>" +
            "<|start_header_id|>user<|end_header_id|>\n\n" +
            "Hello<|eot_id|>" +
            "<|start_header_id|>assistant<|end_header_id|>\n\n";

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
            "<|begin_of_text|>" +
            "<|start_header_id|>system<|end_header_id|>\n\n" +
            "You are helpful.<|eot_id|>" +
            "<|start_header_id|>user<|end_header_id|>\n\n" +
            "Hi<|eot_id|>" +
            "<|start_header_id|>assistant<|end_header_id|>\n\n" +
            "Hello!<|eot_id|>" +
            "<|start_header_id|>user<|end_header_id|>\n\n" +
            "What is 2+2?<|eot_id|>" +
            "<|start_header_id|>assistant<|end_header_id|>\n\n";

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_StartsWithBeginOfText()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.StartsWith("<|begin_of_text|>", result);
    }

    [Fact]
    public void Format_EndsWithAssistantHeader()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|start_header_id|>assistant<|end_header_id|>\n\n", result);
    }

    [Fact]
    public void Format_EachMessageHasEotId()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Sys."),
            new(ChatRole.User, "User."),
            new(ChatRole.Assistant, "Asst."),
            new(ChatRole.User, "User2.")
        };

        var result = _formatter.FormatMessages(messages);

        var eotCount = result.Split("<|eot_id|>").Length - 1;
        Assert.Equal(messages.Count, eotCount);
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

        Assert.Contains("<|start_header_id|>user<|end_header_id|>\n\n", result);
        Assert.Contains("<|eot_id|>", result);
    }

    [Fact]
    public void Format_MultilineContent_PreservesNewlines()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Line 1\nLine 2")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("Line 1\nLine 2", result);
    }

    [Fact]
    public void Format_EmptyMessageList_ReturnsMinimalStructure()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|begin_of_text|>", result);
        Assert.Contains("<|start_header_id|>assistant<|end_header_id|>", result);
    }

    // ──────────────────────────────────────────────
    // Llama-3 specific: no other template tokens
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_DoesNotContainChatMLTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
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
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|system|>", result);
        Assert.DoesNotContain("<|user|>", result);
    }
}
