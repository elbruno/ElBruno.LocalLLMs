namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Resolves the correct formatter based on ChatTemplateFormat.
/// </summary>
internal static class ChatTemplateFactory
{
    internal static IChatTemplateFormatter Create(ChatTemplateFormat format) => format switch
    {
        ChatTemplateFormat.ChatML => new ChatMLFormatter(),
        ChatTemplateFormat.Phi3 => new Phi3Formatter(),
        ChatTemplateFormat.Llama3 => new Llama3Formatter(),
        ChatTemplateFormat.Qwen => new QwenFormatter(),
        ChatTemplateFormat.Mistral => new MistralFormatter(),
        ChatTemplateFormat.DeepSeek => new ChatMLFormatter(), // DeepSeek uses ChatML-style
        ChatTemplateFormat.Gemma => new ChatMLFormatter(),    // Gemma fallback to ChatML
        ChatTemplateFormat.Custom => new ChatMLFormatter(),   // Custom fallback to ChatML
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported chat template format: {format}")
    };
}
