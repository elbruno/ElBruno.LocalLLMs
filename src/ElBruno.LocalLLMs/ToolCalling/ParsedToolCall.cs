namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Represents a parsed tool call extracted from model output.
/// </summary>
/// <param name="CallId">Unique identifier for this tool call (auto-generated if not in model output)</param>
/// <param name="FunctionName">Name of the function to call</param>
/// <param name="Arguments">Dictionary of argument name to value</param>
/// <param name="RawText">Original text from which this call was parsed (for debugging)</param>
internal record ParsedToolCall(
    string CallId,
    string FunctionName,
    IDictionary<string, object?> Arguments,
    string? RawText
);
