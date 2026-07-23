using System.Collections.Concurrent;
using MagenticUIServer.Agents.Agents;
using MagenticUIServer.Agents.Models;

namespace MagenticUIServer.Agents.Tests;

/// <summary>
/// Phase 3B tests for the AgentSessionService session-management pattern.
///
/// AgentSessionService lives in MagenticUIServer (ASP.NET Core web project)
/// and depends on SignalR IClientProxy / IConfiguration, making it unsuitable
/// for direct unit testing in this project.  Instead, this file verifies the
/// core Phase 3B behaviour — the ConcurrentDictionary&lt;string, ActiveSession&gt;
/// paired with UserProxyAgent.SetResponse — using a lean TestableSessionManager
/// that mirrors exactly the same wiring used by AgentSessionService.
/// </summary>
public sealed class AgentSessionServicePhase3BTests
{
    // ── Minimal session manager that mirrors AgentSessionService Phase 3B logic ──

    private sealed record ActiveSession(CancellationTokenSource Cts, UserProxyAgent UserProxy);

    private sealed class TestableSessionManager
    {
        private readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();

        /// <summary>Creates (or replaces) a session for <paramref name="sessionId"/>.</summary>
        public void StartSession(string sessionId)
        {
            // Cancel and dispose any prior session for this ID (mirrors the service)
            if (_sessions.TryRemove(sessionId, out var old))
            {
                old.Cts.Cancel();
                old.Cts.Dispose();
            }

            var cts = new CancellationTokenSource();
            var proxy = new UserProxyAgent();
            _sessions[sessionId] = new ActiveSession(cts, proxy);
        }

        /// <summary>Mirrors AgentSessionService.RespondToInputAsync.</summary>
        public void RespondToInput(string sessionId, string response)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
                session.UserProxy.SetResponse(response);
        }

        /// <summary>Mirrors AgentSessionService.CancelTaskAsync.  Returns true if session existed.</summary>
        public bool CancelSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.Cts.Cancel();
                session.Cts.Dispose();
                return true;
            }
            return false;
        }

        public bool HasSession(string sessionId) => _sessions.ContainsKey(sessionId);

        public UserProxyAgent? GetUserProxy(string sessionId) =>
            _sessions.TryGetValue(sessionId, out var s) ? s.UserProxy : null;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void RespondToInput_OnNonExistentSession_IsNoOp_NoException()
    {
        // Arrange
        var manager = new TestableSessionManager();

        // Act & Assert — unknown session ID must never throw
        var ex = Record.Exception(() =>
            manager.RespondToInput("nonexistent-session-id", "any response"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task RespondToInput_CallsSetResponseOnCorrectUserProxyAgent()
    {
        // Arrange — two independent sessions
        var manager = new TestableSessionManager();
        manager.StartSession("session-A");
        manager.StartSession("session-B");

        var proxyA = manager.GetUserProxy("session-A")!;
        var messages = new List<AgentMessage>();
        var progress = new Progress<AgentMessage>(msg => messages.Add(msg));

        // UserProxy for session-A starts waiting for human input
        var waitTask = proxyA.ExecuteAsync("Confirm deployment?", progress);
        await Task.Delay(50);

        // Act — client sends a response for session-A (session-B is untouched)
        manager.RespondToInput("session-A", "confirmed");
        var result = await waitTask;

        // Assert
        Assert.Equal("confirmed", result);
    }

    [Fact]
    public void StartSession_CreatesEntry_CanBeRetrievedAndCancelled()
    {
        // Arrange
        var manager = new TestableSessionManager();

        // Act — start a session
        manager.StartSession("my-session");

        // Assert — session exists
        Assert.True(manager.HasSession("my-session"));

        // Act — cancel
        var cancelled = manager.CancelSession("my-session");

        // Assert — removed
        Assert.True(cancelled);
        Assert.False(manager.HasSession("my-session"));
    }

    [Fact]
    public void StartSession_WhenSessionAlreadyExists_ReplacesIt()
    {
        // Arrange
        var manager = new TestableSessionManager();
        manager.StartSession("session-X");
        var firstProxy = manager.GetUserProxy("session-X");

        // Act — start again with same ID (simulates reconnection)
        manager.StartSession("session-X");
        var secondProxy = manager.GetUserProxy("session-X");

        // Assert — session still present; new UserProxyAgent instance created
        Assert.True(manager.HasSession("session-X"));
        Assert.NotSame(firstProxy, secondProxy);
    }

    [Fact]
    public void CancelSession_OnNonExistentSession_ReturnsFalse_NoException()
    {
        // Arrange
        var manager = new TestableSessionManager();

        // Act
        var ex = Record.Exception(() => manager.CancelSession("no-such-session"));
        var returned = manager.CancelSession("also-missing");

        // Assert
        Assert.Null(ex);
        Assert.False(returned);
    }

    [Fact]
    public async Task EndToEnd_UserProxyWaiting_RespondToInputResolvesItWithCorrectValue()
    {
        // Arrange — mirrors the full Phase 3B round-trip:
        //   1. Orchestrator calls UserProxy.ExecuteAsync (waiting for human)
        //   2. SignalR hub receives InputResponse → AgentSessionService.RespondToInputAsync
        //   3. UserProxy.SetResponse resolves the TCS
        var manager = new TestableSessionManager();
        manager.StartSession("hub-connection-1");

        var proxy = manager.GetUserProxy("hub-connection-1")!;
        var messages = new List<AgentMessage>();
        var progress = new Progress<AgentMessage>(msg => messages.Add(msg));

        // Act — UserProxy is blocked waiting for human response
        var pendingResponse = proxy.ExecuteAsync("What is the target deadline?", progress);
        await Task.Delay(50);

        // Simulate: client → SignalR hub → AgentSessionService.RespondToInputAsync
        manager.RespondToInput("hub-connection-1", "end of day today");

        var answer = await pendingResponse;

        // Assert
        Assert.Equal("end of day today", answer);
        var msg = Assert.Single(messages);
        Assert.Equal("input_request", msg.Role);
        Assert.Equal("What is the target deadline?", msg.Text);
    }
}
