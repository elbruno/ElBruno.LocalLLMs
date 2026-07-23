using MagenticUIServer.Agents.Models;
using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Agents;

/// <summary>
/// Agent wrapper for WebFetchTool.
/// Extracts a URL from a natural-language instruction and fetches the page.
/// </summary>
public sealed class WebFetcherAgent
{
    private readonly WebFetchTool _tool;
    private const string AgentName = "WebFetcher";

    public WebFetcherAgent(WebFetchTool tool)
    {
        _tool = tool;
    }

    /// <summary>
    /// Extracts the first http/https URL from the instruction and fetches it.
    /// </summary>
    public async Task<string> ExecuteAsync(
        string instruction,
        IProgress<AgentMessage> progress,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var url = ExtractUrl(instruction);
        if (string.IsNullOrWhiteSpace(url))
        {
            var msg = $"No URL found in instruction: {instruction}";
            Report(progress, "tool", msg, round: 0);
            return msg;
        }

        Report(progress, "tool", $"Fetching: {url}", round: 0);
        var result = await _tool.FetchUrl(url);
        Report(progress, "tool", result.Length > 200 ? result[..200] + "..." : result, round: 0);
        return result;
    }

    private void Report(IProgress<AgentMessage> progress, string role, string text, int round) =>
        progress.Report(new AgentMessage(AgentName, role, text, round, DateTimeOffset.UtcNow));

    private static string ExtractUrl(string instruction)
    {
        // Look for the first token that starts with http:// or https://
        foreach (var token in instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return token.Trim('\'', '"', ',', '.');
        }
        return string.Empty;
    }
}
