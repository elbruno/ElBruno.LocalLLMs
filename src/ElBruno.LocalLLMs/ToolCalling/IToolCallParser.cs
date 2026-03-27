namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Parses tool calls from model response text.
/// </summary>
internal interface IToolCallParser
{
    /// <summary>
    /// Parse tool calls from the model's response text.
    /// </summary>
    /// <param name="responseText">Raw response text from the model</param>
    /// <returns>List of parsed tool calls (empty if none found)</returns>
    IReadOnlyList<ParsedToolCall> Parse(string responseText);
}
