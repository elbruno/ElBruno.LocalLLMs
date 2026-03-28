using Microsoft.Extensions.AI;

namespace McpToolRouting;

/// <summary>
/// Uses a local LLM to distill complex user prompts into a single-sentence intent.
/// This reduces noise before semantic tool routing, improving match quality.
/// </summary>
public static class PromptDistiller
{
    private const string SystemPrompt =
        "Extract the user's primary intent in a single sentence. " +
        "Be specific about what action or information is requested. " +
        "Do not add any explanation or commentary — output only the distilled sentence.";

    /// <summary>
    /// Distills a potentially complex user prompt into a single-sentence intent
    /// suitable for semantic tool matching.
    /// </summary>
    public static async Task<string> DistillIntentAsync(
        IChatClient client,
        string userPrompt,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await client.GetResponseAsync(
            messages,
            new ChatOptions { MaxOutputTokens = 128, Temperature = 0.1f },
            ct);

        var distilled = response.Text?.Trim() ?? userPrompt;

        // Fallback to original prompt if distillation produced empty or very short result
        return distilled.Length < 5 ? userPrompt : distilled;
    }
}
