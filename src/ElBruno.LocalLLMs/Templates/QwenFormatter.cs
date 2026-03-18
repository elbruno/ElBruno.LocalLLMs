using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Qwen format: Uses ChatML-style with &lt;|im_start|&gt;/&lt;|im_end|&gt; tokens.
/// Qwen and Qwen2.5 models use ChatML format.
/// </summary>
internal sealed class QwenFormatter : IChatTemplateFormatter
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
