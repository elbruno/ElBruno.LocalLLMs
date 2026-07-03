using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.Internal;

internal interface ITextGenerationModel : IDisposable
{
    ExecutionProvider ActiveProvider { get; }

    string? ProviderSelectionDetails { get; }

    ModelMetadata? Metadata { get; }

    GenerationResult Generate(string prompt, GenerationParameters parameters, CancellationToken ct);

    int CountPromptTokens(string prompt);

    IAsyncEnumerable<string> GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        CancellationToken ct);
}

internal interface ITextGenerationModelFactory
{
    ITextGenerationModel Create(
        string modelPath,
        ExecutionProvider provider,
        int gpuDeviceId,
        int? optionsMaxSequenceLength,
        ILogger logger);
}

internal sealed class OnnxGenAIModelFactory : ITextGenerationModelFactory
{
    public ITextGenerationModel Create(
        string modelPath,
        ExecutionProvider provider,
        int gpuDeviceId,
        int? optionsMaxSequenceLength,
        ILogger logger)
        => new OnnxGenAIModel(modelPath, provider, gpuDeviceId, optionsMaxSequenceLength, logger);
}
