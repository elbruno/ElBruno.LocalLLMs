using MagenticUIServer.Agents.Models;
using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Agents;

/// <summary>
/// Agent wrapper for FileSurferTool.
/// Receives a natural-language instruction, determines the appropriate file operation,
/// and executes it via FileSurferTool, reporting the result via IProgress.
/// </summary>
public sealed class FileSurferAgent
{
    private readonly FileSurferTool _tool;
    private const string AgentName = "FileSurfer";

    public FileSurferAgent(FileSurferTool tool)
    {
        _tool = tool;
    }

    /// <summary>
    /// Executes a file operation based on the given instruction.
    /// Instruction keywords:
    ///   "read" / "open"  → ReadFile
    ///   "write" / "save" → WriteFile (expects "path|||content" format)
    ///   anything else    → ListDirectory
    /// </summary>
    public async Task<string> ExecuteAsync(
        string instruction,
        IProgress<AgentMessage> progress,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Report(progress, "tool", $"Instruction: {instruction}", round: 0);

        var lower = instruction.ToLowerInvariant();

        string result;
        if (lower.StartsWith("read") || lower.StartsWith("open"))
        {
            var path = ExtractFirstArg(instruction);
            result = _tool.ReadFile(path);
        }
        else if (lower.StartsWith("write") || lower.StartsWith("save"))
        {
            // Convention: "write path|||content"
            var parts = instruction.Split("|||", 2);
            var path = ExtractFirstArg(parts[0]);
            var content = parts.Length > 1 ? parts[1] : "";
            _tool.WriteFile(path, content);
            result = $"Written: {path}";
        }
        else
        {
            // Default: list
            var dir = ExtractFirstArg(instruction);
            result = _tool.ListDirectory(string.IsNullOrWhiteSpace(dir) ? null : dir);
        }

        await Task.CompletedTask;
        Report(progress, "tool", result, round: 0);
        return result;
    }

    private void Report(IProgress<AgentMessage> progress, string role, string text, int round) =>
        progress.Report(new AgentMessage(AgentName, role, text, round, DateTimeOffset.UtcNow));

    // Extracts the first whitespace-delimited token after the verb (e.g. "read foo.txt" → "foo.txt")
    private static string ExtractFirstArg(string instruction)
    {
        var parts = instruction.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }
}
