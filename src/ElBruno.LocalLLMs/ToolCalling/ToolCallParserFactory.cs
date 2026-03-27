namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Factory for creating tool call parsers based on chat template format.
/// </summary>
internal static class ToolCallParserFactory
{
    /// <summary>
    /// Creates a parser appropriate for the given chat template format.
    /// Currently all formats use JSON parsing.
    /// </summary>
    public static IToolCallParser Create(ChatTemplateFormat format)
    {
        // For now, all formats use JSON-based tool calling
        // Future: could have format-specific parsers (e.g., ChatML-specific, Llama3-specific)
        return new JsonToolCallParser();
    }
}
