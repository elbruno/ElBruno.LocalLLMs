namespace MagenticUIServer.Agents.Models;

/// <summary>
/// Describes a task submitted to the MagenticUI orchestrator.
/// </summary>
public sealed record TaskRequest(
    string TaskId,
    string Prompt,
    string? WorkingDirectory = null);
