using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Tests for <see cref="ChatTemplateFactory"/> — ensures correct formatter
/// is created for each <see cref="ChatTemplateFormat"/> enum value.
/// </summary>
public class ChatTemplateFactoryTests
{
    // ──────────────────────────────────────────────
    // Factory returns non-null for all known formats
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.Custom)]
    public void Create_KnownFormat_ReturnsNonNull(ChatTemplateFormat format)
    {
        var formatter = ChatTemplateFactory.Create(format);

        Assert.NotNull(formatter);
    }

    // ──────────────────────────────────────────────
    // Each format produces distinct output
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_ChatML_ProducesChatMLOutput()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);
        var result = FormatSimple(formatter);

        Assert.Contains("<|im_start|>", result);
        Assert.Contains("<|im_end|>", result);
    }

    [Fact]
    public void Create_Phi3_ProducesPhi3Output()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Phi3);
        var result = FormatSimple(formatter);

        Assert.Contains("<|user|>", result);
        Assert.Contains("<|end|>", result);
    }

    [Fact]
    public void Create_Llama3_ProducesLlama3Output()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Llama3);
        var result = FormatSimple(formatter);

        Assert.Contains("<|begin_of_text|>", result);
        Assert.Contains("<|start_header_id|>", result);
        Assert.Contains("<|eot_id|>", result);
    }

    [Fact]
    public void Create_Qwen_ProducesOutput()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Qwen);
        var result = FormatSimple(formatter);

        Assert.Contains("Hello", result);
        Assert.Contains("assistant", result);
    }

    [Fact]
    public void Create_Mistral_ProducesMistralOutput()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Mistral);
        var result = FormatSimple(formatter);

        Assert.Contains("[INST]", result);
        Assert.Contains("[/INST]", result);
    }

    [Fact]
    public void Create_DeepSeek_FallsThroughToChatML()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.DeepSeek);
        var result = FormatSimple(formatter);

        Assert.Contains("<|im_start|>", result);
        Assert.Contains("<|im_end|>", result);
    }

    [Fact]
    public void Create_Gemma_FallsThroughToChatML()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);
        var result = FormatSimple(formatter);

        Assert.Contains("<|im_start|>", result);
    }

    [Fact]
    public void Create_Custom_FallsThroughToChatML()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Custom);
        var result = FormatSimple(formatter);

        Assert.Contains("<|im_start|>", result);
    }

    // ──────────────────────────────────────────────
    // Different formats produce different output
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_DifferentFormats_ProduceDifferentOutput()
    {
        var chatml = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);
        var phi3 = ChatTemplateFactory.Create(ChatTemplateFormat.Phi3);
        var llama3 = ChatTemplateFactory.Create(ChatTemplateFormat.Llama3);
        var mistral = ChatTemplateFactory.Create(ChatTemplateFormat.Mistral);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Hello")
        };

        var chatMLResult = chatml.FormatMessages(messages);
        var phi3Result = phi3.FormatMessages(messages);
        var llama3Result = llama3.FormatMessages(messages);
        var mistralResult = mistral.FormatMessages(messages);

        // All four major formats must produce different strings
        Assert.NotEqual(chatMLResult, phi3Result);
        Assert.NotEqual(chatMLResult, llama3Result);
        Assert.NotEqual(chatMLResult, mistralResult);
        Assert.NotEqual(phi3Result, llama3Result);
        Assert.NotEqual(phi3Result, mistralResult);
        Assert.NotEqual(llama3Result, mistralResult);
    }

    // ──────────────────────────────────────────────
    // Same format, same input → same output (determinism)
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.Custom)]
    public void Create_SameFormatSameInput_ProducesSameOutput(ChatTemplateFormat format)
    {
        var formatter1 = ChatTemplateFactory.Create(format);
        var formatter2 = ChatTemplateFactory.Create(format);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "System prompt."),
            new(ChatRole.User, "User message.")
        };

        var result1 = formatter1.FormatMessages(messages);
        var result2 = formatter2.FormatMessages(messages);

        Assert.Equal(result1, result2);
    }

    // ──────────────────────────────────────────────
    // All formatters handle the IChatTemplateFormatter contract
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.Custom)]
    public void Create_AllFormatters_ImplementInterface(ChatTemplateFormat format)
    {
        var formatter = ChatTemplateFactory.Create(format);

        Assert.IsAssignableFrom<IChatTemplateFormatter>(formatter);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static string FormatSimple(IChatTemplateFormatter formatter) =>
        formatter.FormatMessages(new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        });
}
