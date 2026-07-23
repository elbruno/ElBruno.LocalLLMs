namespace MagenticUIServer.Agents.Models;

/// <summary>
/// Holds all state for a single agent orchestration session.
/// One session per browser connection (connection ID = session ID).
/// </summary>
public sealed class AgentSession : IDisposable
{
    public string SessionId { get; init; } = string.Empty;
    public TaskRequest? CurrentTask { get; set; }
    public List<AgentMessage> History { get; } = [];
    public CancellationTokenSource Cts { get; } = new();
    public void Dispose() => Cts.Dispose();
}
