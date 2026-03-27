using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Gemma format: &lt;start_of_turn&gt;role\ncontent&lt;end_of_turn&gt;
/// Used by Google Gemma and Gemma 2 models.
/// </summary>
internal sealed class GemmaFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        // TODO: Implement tool support for Gemma format
        var sb = new StringBuilder();
        string? systemContent = null;

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                // Gemma doesn't have a native system role — prepend to first user message
                systemContent = message.Text ?? string.Empty;
                continue;
            }

            var role = message.Role == ChatRole.Assistant ? "model" : "user";
            var content = message.Text ?? string.Empty;

            if (systemContent != null && role == "user")
            {
                content = $"{systemContent}\n\n{content}";
                systemContent = null;
            }

            sb.Append($"<start_of_turn>{role}\n{content}<end_of_turn>\n");
        }

        // Signal the model to generate
        sb.Append("<start_of_turn>model\n");

        return sb.ToString();
    }
}
