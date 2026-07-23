using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Qwen3 format: Uses ChatML-style tokens with Qwen3-specific tool calling conventions.
/// Key differences from Qwen2.5 (QwenFormatter):
/// - Tool definitions use &lt;tools&gt;/&lt;/tools&gt; XML tags, one JSON object per line.
/// - Tool call history uses no "id" field in the JSON.
/// - Tool results use &lt;tool_response&gt;/&lt;/tool_response&gt; XML tags.
/// - Generation prompt includes non-thinking marker: &lt;think&gt;\n\n&lt;/think&gt;\n\n
/// </summary>
internal sealed class Qwen3Formatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        var sb = new StringBuilder();
        var toolsList = tools?.ToList();
        var hasTools = toolsList is { Count: > 0 };

        foreach (var message in messages)
        {
            var role = MapRole(message.Role);

            // For system messages with tools, inject Qwen3-format tool definitions
            if (message.Role == ChatRole.System && hasTools)
            {
                var systemContent = message.Text ?? "You are a helpful assistant.";
                sb.Append($"<|im_start|>system\n{systemContent}\n\n");
                AppendToolDefinitions(sb, toolsList!);
                sb.Append("<|im_end|>\n");
                continue;
            }

            // Handle assistant messages with function calls
            if (message.Role == ChatRole.Assistant)
            {
                var content = FormatAssistantMessage(message);
                sb.Append($"<|im_start|>{role}\n{content}<|im_end|>\n");
                continue;
            }

            // Handle tool result messages (ChatRole.Tool — real agent loop path)
            // Emits <|im_start|>tool per Qwen3 spec (role.Value = "tool").
            if (message.Role == ChatRole.Tool)
            {
                var content = FormatToolResultMessage(message);
                sb.Append($"<|im_start|>{role}\n{content}<|im_end|>\n");
                continue;
            }

            // Handle user messages — may contain FunctionResultContent
            // (ChatRole.User path used in unit tests and backward-compat scenarios)
            if (message.Role == ChatRole.User)
            {
                var content = FormatUserMessage(message);
                sb.Append($"<|im_start|>{role}\n{content}<|im_end|>\n");
                continue;
            }

            // Default message formatting
            var defaultContent = message.Text ?? string.Empty;
            sb.Append($"<|im_start|>{role}\n{defaultContent}<|im_end|>\n");
        }

        // If we have tools but no system message, inject tool definitions at the start
        if (hasTools && !messages.Any(m => m.Role == ChatRole.System))
        {
            var toolsPrompt = new StringBuilder();
            toolsPrompt.Append("<|im_start|>system\nYou are a helpful assistant.\n\n");
            AppendToolDefinitions(toolsPrompt, toolsList!);
            toolsPrompt.Append("<|im_end|>\n");
            sb.Insert(0, toolsPrompt.ToString());
        }

        // Signal the model to generate an assistant response.
        // Non-thinking mode: the empty <think> block suppresses chain-of-thought,
        // which is required for reliable tool calling (e.g. MagenticBrain).
        sb.Append("<|im_start|>assistant\n<think>\n\n</think>\n\n");

        return sb.ToString();
    }

    private static void AppendToolDefinitions(StringBuilder sb, IList<AITool> tools)
    {
        sb.Append("# Tools\n\n");
        sb.Append("You may call one or more functions to assist with the user query.\n\n");
        sb.Append("You are provided with function signatures within <tools></tools> XML tags:\n");
        sb.Append("<tools>\n");

        foreach (var tool in tools.OfType<AIFunction>())
        {
            var parameters = tool.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Undefined
                ? (object)tool.JsonSchema
                : new { type = "object", properties = new { } };

            var def = new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description ?? "",
                    parameters
                }
            };

            sb.AppendLine(JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = false }));
        }

        sb.Append("</tools>\n\n");
        sb.Append("For each function call, return a json object with function name and arguments within <tool_call></tool_call> XML tags:\n");
        sb.Append("<tool_call>\n{\"name\": <function-name>, \"arguments\": <args-json-object>}\n</tool_call>\n");
    }

    private static string FormatAssistantMessage(ChatMessage message)
    {
        var parts = new List<string>();

        // Add text content if present
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            parts.Add(message.Text);
        }

        // Add function calls (Qwen3: no "id" field)
        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent funcCall)
            {
                var callJson = new
                {
                    name = funcCall.Name,
                    arguments = funcCall.Arguments
                };
                parts.Add($"<tool_call>\n{JsonSerializer.Serialize(callJson)}\n</tool_call>");
            }
        }

        return string.Join("\n", parts);
    }

    // ChatRole.Tool path: used by the real agent loop (samples).
    // Emits <|im_start|>tool per Qwen3 spec.
    private static string FormatToolResultMessage(ChatMessage message)
    {
        return FormatFunctionResults(message);
    }

    // ChatRole.User path: used by unit tests and backward-compat callers that
    // pack FunctionResultContent into user-role messages. The role token emitted
    // is still <|im_start|>user (caller determines the outer token via MapRole).
    private static string FormatUserMessage(ChatMessage message)
    {
        var parts = new List<string>();

        // Add text content if present
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            parts.Add(message.Text);
        }

        parts.AddRange(FormatFunctionResultParts(message));

        return string.Join("\n", parts);
    }

    private static string FormatFunctionResults(ChatMessage message)
    {
        var parts = FormatFunctionResultParts(message).ToList();
        return string.Join("\n", parts);
    }

    private static IEnumerable<string> FormatFunctionResultParts(ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent funcResult)
            {
                var resultText = funcResult.Exception is not null
                    ? $"Error: {funcResult.Exception.Message}"
                    : (funcResult.Result?.ToString() ?? "null");

                yield return $"<tool_response>\n{resultText}\n</tool_response>";
            }
        }
    }

    private static string MapRole(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        return role.Value;
    }
}
