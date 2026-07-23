using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.Internal;

internal interface IVisionGenerationModel : ITextGenerationModel
{
    GenerationResult GenerateWithImages(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        CancellationToken ct);

    IAsyncEnumerable<string> GenerateWithImagesStreamingAsync(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        CancellationToken ct);
}

internal interface IVisionGenerationModelFactory
{
    IVisionGenerationModel Create(
        string modelPath,
        ExecutionProvider provider,
        int gpuDeviceId,
        int? optionsMaxSequenceLength,
        ILogger logger);
}

internal sealed class OnnxVisionModelFactory : IVisionGenerationModelFactory
{
    public IVisionGenerationModel Create(
        string modelPath,
        ExecutionProvider provider,
        int gpuDeviceId,
        int? optionsMaxSequenceLength,
        ILogger logger)
        => new OnnxVisionModel(modelPath, provider, gpuDeviceId, optionsMaxSequenceLength, logger);
}
