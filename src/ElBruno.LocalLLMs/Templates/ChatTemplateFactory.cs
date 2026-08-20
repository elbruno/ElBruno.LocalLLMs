using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Resolves the correct formatter based on ChatTemplateFormat.
/// </summary>
internal static class ChatTemplateFactory
{
    internal static IChatTemplateFormatter Create(ChatTemplateFormat format) =>
        Create(format, ReasoningEffort.Medium);

    /// <summary>
    /// Creates a formatter, passing model-specific generation preferences where the
    /// format supports them. <paramref name="reasoningEffort"/> is only consumed by
    /// <see cref="ChatTemplateFormat.Harmony"/>; all other formats ignore it.
    /// </summary>
    internal static IChatTemplateFormatter Create(ChatTemplateFormat format, ReasoningEffort reasoningEffort) => format switch
    {
        ChatTemplateFormat.ChatML => new ChatMLFormatter(),
        ChatTemplateFormat.Phi3 => new Phi3Formatter(),
        ChatTemplateFormat.Llama3 => new Llama3Formatter(),
        ChatTemplateFormat.Qwen => new QwenFormatter(),
        ChatTemplateFormat.Qwen3 => new Qwen3Formatter(),
        ChatTemplateFormat.Fara => new FaraFormatter(),
        ChatTemplateFormat.Mistral => new MistralFormatter(),
        ChatTemplateFormat.DeepSeek => new DeepSeekFormatter(),
        ChatTemplateFormat.Gemma => new GemmaFormatter(),
        ChatTemplateFormat.Harmony => new HarmonyFormatter(reasoningEffort),
        ChatTemplateFormat.Custom => new ChatMLFormatter(),   // Custom fallback to ChatML
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported chat template format: {format}")
    };
}
