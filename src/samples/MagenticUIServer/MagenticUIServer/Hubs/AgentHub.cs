using Microsoft.AspNetCore.SignalR;
using MagenticUIServer.Services;

namespace MagenticUIServer.Hubs;

public sealed class AgentHub : Hub
{
    private readonly AgentSessionService _sessions;

    public AgentHub(AgentSessionService sessions) => _sessions = sessions;

    public async Task SubmitTask(TaskRequest request)
    {
        var sessionId = Context.ConnectionId;
        await _sessions.StartTaskAsync(sessionId, request, Clients.Caller, Context.ConnectionAborted);
    }

    public async Task CancelTask(string taskId)
    {
        await _sessions.CancelTaskAsync(Context.ConnectionId);
        await Clients.Caller.SendAsync("TaskError", taskId, "Cancelled by user.");
    }

    /// <summary>
    /// Called by the client when the user submits a response to a UserProxy input request.
    /// </summary>
    public async Task InputResponse(string response)
    {
        await _sessions.RespondToInputAsync(Context.ConnectionId, response);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _sessions.CancelTaskAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed record TaskRequest(string TaskId, string Prompt, string? WorkingDirectory);
