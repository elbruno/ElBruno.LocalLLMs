using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.TestDoubles;

internal sealed class ScriptedTextGenerationModel : ITextGenerationModel
{
    private readonly Queue<Func<string, GenerationParameters, CancellationToken, GenerationResult>> _bufferedResponses = new();
    private readonly Queue<Func<string, GenerationParameters, CancellationToken, IAsyncEnumerable<string>>> _streamingResponses = new();

    internal int CountPromptTokensResult { get; set; } = 4;

    public ExecutionProvider ActiveProvider { get; init; } = ExecutionProvider.Cpu;

    public string? ProviderSelectionDetails { get; init; }

    public ModelMetadata? Metadata { get; init; }

    internal void EnqueueBufferedResponse(
        Func<string, GenerationParameters, CancellationToken, GenerationResult> responseFactory)
        => _bufferedResponses.Enqueue(responseFactory);

    internal void EnqueueBufferedResponse(string text, int inputTokens = 4, int outputTokens = 1, int timeToFirstTokenMs = 1)
        => EnqueueBufferedResponse((_, _, _) => new GenerationResult(
            text,
            InputTokenCount: inputTokens,
            OutputTokenCount: outputTokens,
            TimeToFirstToken: TimeSpan.FromMilliseconds(timeToFirstTokenMs)));

    internal void EnqueueStreamingResponse(
        Func<string, GenerationParameters, CancellationToken, IAsyncEnumerable<string>> responseFactory)
        => _streamingResponses.Enqueue(responseFactory);

    internal void EnqueueStreamingResponse(params string[] tokens)
        => EnqueueStreamingResponse((_, _, ct) => YieldTokensAsync(tokens, ct));

    public GenerationResult Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
    {
        if (_bufferedResponses.Count == 0)
        {
            throw new InvalidOperationException("No buffered response was configured for this test.");
        }

        return _bufferedResponses.Dequeue()(prompt, parameters, ct);
    }

    public int CountPromptTokens(string prompt) => CountPromptTokensResult;

    public IAsyncEnumerable<string> GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        CancellationToken ct)
    {
        if (_streamingResponses.Count == 0)
        {
            throw new InvalidOperationException("No streaming response was configured for this test.");
        }

        return _streamingResponses.Dequeue()(prompt, parameters, ct);
    }

    public void Dispose()
    {
    }

    private static async IAsyncEnumerable<string> YieldTokensAsync(
        IReadOnlyList<string> tokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
            await Task.Yield();
        }
    }
}

internal sealed class ScriptedTextGenerationModelFactory : ITextGenerationModelFactory
{
    private readonly ITextGenerationModel _model;

    internal ScriptedTextGenerationModelFactory(ITextGenerationModel model)
    {
        _model = model;
    }

    public ITextGenerationModel Create(
        string modelPath,
        ExecutionProvider provider,
        int gpuDeviceId,
        int? optionsMaxSequenceLength,
        Microsoft.Extensions.Logging.ILogger logger)
        => _model;
}
