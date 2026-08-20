using ElBruno.LocalLLMs.Internal;
using ElBruno.LocalLLMs.Tests.TestDoubles;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Client-level tests for GPT-OSS / Harmony wiring: chain-of-thought must be stripped in
/// both the buffered and streaming paths, tool calls must still be recovered from the
/// commentary channel, and non-Harmony models must be completely unaffected.
///
/// Response fixtures are verbatim GPT-OSS 20B output captured from the real ONNX model.
/// </summary>
public class HarmonyChatClientTests
{
    private const string AnalysisThenFinal =
        "<|channel|>analysis<|message|>Need answer: Paris.<|end|>" +
        "<|start|>assistant<|channel|>final<|message|>The capital of France is Paris.<|return|>";

    private const string AnalysisThenToolCall =
        "<|channel|>analysis<|message|>We need to call the get_weather function.<|end|>" +
        "<|start|>assistant<|channel|>commentary to=functions.get_weather <|constrain|>json" +
        "<|message|>{\"city\":\"Paris\"}<|call|>";

    // ── Buffered path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_Harmony_StripsChainOfThought()
    {
        var model = new ScriptedTextGenerationModel();
        model.EnqueueBufferedResponse(AnalysisThenFinal);

        await using var client = CreateClient(model, KnownModels.GptOss20B);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Capital of France?")]);

        Assert.Equal("The capital of France is Paris.", response.Text);
        Assert.DoesNotContain("Need answer", response.Text);
        Assert.DoesNotContain("<|", response.Text);
    }

    [Fact]
    public async Task GetResponseAsync_Harmony_RecoversToolCallFromCommentaryChannel()
    {
        var model = new ScriptedTextGenerationModel();
        model.EnqueueBufferedResponse(AnalysisThenToolCall);

        await using var client = CreateClient(model, KnownModels.GptOss20B);

        var tool = AIFunctionFactory.Create(
            (string city) => $"sunny in {city}", "get_weather", "Get weather.");

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Weather in Paris?")],
            new ChatOptions { Tools = [tool] });

        var call = Assert.Single(response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>());

        Assert.Equal("get_weather", call.Name);
        Assert.Equal("Paris", call.Arguments!["city"]);
    }

    [Fact]
    public async Task GetResponseAsync_NonHarmonyModel_TextPassesThroughUnchanged()
    {
        // A non-Harmony model must never have its output rewritten, even if it happens
        // to contain text that resembles Harmony markers.
        const string raw = "Plain answer with <|channel|> lookalike text.";

        var model = new ScriptedTextGenerationModel();
        model.EnqueueBufferedResponse(raw);

        await using var client = CreateClient(model, KnownModels.Phi35MiniInstruct);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.Equal(raw, response.Text);
    }

    // ── Streaming path ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStreamingResponseAsync_Harmony_EmitsOnlyFinalChannel()
    {
        // Split into many small chunks so markers straddle token boundaries.
        var tokens = ChunkBy(AnalysisThenFinal, 3);

        var model = new ScriptedTextGenerationModel();
        model.EnqueueStreamingResponse(tokens);

        await using var client = CreateClient(model, KnownModels.GptOss20B);

        var text = await CollectAsync(client, "Capital of France?");

        Assert.Equal("The capital of France is Paris.", text);
        Assert.DoesNotContain("Need answer", text);
        Assert.DoesNotContain("<|", text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Harmony_ToolCallEmitsNoVisibleText()
    {
        var model = new ScriptedTextGenerationModel();
        model.EnqueueStreamingResponse(ChunkBy(AnalysisThenToolCall, 5));

        await using var client = CreateClient(model, KnownModels.GptOss20B);

        var tool = AIFunctionFactory.Create(
            (string city) => $"sunny in {city}", "get_weather", "Get weather.");

        var textChunks = new List<string>();
        var calls = new List<FunctionCallContent>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Weather?")],
            new ChatOptions { Tools = [tool] }))
        {
            textChunks.Add(update.Text);
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
        }

        Assert.Equal(string.Empty, string.Concat(textChunks));
        var call = Assert.Single(calls);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("Paris", call.Arguments!["city"]);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NonHarmonyModel_TokensPassThroughUnchanged()
    {
        string[] tokens = ["Hello", ", ", "world", "!"];

        var model = new ScriptedTextGenerationModel();
        model.EnqueueStreamingResponse(tokens);

        await using var client = CreateClient(model, KnownModels.Phi35MiniInstruct);

        Assert.Equal("Hello, world!", await CollectAsync(client, "Hi"));
    }

    // ── Prompt construction ───────────────────────────────────────────────────

    [Fact]
    public async Task GetResponseAsync_Harmony_UsesHarmonyPromptWithReasoningEffort()
    {
        string? capturedPrompt = null;

        var model = new ScriptedTextGenerationModel();
        model.EnqueueBufferedResponse((prompt, _, _) =>
        {
            capturedPrompt = prompt;
            return new GenerationResult(AnalysisThenFinal, 4, 1, TimeSpan.FromMilliseconds(1));
        });

        var options = BuildOptions(KnownModels.GptOss20B);
        options.ReasoningEffort = ReasoningEffort.High;

        await using var client = CreateClient(model, options);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("<|start|>system<|message|>", capturedPrompt);
        Assert.Contains("Reasoning: high", capturedPrompt);
        Assert.EndsWith("<|start|>assistant", capturedPrompt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocalLLMsOptions BuildOptions(ModelDefinition model) => new()
    {
        Model = model,
        ModelPath = Path.GetTempPath(),
        EnsureModelDownloaded = false
    };

    private static LocalChatClient CreateClient(ScriptedTextGenerationModel model, ModelDefinition definition)
        => CreateClient(model, BuildOptions(definition));

    private static LocalChatClient CreateClient(ScriptedTextGenerationModel model, LocalLLMsOptions options)
        => new(
            options,
            Substitute.For<IModelDownloader>(),
            loggerFactory: null,
            modelFactory: new ScriptedTextGenerationModelFactory(model));

    private static async Task<string> CollectAsync(LocalChatClient client, string userText)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, userText)]))
        {
            sb.Append(update.Text);
        }

        return sb.ToString();
    }

    private static string[] ChunkBy(string text, int size)
    {
        var chunks = new List<string>();
        for (var i = 0; i < text.Length; i += size)
        {
            chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        }

        return [.. chunks];
    }
}
