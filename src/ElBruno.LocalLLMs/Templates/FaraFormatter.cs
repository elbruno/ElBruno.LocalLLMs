using System.Text;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Fara1.5 VLM format — Qwen3 ChatML with Qwen-VL vision token injection.
/// Vision tokens (vision_start/image_pad/vision_end) are injected once
/// in the first user message when images are present. No tool calling.
/// </summary>
internal sealed class FaraFormatter : IChatTemplateFormatter
{
    private const string DefaultSystemContent =
        "You are Fara, a vision-language assistant. You can analyze images and answer questions about their content. " +
        "For UI screenshots, you can identify interactive elements and provide precise coordinate-based actions.";

    public string FormatMessages(IList<ChatMessage> messages)
        => FormatMessagesWithImages(messages, hasImages: false);

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
        => FormatMessages(messages);

    /// <summary>
    /// Formats messages with optional vision token injection for the first user message.
    /// Called by LocalVisionChatClient with the actual image presence flag.
    /// </summary>
    internal string FormatMessagesWithImages(IList<ChatMessage> messages, bool hasImages)
    {
        var sb = new StringBuilder();
        var systemEmitted = false;
        var firstUserMessageSeen = false;

        // Emit system message first
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage is not null)
        {
            var content = systemMessage.Text ?? DefaultSystemContent;
            sb.Append($"<|im_start|>system\n{content}<|im_end|>\n");
            systemEmitted = true;
        }

        if (!systemEmitted)
        {
            sb.Append($"<|im_start|>system\n{DefaultSystemContent}<|im_end|>\n");
        }

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
                continue;

            if (message.Role == ChatRole.User)
            {
                var userText = message.Text ?? string.Empty;

                if (!firstUserMessageSeen && hasImages)
                {
                    sb.Append($"<|im_start|>user\n<|vision_start|><|image_pad|><|vision_end|>\n{userText}<|im_end|>\n");
                    firstUserMessageSeen = true;
                }
                else
                {
                    sb.Append($"<|im_start|>user\n{userText}<|im_end|>\n");
                    firstUserMessageSeen = true;
                }

                continue;
            }

            if (message.Role == ChatRole.Assistant)
            {
                var assistantText = message.Text ?? string.Empty;
                sb.Append($"<|im_start|>assistant\n{assistantText}<|im_end|>\n");
                continue;
            }

            // Other roles: format generically
            var roleValue = message.Role.Value;
            var text = message.Text ?? string.Empty;
            sb.Append($"<|im_start|>{roleValue}\n{text}<|im_end|>\n");
        }

        // Generation prompt — no <think> block; Fara is not a reasoning model
        sb.Append("<|im_start|>assistant\n");

        return sb.ToString();
    }
}
