using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Formats IList&lt;ChatMessage&gt; into the model's expected prompt format.
/// </summary>
internal interface IChatTemplateFormatter
{
    string FormatMessages(IList<ChatMessage> messages);
}
