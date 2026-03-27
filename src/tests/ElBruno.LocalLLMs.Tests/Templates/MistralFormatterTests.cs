using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for Mistral template formatter.
/// Mistral format: system prompt prepended to first [INST] block.
/// </summary>
public class MistralFormatterTests
{
    private readonly IChatTemplateFormatter _formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Mistral);

    // ──────────────────────────────────────────────
    // Standard message formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_SystemAndUser_PrependSystemToFirstInst()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        // System prompt is prepended to first user message
        var expected = "[INST] You are helpful.\n\nHello [/INST]";
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

        var expected = "[INST] Hello [/INST]";
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

        var expected = "[INST] Hi [/INST]Hello![INST] What is 2+2? [/INST]";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_SystemWithMultiTurn_PrependSystemOnlyToFirst()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Be concise."),
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!"),
            new(ChatRole.User, "What is 2+2?")
        };

        var result = _formatter.FormatMessages(messages);

        // System should appear only once, prepended to first user message
        var expected = "[INST] Be concise.\n\nHi [/INST]Hello![INST] What is 2+2? [/INST]";
        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // Structural assertions
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_EndsWithClosingInstTag()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Question?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("[/INST]", result);
    }

    [Fact]
    public void Format_EmptyContent_HandlesGracefully()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("[INST]", result);
        Assert.Contains("[/INST]", result);
    }

    [Fact]
    public void Format_EmptyMessageList_ReturnsEmptyString()
    {
        var messages = new List<ChatMessage>();

        var result = _formatter.FormatMessages(messages);

        // No messages → no [INST] blocks
        Assert.Equal(string.Empty, result);
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
    // Mistral specific: system is folded into [INST]
    // ──────────────────────────────────────────────

    [Fact]
    public void Format_SystemOnly_ProducesEmptyOutput()
    {
        // System-only messages with no user turn → nothing to wrap in [INST]
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful.")
        };

        var result = _formatter.FormatMessages(messages);

        // System message alone doesn't generate output (no user [INST] block)
        Assert.Equal(string.Empty, result);
    }

    // ──────────────────────────────────────────────
    // Mistral specific: no other template tokens
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
        Assert.DoesNotContain("<|end|>", result);
    }

    [Fact]
    public void Format_DoesNotContainLlama3Tokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|begin_of_text|>", result);
        Assert.DoesNotContain("<|start_header_id|>", result);
        Assert.DoesNotContain("<|eot_id|>", result);
    }
}
