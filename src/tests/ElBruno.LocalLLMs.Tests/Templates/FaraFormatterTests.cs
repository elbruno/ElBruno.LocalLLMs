using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for the Fara1.5-9B VLM template formatter.
/// FaraFormatter uses ChatML tokens with optional vision token injection:
///   - Vision tokens: &lt;|vision_start|&gt;&lt;|image_pad|&gt;&lt;|vision_end|&gt;
///   - NO &lt;think&gt; block in generation prompt
///   - Tools parameter is silently ignored
/// </summary>
[Trait("Category", "Fara")]
public class FaraFormatterTests
{
    private readonly FaraFormatter _formatter = new();

    // ──────────────────────────────────────────────
    // Group 1: Basic formatting (no images)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_SingleUserMessage_ContainsChatMLTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>user\n", result);
        Assert.Contains("Hello", result);
        Assert.Contains("<|im_end|>\n", result);
    }

    [Fact]
    public void FormatMessages_SingleUserMessage_NoVisionTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.DoesNotContain("<|vision_start|>", result);
        Assert.DoesNotContain("<|image_pad|>", result);
        Assert.DoesNotContain("<|vision_end|>", result);
    }

    [Fact]
    public void FormatMessages_SystemAndUser_SystemBlockAppearsFirst()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        var systemIdx = result.IndexOf("<|im_start|>system\n", StringComparison.Ordinal);
        var userIdx = result.IndexOf("<|im_start|>user\n", StringComparison.Ordinal);

        Assert.True(systemIdx >= 0, "System block should be present");
        Assert.True(userIdx > systemIdx, "User block should appear after system block");
        Assert.Contains("You are a helpful assistant.", result);
    }

    [Fact]
    public void FormatMessages_NoSystem_InjectsDefaultSystemPrompt()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>system\n", result);
        Assert.Contains("Fara", result);
    }

    [Fact]
    public void FormatMessages_GenerationSuffix_IsPlainAssistantWithoutThink()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.EndsWith("<|im_start|>assistant\n", result);
        Assert.DoesNotContain("<think>", result);
        Assert.DoesNotContain("</think>", result);
    }

    [Fact]
    public void FormatMessages_MultiTurn_AllRolesFormatted()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are Fara."),
            new(ChatRole.User, "Q1"),
            new(ChatRole.Assistant, "A1"),
            new(ChatRole.User, "Q2")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>system\n", result);
        Assert.Contains("<|im_start|>user\nQ1<|im_end|>\n", result);
        Assert.Contains("<|im_start|>assistant\n", result);
        Assert.Contains("A1", result);
        Assert.Contains("<|im_start|>user\nQ2<|im_end|>\n", result);
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    // ──────────────────────────────────────────────
    // Group 2: Vision token injection (FormatMessagesWithImages)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessagesWithImages_HasImagesTrue_ContainsVisionTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Describe this image")
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: true);

        Assert.Contains("<|vision_start|>", result);
        Assert.Contains("<|image_pad|>", result);
        Assert.Contains("<|vision_end|>", result);
    }

    [Fact]
    public void FormatMessagesWithImages_HasImagesFalse_NoVisionTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Just text")
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: false);

        Assert.DoesNotContain("<|vision_start|>", result);
        Assert.DoesNotContain("<|image_pad|>", result);
        Assert.DoesNotContain("<|vision_end|>", result);
    }

    [Fact]
    public void FormatMessagesWithImages_HasImagesTrue_VisionTokensBeforeFirstUserText()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Describe this")
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: true);

        var visionStartIdx = result.IndexOf("<|vision_start|>", StringComparison.Ordinal);
        var textIdx = result.IndexOf("Describe this", StringComparison.Ordinal);

        Assert.True(visionStartIdx >= 0, "Vision start token must be present");
        Assert.True(visionStartIdx < textIdx, "Vision tokens must precede the user text");
    }

    [Fact]
    public void FormatMessagesWithImages_HasImagesTrue_VisionTokensOnlyInFirstUserMessage()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "First question"),
            new(ChatRole.Assistant, "First answer"),
            new(ChatRole.User, "Second question")
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: true);

        // Count occurrences of vision_start — must be exactly 1
        var count = CountOccurrences(result, "<|vision_start|>");
        Assert.Equal(1, count);
    }

    [Fact]
    public void FormatMessagesWithImages_HasImagesTrue_EmptyUserText_StillInjectsVisionTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, string.Empty)
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: true);

        Assert.Contains("<|vision_start|>", result);
        Assert.Contains("<|image_pad|>", result);
        Assert.Contains("<|vision_end|>", result);
    }

    [Fact]
    public void FormatMessagesWithImages_MultiTurnWithImages_OnlyFirstUserGetsVisionTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are Fara."),
            new(ChatRole.User, "Look at this image"),
            new(ChatRole.Assistant, "I see a cat"),
            new(ChatRole.User, "What color is it?")
        };

        var result = _formatter.FormatMessagesWithImages(messages, hasImages: true);

        // Vision tokens appear exactly once (first user turn only)
        Assert.Equal(1, CountOccurrences(result, "<|vision_start|>"));

        // Second user message must NOT contain vision tokens
        var secondUserIdx = result.LastIndexOf("<|im_start|>user\n", StringComparison.Ordinal);
        var afterSecondUser = result[secondUserIdx..];
        Assert.DoesNotContain("<|vision_start|>", afterSecondUser);
    }

    // ──────────────────────────────────────────────
    // Group 3: Assistant messages
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_AssistantMessage_CorrectTokens()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello there!")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>assistant\nHello there!<|im_end|>\n", result);
    }

    [Fact]
    public void FormatMessages_MultiTurnWithAssistant_CorrectRoundTrip()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 2+2?"),
            new(ChatRole.Assistant, "4"),
            new(ChatRole.User, "And 3+3?")
        };

        var result = _formatter.FormatMessages(messages);

        Assert.Contains("<|im_start|>user\nWhat is 2+2?<|im_end|>\n", result);
        Assert.Contains("<|im_start|>assistant\n4<|im_end|>\n", result);
        Assert.Contains("<|im_start|>user\nAnd 3+3?<|im_end|>\n", result);
        Assert.EndsWith("<|im_start|>assistant\n", result);
    }

    // ──────────────────────────────────────────────
    // Group 4: Tool parameter is ignored
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_WithTools_OutputIdenticalToNoTools()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are Fara."),
            new(ChatRole.User, "What's the weather?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        var resultWithTools = _formatter.FormatMessages(messages, new[] { tool });
        var resultNoTools = _formatter.FormatMessages(messages);

        Assert.Equal(resultNoTools, resultWithTools);
    }

    [Fact]
    public void FormatMessages_WithTools_NoToolXmlInjected()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Help me")
        };

        var tool = AIFunctionFactory.Create(
            (string x) => x,
            name: "do_thing",
            description: "Does a thing"
        );

        var result = _formatter.FormatMessages(messages, new[] { tool });

        Assert.DoesNotContain("<tools>", result);
        Assert.DoesNotContain("</tools>", result);
        Assert.DoesNotContain("<tool_call>", result);
        Assert.DoesNotContain("# Tools", result);
    }

    // ──────────────────────────────────────────────
    // Group 5: Backward compatibility (IChatTemplateFormatter interface)
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatMessages_IListOverload_Works()
    {
        IList<ChatMessage> messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        // Cast to interface to ensure the interface contract is satisfied
        IChatTemplateFormatter formatter = _formatter;
        var result = formatter.FormatMessages(messages);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("<|im_start|>user\n", result);
    }

    [Fact]
    public void FormatMessages_IListWithToolsOverload_Works()
    {
        IList<ChatMessage> messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var tool = AIFunctionFactory.Create(
            () => "result",
            name: "noop",
            description: "Does nothing"
        );

        IChatTemplateFormatter formatter = _formatter;
        var result = formatter.FormatMessages(messages, new[] { tool });

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ──────────────────────────────────────────────
    // Group 6: QwenFormatter unchanged (regression)
    // ──────────────────────────────────────────────

    [Fact]
    public void QwenFormatter_DoesNotContainVisionStartToken()
    {
        var qwenFormatter = new QwenFormatter();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Describe this image")
        };

        var result = qwenFormatter.FormatMessages(messages);

        Assert.DoesNotContain("<|vision_start|>", result);
    }

    [Fact]
    public void QwenFormatter_DoesNotContainFaraDefaultSystemPrompt()
    {
        var qwenFormatter = new QwenFormatter();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = qwenFormatter.FormatMessages(messages);

        // Fara's default system prompt contains "Fara" in a vision-language context
        // QwenFormatter must not inject it
        Assert.DoesNotContain("vision-language", result);
    }

    [Fact]
    public void FaraFormatter_IsDistinctType_NotQwenFormatter()
    {
        Assert.IsNotType<QwenFormatter>(_formatter);
        Assert.IsType<FaraFormatter>(_formatter);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
