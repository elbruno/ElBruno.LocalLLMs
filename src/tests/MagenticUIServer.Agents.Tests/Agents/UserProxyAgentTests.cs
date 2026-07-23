using MagenticUIServer.Agents.Agents;
using MagenticUIServer.Agents.Models;

namespace MagenticUIServer.Agents.Tests.Agents;

public sealed class UserProxyAgentTests
{
    // ── Helper: collect Progress<AgentMessage> callbacks into a list ─────────

    private static List<AgentMessage> CollectMessages(out IProgress<AgentMessage> progress)
    {
        var list = new List<AgentMessage>();
        progress = new Progress<AgentMessage>(msg => list.Add(msg));
        return list;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EmitsExactlyOneInputRequestMessage_BeforeBlocking()
    {
        // Arrange
        var sut = new UserProxyAgent();
        var messages = CollectMessages(out var progress);

        // Act — start without resolving so we can inspect mid-flight
        var executeTask = sut.ExecuteAsync("Do you confirm?", progress);
        await Task.Delay(80); // let the Progress<> thread-pool callback fire

        // Assert — exactly one message emitted before the task resolves
        Assert.Single(messages);

        // Clean up — unblock the task
        sut.SetResponse("done");
        await executeTask;
    }

    [Fact]
    public async Task ExecuteAsync_SetResponse_ReturnsExpectedString()
    {
        // Arrange
        var sut = new UserProxyAgent();
        var messages = CollectMessages(out var progress);

        // Act
        var executeTask = sut.ExecuteAsync("What is your name?", progress);
        await Task.Delay(30);
        sut.SetResponse("hello");
        var result = await executeTask;

        // Assert
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ExecuteAsync_CancelToken_ReturnsCancelledString()
    {
        // Arrange
        var sut = new UserProxyAgent();
        var messages = CollectMessages(out var progress);
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = sut.ExecuteAsync("Some question?", progress, cts.Token);
        await Task.Delay(30); // ensure ExecuteAsync is awaiting the TCS
        cts.Cancel();
        var result = await executeTask;

        // Assert
        Assert.Equal("[Cancelled]", result);
    }

    [Fact]
    public void SetResponse_WhenNoPendingRequest_DoesNotThrow()
    {
        // Arrange
        var sut = new UserProxyAgent();

        // Act & Assert — calling SetResponse before ExecuteAsync should be safe
        var ex = Record.Exception(() => sut.SetResponse("nobody is listening"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ExecuteAsync_TwoSequentialCalls_BothResolveIndependently()
    {
        // Arrange
        var sut = new UserProxyAgent();
        var messages = CollectMessages(out var progress);

        // Act — first call
        var task1 = sut.ExecuteAsync("First question?", progress);
        await Task.Delay(30);
        sut.SetResponse("first-answer");
        var result1 = await task1;

        // Act — second call
        var task2 = sut.ExecuteAsync("Second question?", progress);
        await Task.Delay(30);
        sut.SetResponse("second-answer");
        var result2 = await task2;

        // Assert
        Assert.Equal("first-answer", result1);
        Assert.Equal("second-answer", result2);
        Assert.Equal(2, messages.Count); // one input_request per call
    }

    [Fact]
    public async Task ExecuteAsync_EmittedMessage_HasCorrectRoleAndText()
    {
        // Arrange
        var sut = new UserProxyAgent();
        var messages = CollectMessages(out var progress);
        const string clarification = "Please confirm the operation.";

        // Act
        var executeTask = sut.ExecuteAsync(clarification, progress);
        await Task.Delay(80); // let Progress<> callback fire
        sut.SetResponse("ok");
        await executeTask;

        // Assert
        var msg = Assert.Single(messages);
        Assert.Equal("input_request", msg.Role);
        Assert.Equal(clarification, msg.Text);
    }
}
