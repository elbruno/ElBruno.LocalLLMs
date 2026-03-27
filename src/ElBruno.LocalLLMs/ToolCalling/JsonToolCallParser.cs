using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Parses tool calls from various JSON-based formats:
/// - Qwen-style: &lt;tool_call&gt;{"name": "fn", "arguments": {...}}&lt;/tool_call&gt;
/// - ChatML-style: plain JSON {"name": "fn", "arguments": {...}}
/// - Array format: [{"name": "fn1", ...}, {"name": "fn2", ...}]
/// </summary>
internal sealed class JsonToolCallParser : IToolCallParser
{
    private static readonly Regex ToolCallTagPattern = new(
        @"<tool_call>\s*(.*?)\s*</tool_call>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public IReadOnlyList<ParsedToolCall> Parse(string responseText)
    {
        ArgumentNullException.ThrowIfNull(responseText);

        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        var results = new List<ParsedToolCall>();

        // Strategy 1: Extract from <tool_call> tags
        var tagMatches = ToolCallTagPattern.Matches(responseText);
        if (tagMatches.Count > 0)
        {
            foreach (Match match in tagMatches)
            {
                var json = match.Groups[1].Value.Trim();
                var parsed = TryParseToolCallJson(json, match.Value);
                if (parsed is not null)
                    results.Add(parsed);
            }

            if (results.Count > 0)
                return results;
        }

        // Strategy 2: Try parsing the entire text as a JSON array of tool calls
        var trimmed = responseText.Trim();
        if (trimmed.StartsWith('['))
        {
            var arrayResults = TryParseToolCallArray(trimmed);
            if (arrayResults.Count > 0)
                return arrayResults;
        }

        // Strategy 3: Try parsing the entire text as a single JSON tool call object
        if (trimmed.StartsWith('{'))
        {
            var parsed = TryParseToolCallJson(trimmed, trimmed);
            if (parsed is not null)
                return [parsed];
        }

        return [];
    }

    private static ParsedToolCall? TryParseToolCallJson(string json, string rawText)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("name", out var nameElement))
                return null;

            var functionName = nameElement.GetString();
            if (string.IsNullOrEmpty(functionName))
                return null;

            var arguments = new Dictionary<string, object?>();
            if (root.TryGetProperty("arguments", out var argsElement) &&
                argsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in argsElement.EnumerateObject())
                {
                    arguments[prop.Name] = ConvertJsonElement(prop.Value);
                }
            }

            return new ParsedToolCall(
                CallId: GenerateCallId(),
                FunctionName: functionName,
                Arguments: arguments,
                RawText: rawText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<ParsedToolCall> TryParseToolCallArray(string json)
    {
        var results = new List<ParsedToolCall>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var elementJson = element.GetRawText();
                var parsed = TryParseToolCallJson(elementJson, elementJson);
                if (parsed is not null)
                    results.Add(parsed);
            }
        }
        catch (JsonException)
        {
            // Malformed array — return whatever we parsed so far
        }

        return results;
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
        _ => element.GetRawText()
    };

    private static string GenerateCallId() => $"call_{Guid.NewGuid().ToString("N")[..12]}";
}
