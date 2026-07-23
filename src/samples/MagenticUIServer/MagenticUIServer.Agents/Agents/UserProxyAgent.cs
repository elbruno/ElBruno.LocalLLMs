using MagenticUIServer.Agents.Models;

namespace MagenticUIServer.Agents.Agents;

/// <summary>
/// Relays user clarification requests back to the orchestration loop.
///
/// Phase 3B: integrates TaskCompletionSource&lt;string&gt; wired to the SignalR hub
/// so a human can respond from the browser (human-in-the-loop, Decision 7 of ADR Phase 3).
/// </summary>
public sealed class UserProxyAgent
{
    private const string AgentName = "UserProxy";
    private TaskCompletionSource<string>? _pending;

    /// <summary>
    /// Emits an input_request message and waits until SetResponse() is called
    /// (by the SignalR hub via AgentSessionService) or ct is cancelled.
    /// </summary>
    public async Task<string> ExecuteAsync(
        string clarificationRequest,
        IProgress<AgentMessage> progress,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _pending = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation so the TCS doesn't hang if the task is cancelled
        using var reg = ct.Register(() => _pending.TrySetCanceled(ct));

        // Signal the frontend that user input is needed
        progress.Report(new AgentMessage(
            AgentName, "input_request", clarificationRequest, Round: 0, DateTimeOffset.UtcNow));

        try
        {
            return await _pending.Task;
        }
        catch (OperationCanceledException)
        {
            return "[Cancelled]";
        }
    }

    /// <summary>
    /// Called by AgentSessionService when the user submits a response via SignalR.
    /// </summary>
    public void SetResponse(string response) =>
        _pending?.TrySetResult(response);
}
