using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Gemma format: &lt;start_of_turn&gt;role\ncontent&lt;end_of_turn&gt;
/// Used by Google Gemma and Gemma 2 models.
/// </summary>
internal sealed class GemmaFormatter : IChatTemplateFormatter
{
    public string FormatMessages(IList<ChatMessage> messages)
    {
        return FormatMessages(messages, tools: null);
    }

    public string FormatMessages(IList<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        var sb = new StringBuilder();
        string? systemContent = null;
        var toolsList = tools?.ToList();
        var hasTools = toolsList is { Count: > 0 };

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                // Gemma doesn't have a native system role — prepend to first user message
                systemContent = message.Text ?? string.Empty;
                if (hasTools)
                {
                    systemContent += "\n\nYou have access to the following tools:\n\n";
                    systemContent += FormatToolDefinitions(toolsList!);
                    systemContent += "\n\nWhen you need to call a tool, respond with a JSON object in this format:\n";
                    systemContent += "{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}";
                }
                continue;
            }

            var role = message.Role == ChatRole.Assistant ? "model" : "user";

            // Handle assistant messages with function calls
            if (message.Role == ChatRole.Assistant)
            {
                var content = FormatAssistantMessage(message);
                sb.Append($"<start_of_turn>{role}\n{content}<end_of_turn>\n");
                continue;
            }

            // Handle user messages with function results
            if (message.Role == ChatRole.User)
            {
                var content = FormatUserMessage(message);

                if (systemContent != null)
                {
                    content = $"{systemContent}\n\n{content}";
                    systemContent = null;
                }

                sb.Append($"<start_of_turn>{role}\n{content}<end_of_turn>\n");
                continue;
            }

            // Default message formatting
            var defaultContent = message.Text ?? string.Empty;

            if (systemContent != null && role == "user")
            {
                defaultContent = $"{systemContent}\n\n{defaultContent}";
                systemContent = null;
            }

            sb.Append($"<start_of_turn>{role}\n{defaultContent}<end_of_turn>\n");
        }

        // If we have tools in system but haven't injected yet, add as first user turn
        if (systemContent != null)
        {
            var toolsPrompt = $"<start_of_turn>user\n{systemContent}<end_of_turn>\n";
            sb.Insert(0, toolsPrompt);
        }

        // Signal the model to generate
        sb.Append("<start_of_turn>model\n");

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
