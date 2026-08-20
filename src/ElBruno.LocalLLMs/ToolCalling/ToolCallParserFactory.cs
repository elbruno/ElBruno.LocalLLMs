namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Factory for creating tool call parsers based on chat template format.
/// </summary>
internal static class ToolCallParserFactory
{
    /// <summary>
    /// Creates a parser appropriate for the given chat template format.
    /// </summary>
    public static IToolCallParser Create(ChatTemplateFormat format)
    {
        // Harmony (GPT-OSS) emits tool calls on the commentary channel with a
        // to=functions.NAME recipient, which the JSON parser cannot read.
        if (format == ChatTemplateFormat.Harmony)
        {
            return new HarmonyToolCallParser();
        }

        // All other formats use JSON-based tool calling
        return new JsonToolCallParser();
    }
}
