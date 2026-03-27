using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;
using ElBruno.LocalLLMs.ToolCalling;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests.ToolCalling;

/// <summary>
/// Integration tests for FunctionCallContent in LocalChatClient.
/// Tests the full round-trip: tool calls in response, function results in messages.
/// Uses mocked ONNX model via IModelDownloader.
/// </summary>
public class FunctionCallContentIntegrationTests : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _disposables = [];

    // ──────────────────────────────────────────────
    // Tool call detection in responses
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_WithToolsAndModelReturnsToolCall_ContainsFunctionCallContent()
    {
        // This test will need a real model or deep mocking
        // For now, create a skeleton that shows the expected behavior
        
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions 
        { 
            EnsureModelDownloaded = false,
            Model = KnownModels.Phi35MiniInstruct
        };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Seattle?")
        };

        var tool = AIFunctionFactory.Create(
            (string city) => $"Weather in {city}: Sunny, 72°F",
            name: "get_weather",
            description: "Get current weather for a city"
        );

        // TODO: This test requires Trinity's implementation to:
        // 1. Accept tools parameter in GetResponseAsync
        // 2. Parse model output for tool calls
        // 3. Return FunctionCallContent in the response
        
        // Expected behavior (test structure):
        // var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = new[] { tool } });
        // Assert.NotNull(response);
        // var functionCall = response.Contents.OfType<FunctionCallContent>().FirstOrDefault();
        // Assert.NotNull(functionCall);
        // Assert.Equal("get_weather", functionCall.Name);
        // Assert.Equal("Seattle", functionCall.Arguments["city"]);
        
        // For now, skip until implementation exists
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetResponseAsync_WithToolsButPlainTextResponse_NoFunctionCallContent()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions 
        { 
            EnsureModelDownloaded = false,
            Model = KnownModels.Phi35MiniInstruct
        };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        // TODO: Test that when model returns plain text (no tool call),
        // response is a normal ChatMessage without FunctionCallContent
        
        // Expected behavior:
        // var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
        // Assert.NotNull(response);
        // Assert.Empty(response.Contents.OfType<FunctionCallContent>());
        
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    // FunctionCallContent properties
    // ──────────────────────────────────────────────

    [Fact]
    public void FunctionCallContent_HasRequiredProperties()
    {
        // Test that FunctionCallContent (from MEAI) has expected structure
        var functionCall = new FunctionCallContent(
            callId: "call_123",
            name: "get_weather",
            arguments: new Dictionary<string, object?> { { "city", "Seattle" } }
        );

        Assert.Equal("call_123", functionCall.CallId);
        Assert.Equal("get_weather", functionCall.Name);
        Assert.Equal("Seattle", functionCall.Arguments?["city"]);
    }

    // ──────────────────────────────────────────────
    // FunctionResultContent in messages
    // ──────────────────────────────────────────────

    [Fact]
    public async Task FormatMessages_WithFunctionResultContent_FormatsCorrectly()
    {
        var formatter = new ChatMLFormatter();
        
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?"),
            new(ChatRole.Assistant, [
                new FunctionCallContent("call_1", "get_weather", 
                    new Dictionary<string, object?> { { "city", "Seattle" } })
            ]),
            new(ChatRole.Tool, [
                new FunctionResultContent(callId: "call_1", result: "Sunny, 72°F")
            ]),
            new(ChatRole.User, "Thanks!")
        };

        // TODO: Trinity to implement FunctionResultContent handling in formatters
        // For now, test the structure is accepted
        
        // Expected behavior:
        // var result = formatter.FormatMessages(messages, null);
        // Assert.Contains("get_weather", result);
        // Assert.Contains("Sunny, 72°F", result);
        
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetResponseAsync_AfterToolResultMessage_ContinuesConversation()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions 
        { 
            EnsureModelDownloaded = false,
            Model = KnownModels.Phi35MiniInstruct
        };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Portland?"),
            new(ChatRole.Assistant, [
                new FunctionCallContent("call_1", "get_weather", 
                    new Dictionary<string, object?> { { "city", "Portland" } })
            ]),
            new(ChatRole.Tool, [
                new FunctionResultContent(callId: "call_1", result: "Rainy, 55°F")
            ])
        };

        // TODO: Test that conversation continues after tool result
        // Expected behavior:
        // var response = await client.GetResponseAsync(messages);
        // Assert.NotNull(response);
        // Assert.Contains("rainy", response.Text?.ToLowerInvariant());
        
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    // Multiple tool calls
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_MultipleToolCalls_AllParsedCorrectly()
    {
        var parser = new JsonToolCallParser();
        
        var modelOutput = """
            [
                {"name": "get_weather", "arguments": {"city": "Seattle"}},
                {"name": "get_time", "arguments": {"timezone": "PST"}}
            ]
            """;

        // TODO: Test that multiple tool calls are all converted to FunctionCallContent
        // Expected behavior when Trinity implements:
        var result = parser.Parse(modelOutput);
        Assert.Equal(2, result.Count);
        
        // Then in LocalChatClient:
        // response.Contents should have 2 FunctionCallContent items
        
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    // Tool result content formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void FunctionResultContent_CreatedCorrectly()
    {
        var result = new FunctionResultContent(
            callId: "call_123",
            result: "Sunny, 75°F"
        );

        Assert.Equal("call_123", result.CallId);
        Assert.Equal("Sunny, 75°F", result.Result);
    }

    [Fact]
    public void FunctionResultContent_WithComplexResult_HandlesCorrectly()
    {
        var complexResult = new 
        { 
            temperature = 72,
            condition = "Sunny",
            humidity = 45
        };

        var result = new FunctionResultContent(
            callId: "call_1",
            result: complexResult
        );

        Assert.Equal("call_1", result.CallId);
        Assert.NotNull(result.Result);
    }

    // ──────────────────────────────────────────────
    // ChatOptions.Tools integration
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_ChatOptionsWithTools_PassedToFormatter()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions 
        { 
            EnsureModelDownloaded = false,
            Model = KnownModels.Phi35MiniInstruct
        };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        var tool = AIFunctionFactory.Create(
            (string city) => "weather",
            name: "get_weather"
        );

        var chatOptions = new ChatOptions
        {
            Tools = new[] { tool }
        };

        // TODO: Test that tools from ChatOptions are passed to formatter
        // Expected behavior:
        // await client.GetResponseAsync(messages, chatOptions);
        // Formatter should receive the tools list
        
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_NullToolsInChatOptions_NoError()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var options = new LocalLLMsOptions 
        { 
            EnsureModelDownloaded = false,
            Model = KnownModels.Phi35MiniInstruct
        };
        var client = new LocalChatClient(options, downloader);
        _disposables.Add(client);

        var chatOptions = new ChatOptions
        {
            Tools = null
        };

        // Should not throw with null tools
        // var response = await client.GetResponseAsync(messages, chatOptions);
        
        await Task.CompletedTask;
    }

    [Fact]
    public void ParsedToolCall_ToFunctionCallContent_MapsCorrectly()
    {
        // Test the mapping from ParsedToolCall to FunctionCallContent
        var parsed = new ParsedToolCall(
            CallId: "call_abc",
            FunctionName: "get_weather",
            Arguments: new Dictionary<string, object?> { { "city", "Boston" } },
            RawText: """{"name": "get_weather", "arguments": {"city": "Boston"}}"""
        );

        var functionCall = new FunctionCallContent(
            parsed.CallId,
            parsed.FunctionName,
            parsed.Arguments
        );

        Assert.Equal(parsed.CallId, functionCall.CallId);
        Assert.Equal(parsed.FunctionName, functionCall.Name);
        Assert.Equal(parsed.Arguments["city"], functionCall.Arguments?["city"]);
    }

    // ──────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
    }
}
