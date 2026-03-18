using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Llama 3 format: &lt;|begin_of_text|&gt;&lt;|start_header_id|&gt;role&lt;|end_header_id|&gt;\n\ncontent&lt;|eot_id|&gt;
/// Used by Meta Llama 3 and Llama 3.1/3.2 models.
/// </summary>
internal sealed class Llama3Formatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.Append("<|begin_of_text|>");

        foreach (var message in messages)
        {
            var role = MapRole(message.Role);
            var content = message.Text ?? string.Empty;

            sb.Append($"<|start_header_id|>{role}<|end_header_id|>\n\n{content}<|eot_id|>");
        }

        // Signal the model to generate an assistant response
        sb.Append("<|start_header_id|>assistant<|end_header_id|>\n\n");

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
