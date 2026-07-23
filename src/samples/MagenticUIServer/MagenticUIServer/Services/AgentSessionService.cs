using System.Collections.Concurrent;
using ElBruno.LocalLLMs;
using MagenticUIServer.Agents.Agents;
using MagenticUIServer.Agents.Orchestrator;
using MagenticUIServer.Agents.Tools;
using MagenticUIServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgentModels = MagenticUIServer.Agents.Models;

namespace MagenticUIServer.Services;

public sealed class AgentSessionService
{
    private sealed record ActiveSession(
        CancellationTokenSource Cts,
        UserProxyAgent UserProxy);

    private readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();
    private readonly IConfiguration _config;
    private readonly ILogger<AgentSessionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentSessionService(
        IConfiguration config,
        ILogger<AgentSessionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public Task StartTaskAsync(
        string sessionId,
        TaskRequest request,
        IClientProxy caller,
        CancellationToken connectionToken)
    {
        // Cancel any existing task for this session
        if (_sessions.TryRemove(sessionId, out var old))
        {
            old.Cts.Cancel();
            old.Cts.Dispose();
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        var userProxy = new UserProxyAgent();
        _sessions[sessionId] = new ActiveSession(cts, userProxy);

        var modelPath = _config["ModelPath"];

        _ = Task.Run(async () =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    _logger.LogWarning(
                        "ModelPath not configured. Set 'ModelPath' in appsettings to enable real orchestration.");

                    await caller.SendAsync("AgentMessage", new
                    {
                        agentName = "System",
                        role = "system",
                        text = $"[STUB] Task received: {request.Prompt}. Set ModelPath in configuration to enable real orchestration.",
                        round = 0
                    }, cts.Token);

                    await Task.Delay(500, cts.Token);
                    await caller.SendAsync("TaskComplete", request.TaskId, "[STUB] Phase 3A stub response.");
                    return;
                }

                // Real orchestration
                using var chatClient = new LocalChatClient(
                    new LocalLLMsOptions { ModelPath = modelPath });

                var fileSurfer = new FileSurferTool(request.WorkingDirectory ?? ".");
                var webFetcher = new WebFetchTool(_httpClientFactory.CreateClient("WebFetcher"));
                var coder = new CodeExecutorTool();
                var orchestrator = new MagenticUIOrchestrator(
                    chatClient, fileSurfer, webFetcher, coder, userProxy);

                var agentRequest = new AgentModels.TaskRequest(
                    request.TaskId, request.Prompt, request.WorkingDirectory);

                var progress = new Progress<AgentModels.AgentMessage>(msg =>
                {
                    _ = caller.SendAsync("AgentMessage", new
                    {
                        agentName = msg.AgentName,
                        role = msg.Role,
                        text = msg.Text,
                        round = msg.Round
                    }, cts.Token);

                    if (msg.Role == "input_request")
                        _ = caller.SendAsync("InputRequest", msg.Text, cts.Token);
                });

                await orchestrator.RunAsync(agentRequest, progress, cts.Token);
                await caller.SendAsync("TaskComplete", request.TaskId, "Task completed.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orchestration failed for session {SessionId}", sessionId);
                try
                {
                    await caller.SendAsync("TaskError", request.TaskId, ex.Message);
                }
                catch { /* connection may already be gone */ }
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public Task RespondToInputAsync(string sessionId, string response)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.UserProxy.SetResponse(response);
        return Task.CompletedTask;
    }

    public Task CancelTaskAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Cts.Cancel();
            session.Cts.Dispose();
        }
        return Task.CompletedTask;
    }
}
