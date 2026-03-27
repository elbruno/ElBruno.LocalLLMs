using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Mistral format: [INST] user message [/INST] assistant response
/// Used by Mistral and Mixtral models.
/// </summary>
internal sealed class MistralFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        var sb = new StringBuilder();
        string? systemPrompt = null;
        var toolsList = tools?.ToList();
        var hasTools = toolsList is { Count: > 0 };

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                systemPrompt = message.Text;
                if (hasTools)
                {
                    systemPrompt += "\n\nYou have access to the following tools:\n\n";
                    systemPrompt += FormatToolDefinitions(toolsList!);
                    systemPrompt += "\n\nWhen you need to call a tool, respond with a JSON object in this format:\n";
                    systemPrompt += "{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}";
                }
                continue;
            }

            if (message.Role == ChatRole.User)
            {
                sb.Append("[INST] ");

                // Prepend system prompt to the first user message
                if (systemPrompt is not null)
                {
                    sb.Append(systemPrompt).Append("\n\n");
                    systemPrompt = null;
                }

                sb.Append(FormatUserMessage(message));
                sb.Append(" [/INST]");
            }
            else if (message.Role == ChatRole.Assistant)
            {
                sb.Append(FormatAssistantMessage(message));
            }
        }

        // If we have tools but no system message was added to first user message
        if (hasTools && systemPrompt is not null)
        {
            // Prepend to the start
            var toolsPrompt = "[INST] ";
            toolsPrompt += systemPrompt;
            toolsPrompt += " [/INST]";
            sb.Insert(0, toolsPrompt);
        }

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

        return parts.Count > 0 ? string.Join("\n", parts) : string.Empty;
    }
}
