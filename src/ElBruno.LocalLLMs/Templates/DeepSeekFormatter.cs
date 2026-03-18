using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// DeepSeek format used by DeepSeek-R1 distilled models.
/// Uses ChatML-style tokens with DeepSeek-specific begin/end of sentence markers.
/// </summary>
internal sealed class DeepSeekFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.Append("<｜begin▁of▁sentence｜>");

        foreach (var message in messages)
        {
            var role = MapRole(message.Role);
            var content = message.Text ?? string.Empty;

            sb.Append($"<｜{role}｜>\n{content}");

            if (message.Role != ChatRole.Assistant || message != messages[^1])
            {
                sb.Append("<｜end▁of▁sentence｜>");
            }

            sb.Append('\n');
        }

        // Signal the model to generate an assistant response
        sb.Append("<｜assistant｜>\n");

        return sb.ToString();
    }

    private static string MapRole(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        return role.Value;
    }
}
