using System.Text.Json;
using MagenticUIServer.Agents.Models;

namespace MagenticUIServer.Agents.Tests.Models;

public sealed class AgentMessageTests
{
    private static readonly DateTimeOffset _ts = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AgentMessage_CanBeConstructed()
    {
        var msg = new AgentMessage("WebFetcher", "assistant", "Hello", 1, _ts);

        Assert.Equal("WebFetcher", msg.AgentName);
        Assert.Equal("assistant", msg.Role);
        Assert.Equal("Hello", msg.Text);
        Assert.Equal(1, msg.Round);
        Assert.Equal(_ts, msg.Timestamp);
    }

    [Fact]
    public void AgentMessage_RecordEquality_SameValuesAreEqual()
    {
        var msg1 = new AgentMessage("Agent", "user", "Hi", 0, _ts);
        var msg2 = new AgentMessage("Agent", "user", "Hi", 0, _ts);

        Assert.Equal(msg1, msg2);
    }

    [Fact]
    public void AgentMessage_RecordEquality_DifferentValuesAreNotEqual()
    {
        var msg1 = new AgentMessage("Agent", "user", "Hi", 0, _ts);
        var msg2 = new AgentMessage("Agent", "user", "Bye", 0, _ts);

        Assert.NotEqual(msg1, msg2);
    }

    [Fact]
    public void AgentMessage_RoundTripSerialization()
    {
        var original = new AgentMessage("Orchestrator", "system", "Starting task", 2, _ts);

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AgentMessage>(json);

        Assert.NotNull(restored);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void AgentMessage_WithExpression_ProducesModifiedCopy()
    {
        var original = new AgentMessage("A", "assistant", "text", 1, _ts);
        var modified = original with { Round = 2 };

        Assert.Equal(1, original.Round);
        Assert.Equal(2, modified.Round);
    }

    // ── TaskRequest ──────────────────────────────────────────────────────────

    [Fact]
    public void TaskRequest_CanBeConstructed()
    {
        var req = new TaskRequest("task-001", "Summarize the file", "/workspace");

        Assert.Equal("task-001", req.TaskId);
        Assert.Equal("Summarize the file", req.Prompt);
        Assert.Equal("/workspace", req.WorkingDirectory);
    }

    [Fact]
    public void TaskRequest_WorkingDirectoryIsNullByDefault()
    {
        var req = new TaskRequest("task-002", "Do something");

        Assert.Null(req.WorkingDirectory);
    }

    [Fact]
    public void TaskRequest_RecordEquality_SameValuesAreEqual()
    {
        var r1 = new TaskRequest("t1", "prompt", null);
        var r2 = new TaskRequest("t1", "prompt", null);

        Assert.Equal(r1, r2);
    }

    [Fact]
    public void TaskRequest_RecordEquality_DifferentIdNotEqual()
    {
        var r1 = new TaskRequest("t1", "prompt");
        var r2 = new TaskRequest("t2", "prompt");

        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void TaskRequest_RoundTripSerialization()
    {
        var original = new TaskRequest("task-json", "Serialize me", "/tmp");

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TaskRequest>(json);

        Assert.NotNull(restored);
        Assert.Equal(original, restored);
    }
}
