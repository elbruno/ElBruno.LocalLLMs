using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Llama 3 format: &lt;|begin_of_text|&gt;&lt;|start_header_id|&gt;role&lt;|end_header_id|&gt;\n\ncontent&lt;|eot_id|&gt;
/// Used by Meta Llama 3 and Llama 3.1/3.2 models.
/// </summary>
internal sealed class Llama3Formatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        var sb = new StringBuilder();
        sb.Append("<|begin_of_text|>");

        var toolsList = tools?.ToList();
        var hasTools = toolsList is { Count: > 0 };

        foreach (var message in messages)
        {
            var role = MapRole(message.Role);

            // For system messages with tools, inject tool definitions
            if (message.Role == ChatRole.System && hasTools)
            {
                var systemContent = message.Text ?? "You are a helpful assistant.";
                sb.Append($"<|start_header_id|>{role}<|end_header_id|>\n\n{systemContent}\n\n");
                sb.Append("You have access to the following tools:\n\n");
                sb.Append(FormatToolDefinitions(toolsList!));
                sb.Append("\n\nWhen you need to call a tool, respond with a JSON object in this format:\n");
                sb.Append("{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}\n");
                sb.Append("<|eot_id|>");
                continue;
            }

            // Handle assistant messages with function calls
            if (message.Role == ChatRole.Assistant)
            {
                var content = FormatAssistantMessage(message);
                sb.Append($"<|start_header_id|>{role}<|end_header_id|>\n\n{content}<|eot_id|>");
                continue;
            }

            // Handle user messages with function results
            if (message.Role == ChatRole.User)
            {
                var content = FormatUserMessage(message);
                sb.Append($"<|start_header_id|>{role}<|end_header_id|>\n\n{content}<|eot_id|>");
                continue;
            }

            // Default message formatting
            var defaultContent = message.Text ?? string.Empty;
            sb.Append($"<|start_header_id|>{role}<|end_header_id|>\n\n{defaultContent}<|eot_id|>");
        }

        // If we have tools but no system message, inject tool definitions at the start
        if (hasTools && !messages.Any(m => m.Role == ChatRole.System))
        {
            var toolsPrompt = new StringBuilder();
            toolsPrompt.Append("<|start_header_id|>system<|end_header_id|>\n\n");
            toolsPrompt.Append("You are a helpful assistant with access to the following tools:\n\n");
            toolsPrompt.Append(FormatToolDefinitions(toolsList!));
            toolsPrompt.Append("\n\nWhen you need to call a tool, respond with a JSON object in this format:\n");
            toolsPrompt.Append("{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}\n");
            toolsPrompt.Append("<|eot_id|>");
            sb.Insert("<|begin_of_text|>".Length, toolsPrompt.ToString());
        }

        // Signal the model to generate an assistant response
        sb.Append("<|start_header_id|>assistant<|end_header_id|>\n\n");

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

        // Add function calls as JSON (Llama uses JSON format)
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

    private static string MapRole(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.User) return "user";
        if (role == ChatRole.Assistant) return "assistant";
        return role.Value;
    }
}
