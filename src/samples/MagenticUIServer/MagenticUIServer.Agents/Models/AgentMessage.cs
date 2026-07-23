namespace MagenticUIServer.Agents.Models;

/// <summary>
/// Represents a single message emitted by an agent during orchestration.
/// Streamed to callers via IProgress&lt;AgentMessage&gt;.
/// </summary>
public sealed record AgentMessage(
    string AgentName,
    string Role,        // "assistant" | "tool" | "system" | "user"
    string Text,
    int Round,
    DateTimeOffset Timestamp);
