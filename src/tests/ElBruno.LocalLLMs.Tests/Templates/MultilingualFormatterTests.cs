using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

#pragma warning disable xUnit1026 // language parameter used for test display names

namespace ElBruno.LocalLLMs.Tests.Templates;

/// <summary>
/// Multilingual and charset tests for all chat template formatters.
/// Verifies that Unicode content in various scripts/languages is preserved
/// through the template formatting pipeline.
/// </summary>
public class MultilingualFormatterTests
{
    // ──────────────────────────────────────────────
    // Test data: prompts in different languages/scripts
    // ──────────────────────────────────────────────

    public static TheoryData<string, string> MultilingualPrompts => new()
    {
        // East Asian
        { "Chinese (Simplified)", "什么是量子计算？请用简单的语言解释。" },
        { "Chinese (Traditional)", "什麼是量子計算？請用簡單的語言解釋。" },
        { "Japanese", "量子コンピューティングとは何ですか？簡単に説明してください。" },
        { "Korean", "양자 컴퓨팅이란 무엇인가요? 간단히 설명해 주세요." },

        // Cyrillic
        { "Russian", "Что такое квантовые вычисления? Объясните простыми словами." },
        { "Ukrainian", "Що таке квантові обчислення? Поясніть простими словами." },

        // Arabic / Hebrew (RTL)
        { "Arabic", "ما هو الحوسبة الكمومية؟ اشرحها بكلمات بسيطة." },
        { "Hebrew", "מהי מחשוב קוונטי? הסבירו במילים פשוטות." },

        // Indic scripts
        { "Hindi (Devanagari)", "क्वांटम कंप्यूटिंग क्या है? सरल शब्दों में समझाइए।" },
        { "Tamil", "குவாண்டம் கம்ப்யூட்டிங் என்றால் என்ன? எளிய வார்த்தைகளில் விளக்கவும்." },
        { "Thai", "คอมพิวเตอร์ควอนตัมคืออะไร? อธิบายเป็นภาษาง่ายๆ" },

        // European with diacritics
        { "Spanish", "¿Qué es la computación cuántica? Explícalo con palabras sencillas." },
        { "French", "Qu'est-ce que l'informatique quantique ? Expliquez en termes simples." },
        { "German", "Was ist Quantencomputing? Erklären Sie es in einfachen Worten." },
        { "Portuguese", "O que é computação quântica? Explique com palavras simples." },
        { "Turkish", "Kuantum bilişim nedir? Basit kelimelerle açıklayın." },
        { "Polish", "Czym jest obliczanie kwantowe? Wyjaśnij to prostymi słowami." },

        // Special symbols and emoji
        { "Emoji", "What does 🧠💻🔬 mean in science? 🤔" },
        { "Math symbols", "Explain: ∑(i=0..n) = n(n+1)/2, where ∀n ∈ ℕ" },
        { "Mixed scripts", "Hello مرحبا こんにちは 你好 Привет 🌍" },
    };

    // ──────────────────────────────────────────────
    // Gemma formatter (used by Gemma 2, 3, 4)
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Gemma_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("<start_of_turn>user", result);
        Assert.EndsWith("<start_of_turn>model\n", result);
    }

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Gemma_SystemAndUser_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, content),
            new(ChatRole.User, "Respond in English.")
        };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
    }

    // ──────────────────────────────────────────────
    // ChatML formatter (SmolLM, DeepSeek-Coder, etc.)
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void ChatML_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("<|im_start|>user", result);
    }

    // ──────────────────────────────────────────────
    // Qwen formatter
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Qwen_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Qwen);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("<|im_start|>user", result);
    }

    // ──────────────────────────────────────────────
    // Llama 3 formatter
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Llama3_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Llama3);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("<|start_header_id|>user", result);
    }

    // ──────────────────────────────────────────────
    // Phi-3 formatter
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Phi3_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Phi3);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("<|user|>", result);
    }

    // ──────────────────────────────────────────────
    // Mistral formatter
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void Mistral_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Mistral);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
        Assert.Contains("[INST]", result);
    }

    // ──────────────────────────────────────────────
    // DeepSeek formatter
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultilingualPrompts))]
    public void DeepSeek_PreservesMultilingualContent(string language, string content)
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.DeepSeek);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
    }

    // ──────────────────────────────────────────────
    // Multi-turn conversations in non-Latin scripts
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    public void AllFormatters_MultiTurnCJK_PreservesContent(ChatTemplateFormat format)
    {
        var formatter = ChatTemplateFactory.Create(format);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "あなたは日本語アシスタントです。"),
            new(ChatRole.User, "東京の天気はどうですか？"),
            new(ChatRole.Assistant, "東京は晴れで、気温は25°Cです。"),
            new(ChatRole.User, "明日の予報を教えてください。")
        };

        var result = formatter.FormatMessages(messages);

        Assert.Contains("東京の天気はどうですか？", result);
        Assert.Contains("東京は晴れで、気温は25°Cです。", result);
        Assert.Contains("明日の予報を教えてください。", result);
    }

    [Theory]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    public void AllFormatters_MultiTurnArabic_PreservesContent(ChatTemplateFormat format)
    {
        var formatter = ChatTemplateFactory.Create(format);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "أنت مساعد مفيد باللغة العربية."),
            new(ChatRole.User, "ما هي عاصمة مصر؟"),
            new(ChatRole.Assistant, "عاصمة مصر هي القاهرة."),
            new(ChatRole.User, "كم عدد سكانها؟")
        };

        var result = formatter.FormatMessages(messages);

        Assert.Contains("ما هي عاصمة مصر؟", result);
        Assert.Contains("عاصمة مصر هي القاهرة.", result);
        Assert.Contains("كم عدد سكانها؟", result);
    }

    // ──────────────────────────────────────────────
    // Edge cases: mixed scripts, special chars
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    public void AllFormatters_ZeroWidthChars_PreservesContent(ChatTemplateFormat format)
    {
        // Zero-width characters: ZWJ, ZWSP, ZWNJ
        var content = "Test\u200Bwith\u200Czero\u200Dwidth\u00ADchars";
        var formatter = ChatTemplateFactory.Create(format);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
    }

    [Theory]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    public void AllFormatters_SurrogatePairEmoji_PreservesContent(ChatTemplateFormat format)
    {
        // Emoji that require surrogate pairs in UTF-16
        var content = "Family: 👨‍👩‍👧‍👦 Flag: 🇯🇵 Skin: 👋🏽";
        var formatter = ChatTemplateFactory.Create(format);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
    }

    [Theory]
    [InlineData(ChatTemplateFormat.Gemma)]
    [InlineData(ChatTemplateFormat.ChatML)]
    [InlineData(ChatTemplateFormat.Qwen)]
    [InlineData(ChatTemplateFormat.Llama3)]
    [InlineData(ChatTemplateFormat.Phi3)]
    [InlineData(ChatTemplateFormat.Mistral)]
    [InlineData(ChatTemplateFormat.DeepSeek)]
    public void AllFormatters_NewlinesAndWhitespace_PreservesContent(ChatTemplateFormat format)
    {
        var content = "Line 1\nLine 2\r\nLine 3\tTabbed";
        var formatter = ChatTemplateFactory.Create(format);
        var messages = new List<ChatMessage> { new(ChatRole.User, content) };

        var result = formatter.FormatMessages(messages);

        Assert.Contains(content, result);
    }
}
