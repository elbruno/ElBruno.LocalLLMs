using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Phi-3 format: &lt;|system|&gt;\ncontent&lt;|end|&gt;\n&lt;|user|&gt;\ncontent&lt;|end|&gt;\n&lt;|assistant|&gt;\n
/// Used by Phi-3, Phi-3.5, and Phi-4 models.
/// </summary>
internal sealed class Phi3Formatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        // TODO: Implement tool support for Phi-3 format
        var sb = new StringBuilder();

        foreach (var message in messages)
        {
            var tag = MapRoleTag(message.Role);
            var content = message.Text ?? string.Empty;

            sb.Append($"<|{tag}|>\n{content}<|end|>\n");
        }

        // Signal the model to generate an assistant response
        sb.Append("<|assistant|>\n");

        return sb.ToString();
    }

    private static string MapRoleTag(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        return role.Value;
    }
}
