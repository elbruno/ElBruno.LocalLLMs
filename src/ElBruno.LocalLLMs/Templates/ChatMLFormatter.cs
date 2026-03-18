using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// ChatML format: &lt;|im_start|&gt;role\ncontent&lt;|im_end|&gt;
/// Used by many models including ChatML-trained variants.
/// </summary>
internal sealed class ChatMLFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        var sb = new StringBuilder();

        foreach (var message in messages)
        {
            var role = MapRole(message.Role);
            var content = message.Text ?? string.Empty;

            sb.Append($"<|im_start|>{role}\n{content}<|im_end|>\n");
        }

        // Signal the model to generate an assistant response
        sb.Append("<|im_start|>assistant\n");

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
