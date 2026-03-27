using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Phi-3 format: &lt;|system|&gt;\ncontent&lt;|end|&gt;\n&lt;|user|&gt;\ncontent&lt;|end|&gt;\n&lt;|assistant|&gt;\n
/// Used by Phi-3, Phi-3.5, and Phi-4 models.
/// </summary>
internal sealed class Phi3Formatter : IChatTemplateFormatter
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
            var tag = MapRoleTag(message.Role);

            // For system messages with tools, inject tool definitions
            if (message.Role == ChatRole.System && hasTools)
            {
                var systemContent = message.Text ?? "You are a helpful assistant.";
                sb.Append($"<|{tag}|>\n{systemContent}\n\n");
                sb.Append("You have access to the following tools:\n\n");
                sb.Append(FormatToolDefinitions(toolsList!));
                sb.Append("\n\nWhen you need to call a tool, respond with a JSON object in this format:\n");
                sb.Append("{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}\n");
                sb.Append("<|end|>\n");
                continue;
            }

            // Handle assistant messages with function calls
            if (message.Role == ChatRole.Assistant)
            {
                var content = FormatAssistantMessage(message);
                sb.Append($"<|{tag}|>\n{content}<|end|>\n");
                continue;
            }

            // Handle user messages with function results
            if (message.Role == ChatRole.User)
            {
                var content = FormatUserMessage(message);
                sb.Append($"<|{tag}|>\n{content}<|end|>\n");
                continue;
            }

            // Default message formatting
            var defaultContent = message.Text ?? string.Empty;
            sb.Append($"<|{tag}|>\n{defaultContent}<|end|>\n");
        }

        // If we have tools but no system message, inject tool definitions at the start
        if (hasTools && !messages.Any(m => m.Role == ChatRole.System))
        {
            var toolsPrompt = new StringBuilder();
            toolsPrompt.Append("<|system|>\n");
            toolsPrompt.Append("You are a helpful assistant with access to the following tools:\n\n");
            toolsPrompt.Append(FormatToolDefinitions(toolsList!));
            toolsPrompt.Append("\n\nWhen you need to call a tool, respond with a JSON object in this format:\n");
            toolsPrompt.Append("{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}\n");
            toolsPrompt.Append("<|end|>\n");
            sb.Insert(0, toolsPrompt.ToString());
        }

        // Signal the model to generate an assistant response
        sb.Append("<|assistant|>\n");

        return sb.ToString();
    }

    private static string FormatToolDefinitions(IList<AITool> tools)
    {
        var toolDefs = new List<object>();
        foreach (var tool in tools)
        {
            if (tool is AIFunction func)
            {
                var parameters = func.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Undefined
                    ? (object)func.JsonSchema
                    : new { type = "object", properties = new { } };
                var def = new
                {
                    type = "function",
                    function = new
                    {
                        name = func.Name,
                        description = func.Description ?? "",
                        parameters
                    }
                };
                toolDefs.Add(def);
            }
        }

        return JsonSerializer.Serialize(toolDefs, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatAssistantMessage(ChatMessage message)
    {
        var parts = new List<string>();

        // Add text content if present
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            parts.Add(message.Text);
        }

        // Add function calls as JSON
        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent funcCall)
            {
                var callJson = new
                {
                    name = funcCall.Name,
                    arguments = funcCall.Arguments
                };
                parts.Add(JsonSerializer.Serialize(callJson));
            }
        }

        return string.Join("\n", parts);
    }

    private static string FormatUserMessage(ChatMessage message)
    {
        var parts = new List<string>();

        // Add text content if present
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            parts.Add(message.Text);
        }

        // Add function results
        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent funcResult)
            {
                var resultText = funcResult.Exception is not null
                    ? $"Error: {funcResult.Exception.Message}"
                    : (funcResult.Result?.ToString() ?? "null");

                parts.Add($"Tool result: {resultText}");
            }
        }

        return string.Join("\n", parts);
    }

    private static string MapRoleTag(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        return role.Value;
    }
}
