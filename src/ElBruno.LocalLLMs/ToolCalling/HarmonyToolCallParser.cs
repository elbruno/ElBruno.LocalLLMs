using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.LocalLLMs.ToolCalling;

/// <summary>
/// Parses Harmony (GPT-OSS) tool calls, which are emitted on the commentary channel
/// addressed to a function in the <c>functions</c> namespace:
/// <code>
/// &lt;|start|&gt;assistant to=functions.get_weather&lt;|channel|&gt;commentary json&lt;|message|&gt;{"city":"Paris"}&lt;|call|&gt;
/// </code>
/// The <c>to=</c> recipient may also appear after the channel declaration, so both
/// orderings are recognised. The trailing terminator may be <c>&lt;|call|&gt;</c>,
/// <c>&lt;|end|&gt;</c>, <c>&lt;|return|&gt;</c>, or end-of-text when generation was truncated.
/// </summary>
internal sealed class HarmonyToolCallParser : IToolCallParser
{
    // to=functions.NAME ...<|message|>{args}   (recipient before the channel marker).
    // The span between the recipient and <|message|> may contain other control markers
    // such as <|channel|>commentary or <|constrain|>json, so it is matched permissively.
    private static readonly Regex RecipientFirstPattern = new(
        @"to=functions\.(?<name>[A-Za-z0-9_\-\.]+)(?<sep>(?:(?!<\|message\|>|<\|call\|>|<\|end\|>|<\|start\|>).)*)<\|message\|>(?<args>.*?)(?=<\|call\|>|<\|end\|>|<\|return\|>|<\|start\|>|$)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // <|channel|>commentary to=functions.NAME ...<|message|>{args}  (recipient after the channel marker)
    private static readonly Regex ChannelFirstPattern = new(
        @"<\|channel\|>(?:(?!<\|message\|>).)*?to=functions\.(?<name>[A-Za-z0-9_\-\.]+)(?:(?!<\|message\|>).)*<\|message\|>(?<args>.*?)(?=<\|call\|>|<\|end\|>|<\|return\|>|<\|start\|>|$)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public IReadOnlyList<ParsedToolCall> Parse(string responseText)
    {
        ArgumentNullException.ThrowIfNull(responseText);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return [];
        }

        var results = new List<ParsedToolCall>();
        var seen = new HashSet<int>();

        foreach (var pattern in new[] { RecipientFirstPattern, ChannelFirstPattern })
        {
            foreach (Match match in pattern.Matches(responseText))
            {
                // Both patterns can match the same call; de-duplicate by position.
                if (!seen.Add(match.Groups["name"].Index))
                {
                    continue;
                }

                var name = match.Groups["name"].Value;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var arguments = ParseArguments(match.Groups["args"].Value.Trim());

                results.Add(new ParsedToolCall(
                    CallId: GenerateCallId(),
                    FunctionName: name,
                    Arguments: arguments,
                    RawText: match.Value));
            }
        }

        return results;
    }

    private static Dictionary<string, object?> ParseArguments(string json)
    {
        var arguments = new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return arguments;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    arguments[prop.Name] = ConvertJsonElement(prop.Value);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed or truncated arguments — surface the call with no arguments
            // rather than dropping the model's intent entirely.
        }

        return arguments;
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        // Cast to object explicitly: without it the ternary unifies long into double,
        // and integer arguments would surface as floating-point values.
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
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
