using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// OpenAI Harmony format, used by GPT-OSS models. GPT-OSS was trained exclusively on
/// this format and will not behave correctly with any other prompt shape.
///
/// Structure:
/// <code>
/// &lt;|start|&gt;system&lt;|message|&gt;{identity}
/// Knowledge cutoff: 2024-06
/// Current date: {date}
///
/// Reasoning: {low|medium|high}
///
/// # Valid channels: analysis, commentary, final. ...&lt;|end|&gt;
/// &lt;|start|&gt;developer&lt;|message|&gt;# Instructions\n\n{system prompt}\n\n# Tools\n\n{ts namespace}&lt;|end|&gt;
/// &lt;|start|&gt;user&lt;|message|&gt;{text}&lt;|end|&gt;
/// &lt;|start|&gt;assistant
/// </code>
///
/// Notable differences from every other formatter in this library:
/// <list type="bullet">
/// <item>The caller's system prompt becomes the <c>developer</c> message, not the system message.
/// The system message is reserved for model metadata (reasoning level, valid channels).</item>
/// <item>Tool definitions are rendered as a TypeScript-style <c>namespace functions { ... }</c>
/// block, not JSON.</item>
/// <item>Assistant replies are split across channels; only the <c>final</c> channel is
/// user-facing. Prior-turn chain-of-thought is deliberately dropped on replay.</item>
/// <item>Tool calls are addressed with <c>to=functions.NAME</c> and terminated by <c>&lt;|call|&gt;</c>.</item>
/// <item>Tool results come back from a <c>functions.NAME</c> author on the commentary channel.</item>
/// </list>
/// </summary>
internal sealed class HarmonyFormatter : IChatTemplateFormatter
{
    private const string DefaultModelIdentity = "You are ChatGPT, a large language model trained by OpenAI.";
    private const string KnowledgeCutoff = "2024-06";

    private readonly ReasoningEffort _reasoningEffort;
    private readonly Func<DateTimeOffset> _clock;

    public HarmonyFormatter(ReasoningEffort reasoningEffort = ReasoningEffort.Medium)
        : this(reasoningEffort, static () => DateTimeOffset.UtcNow)
    {
    }

    // Clock seam so tests can assert on the rendered "Current date" line.
    internal HarmonyFormatter(ReasoningEffort reasoningEffort, Func<DateTimeOffset> clock)
    {
        _reasoningEffort = reasoningEffort;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string FormatMessages(IList<ChatMessage> messages) => FormatMessages(messages, tools: null);

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var functions = tools?.OfType<AIFunction>().ToList() ?? [];
        var hasTools = functions.Count > 0;

        var sb = new StringBuilder();

        AppendSystemMessage(sb, hasTools);

        // The first system message (if any) is lifted into the developer block.
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        var developerText = systemMessage?.Text;
        AppendDeveloperMessage(sb, developerText, functions);

        // Track the most recent tool call name: Harmony tool results are authored by
        // "functions.NAME", which is only recoverable from the preceding assistant call.
        string? lastToolCallName = null;

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                continue; // already rendered as the developer message
            }

            if (message.Role == ChatRole.User)
            {
                AppendUserMessage(sb, message, ref lastToolCallName);
                continue;
            }

            if (message.Role == ChatRole.Assistant)
            {
                AppendAssistantMessage(sb, message, ref lastToolCallName);
                continue;
            }

            if (message.Role == ChatRole.Tool)
            {
                AppendToolResults(sb, message, ref lastToolCallName);
                continue;
            }

            // Unknown roles fall back to a user turn rather than being dropped silently.
            if (!string.IsNullOrEmpty(message.Text))
            {
                sb.Append("<|start|>user<|message|>").Append(message.Text).Append("<|end|>");
            }
        }

        sb.Append("<|start|>assistant");
        return sb.ToString();
    }

    // ── System message (model metadata, never the caller's prompt) ─────────────

    private void AppendSystemMessage(StringBuilder sb, bool hasTools)
    {
        sb.Append("<|start|>system<|message|>");
        sb.Append(DefaultModelIdentity).Append('\n');
        sb.Append("Knowledge cutoff: ").Append(KnowledgeCutoff).Append('\n');
        sb.Append("Current date: ").Append(_clock().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append("\n\n");
        sb.Append("Reasoning: ").Append(ToHarmonyValue(_reasoningEffort)).Append("\n\n");
        sb.Append("# Valid channels: analysis, commentary, final. Channel must be included for every message.");

        if (hasTools)
        {
            sb.Append("\nCalls to these tools must go to the commentary channel: 'functions'.");
        }

        sb.Append("<|end|>");
    }

    internal static string ToHarmonyValue(ReasoningEffort effort)
    {
        // Harmony defines exactly three levels. The Microsoft.Extensions.AI enum has five,
        // so the extremes are clamped onto the nearest supported level.
        if (effort == ReasoningEffort.None || effort == ReasoningEffort.Low)
        {
            return "low";
        }

        if (effort == ReasoningEffort.High || effort == ReasoningEffort.ExtraHigh)
        {
            return "high";
        }

        return "medium";
    }

    // ── Developer message (instructions + tool namespace) ─────────────────────

    private static void AppendDeveloperMessage(StringBuilder sb, string? instructions, IList<AIFunction> functions)
    {
        var hasInstructions = !string.IsNullOrWhiteSpace(instructions);
        if (!hasInstructions && functions.Count == 0)
        {
            return;
        }

        sb.Append("<|start|>developer<|message|>");

        if (hasInstructions)
        {
            sb.Append("# Instructions\n\n").Append(instructions).Append("\n\n");
        }

        if (functions.Count > 0)
        {
            sb.Append("# Tools\n\n");
            AppendToolNamespace(sb, "functions", functions);
        }

        sb.Append("<|end|>");
    }

    /// <summary>
    /// Renders tools as a TypeScript-style namespace, matching the official Harmony template.
    /// </summary>
    private static void AppendToolNamespace(StringBuilder sb, string namespaceName, IList<AIFunction> functions)
    {
        sb.Append("## ").Append(namespaceName).Append("\n\n");
        sb.Append("namespace ").Append(namespaceName).Append(" {\n\n");

        foreach (var function in functions)
        {
            if (!string.IsNullOrWhiteSpace(function.Description))
            {
                sb.Append("// ").Append(function.Description).Append('\n');
            }

            sb.Append("type ").Append(function.Name).Append(" = ");

            var schema = function.JsonSchema;
            var hasProperties =
                schema.ValueKind == JsonValueKind.Object &&
                schema.TryGetProperty("properties", out var props) &&
                props.ValueKind == JsonValueKind.Object &&
                props.EnumerateObject().Any();

            if (!hasProperties)
            {
                sb.Append("() => any;\n\n");
                continue;
            }

            var required = GetRequiredNames(schema);
            sb.Append("(_: {\n");

            foreach (var prop in schema.GetProperty("properties").EnumerateObject())
            {
                if (prop.Value.TryGetProperty("description", out var desc) &&
                    desc.ValueKind == JsonValueKind.String)
                {
                    sb.Append("// ").Append(desc.GetString()).Append('\n');
                }

                sb.Append(prop.Name);
                if (!required.Contains(prop.Name))
                {
                    sb.Append('?');
                }

                sb.Append(": ").Append(RenderTypeScriptType(prop.Value));

                if (prop.Value.TryGetProperty("default", out var def) && def.ValueKind != JsonValueKind.Undefined)
                {
                    sb.Append(", // default: ").Append(def.GetRawText());
                }
                else
                {
                    sb.Append(',');
                }

                sb.Append('\n');
            }

            sb.Append("}) => any;\n\n");
        }

        sb.Append("} // namespace ").Append(namespaceName);
    }

    private static HashSet<string> GetRequiredNames(JsonElement schema)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("required", out var req) &&
            req.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in req.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var name = item.GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        required.Add(name);
                    }
                }
            }
        }

        return required;
    }

    private static string RenderTypeScriptType(JsonElement paramSpec)
    {
        if (paramSpec.ValueKind != JsonValueKind.Object)
        {
            return "any";
        }

        // Enums render as a union of string literals.
        if (paramSpec.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var literals = enumValues.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? $"\"{v.GetString()}\"" : v.GetRawText())
                .ToList();

            if (literals.Count > 0)
            {
                return string.Join(" | ", literals);
            }
        }

        if (!paramSpec.TryGetProperty("type", out var typeElement))
        {
            return "any";
        }

        // JSON Schema allows a type array (e.g. ["string", "null"]).
        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            var names = typeElement.EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Select(t => MapScalar(t.GetString()))
                .ToList();

            return names.Count > 0 ? string.Join(" | ", names) : "any";
        }

        if (typeElement.ValueKind != JsonValueKind.String)
        {
            return "any";
        }

        var type = typeElement.GetString();
        if (type == "array")
        {
            if (paramSpec.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
            {
                var inner = RenderTypeScriptType(items);
                // Long or ambiguous unions collapse to any[], mirroring the reference template.
                return inner.Length > 50 ? "any[]" : inner + "[]";
            }

            return "any[]";
        }

        if (type == "object")
        {
            if (!paramSpec.TryGetProperty("properties", out var props) ||
                props.ValueKind != JsonValueKind.Object ||
                !props.EnumerateObject().Any())
            {
                return "object";
            }

            var required = GetRequiredNames(paramSpec);
            var parts = props.EnumerateObject()
                .Select(p => $"{p.Name}{(required.Contains(p.Name) ? "" : "?")}: {RenderTypeScriptType(p.Value)}");

            return "{\n" + string.Join(", ", parts) + "}";
        }

        return MapScalar(type);
    }

    private static string MapScalar(string? type) => type switch
    {
        "string" => "string",
        "number" or "integer" => "number",
        "boolean" => "boolean",
        "object" => "object",
        "null" => "null",
        _ => "any"
    };

    // ── Conversation turns ────────────────────────────────────────────────────

    private static void AppendUserMessage(StringBuilder sb, ChatMessage message, ref string? lastToolCallName)
    {
        // Some callers pack tool results into user-role messages; honour that shape.
        var results = message.Contents.OfType<FunctionResultContent>().ToList();
        if (results.Count > 0)
        {
            AppendFunctionResults(sb, results, ref lastToolCallName);

            if (string.IsNullOrEmpty(message.Text))
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(message.Text))
        {
            sb.Append("<|start|>user<|message|>").Append(message.Text).Append("<|end|>");
        }
    }

    private static void AppendAssistantMessage(StringBuilder sb, ChatMessage message, ref string? lastToolCallName)
    {
        var calls = message.Contents.OfType<FunctionCallContent>().ToList();

        if (calls.Count > 0)
        {
            foreach (var call in calls)
            {
                sb.Append("<|start|>assistant to=functions.").Append(call.Name)
                  .Append("<|channel|>commentary json<|message|>")
                  .Append(SerializeArguments(call.Arguments))
                  .Append("<|call|>");

                lastToolCallName = call.Name;
            }

            return;
        }

        // Prior-turn chain-of-thought is intentionally dropped: the reference template
        // only replays the analysis channel during training, never during inference.
        var text = message.Text ?? string.Empty;
        sb.Append("<|start|>assistant<|channel|>final<|message|>").Append(text).Append("<|end|>");
        lastToolCallName = null;
    }

    private static void AppendToolResults(StringBuilder sb, ChatMessage message, ref string? lastToolCallName)
    {
        var results = message.Contents.OfType<FunctionResultContent>().ToList();
        if (results.Count > 0)
        {
            AppendFunctionResults(sb, results, ref lastToolCallName);
            return;
        }

        // Plain-text tool message with no structured result content.
        if (!string.IsNullOrEmpty(message.Text))
        {
            var author = lastToolCallName ?? "tool";
            sb.Append("<|start|>functions.").Append(author)
              .Append(" to=assistant<|channel|>commentary<|message|>")
              .Append(JsonSerializer.Serialize(message.Text))
              .Append("<|end|>");
        }
    }

    private static void AppendFunctionResults(StringBuilder sb, IList<FunctionResultContent> results, ref string? lastToolCallName)
    {
        foreach (var result in results)
        {
            var author = lastToolCallName ?? "tool";
            var text = result.Exception is not null
                ? $"Error: {result.Exception.Message}"
                : result.Result?.ToString() ?? "null";

            sb.Append("<|start|>functions.").Append(author)
              .Append(" to=assistant<|channel|>commentary<|message|>")
              .Append(JsonSerializer.Serialize(text))
              .Append("<|end|>");
        }
    }

    private static string SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return "{}";
        }

        try
        {
            return JsonSerializer.Serialize(arguments);
        }
        catch (NotSupportedException)
        {
            return "{}";
        }
    }
}
