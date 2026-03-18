using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Mistral format: [INST] user message [/INST] assistant response
/// Used by Mistral and Mixtral models.
/// </summary>
internal sealed class MistralFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        string? systemPrompt = null;

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                systemPrompt = message.Text;
                continue;
            }

            if (message.Role == ChatRole.User)
            {
                sb.Append("[INST] ");

                // Prepend system prompt to the first user message
                if (systemPrompt is not null)
                {
                    sb.Append(systemPrompt).Append("\n\n");
                    systemPrompt = null;
                }

                sb.Append(message.Text ?? string.Empty);
                sb.Append(" [/INST]");
            }
            else if (message.Role == ChatRole.Assistant)
            {
                sb.Append(message.Text ?? string.Empty);
            }
        }

        return sb.ToString();
    }
}
