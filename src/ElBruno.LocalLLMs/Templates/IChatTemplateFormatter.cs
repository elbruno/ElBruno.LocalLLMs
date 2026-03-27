using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Formats IList&lt;ChatMessage&gt; into the model's expected prompt format.
/// </summary>
internal interface IChatTemplateFormatter
{
    string FormatMessages(IList<ChatMessage> messages);

    /// <summary>
    /// Formats messages with optional tool definitions.
    /// </summary>
    /// <param name="messages">Chat history</param>
    /// <param name="tools">Available tools (null/empty = no tools)</param>
    /// <returns>Formatted prompt string</returns>
    string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools);
}
