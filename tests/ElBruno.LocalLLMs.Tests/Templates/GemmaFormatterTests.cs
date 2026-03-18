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
}
