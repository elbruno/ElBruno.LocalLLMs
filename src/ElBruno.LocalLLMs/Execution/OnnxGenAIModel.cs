using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Generation configuration parameters for ONNX Runtime GenAI.
/// </summary>
internal sealed record GenerationParameters(
    int MaxLength = 2048,
    float Temperature = 0.7f,
    float TopP = 0.9f,
    int? TopK = null,
    float RepetitionPenalty = 1.0f);

/// <summary>
/// Thin wrapper around ONNX Runtime GenAI for model loading and inference.
/// Manages Model, Tokenizer, and generation lifecycle.
/// </summary>
internal sealed class OnnxGenAIModel : IDisposable
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;
    private bool _disposed;

    internal OnnxGenAIModel(string modelPath, ExecutionProvider provider, int gpuDeviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        if (provider == ExecutionProvider.Cpu)
        {
            _model = new Model(modelPath);
        }
        else
        {
            var config = new Config(modelPath);
            config.ClearProviders();

            var providerName = provider switch
            {
                ExecutionProvider.Cuda => "cuda",
                ExecutionProvider.DirectML => "dml",
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };

            config.AppendProvider(providerName);
            config.SetProviderOption(providerName, "device_id", gpuDeviceId.ToString());

            _model = new Model(config);
        }

        _tokenizer = new Tokenizer(_model);
    }

    /// <summary>
    /// Synchronous full generation. Returns the complete generated text (excluding the prompt).
    /// </summary>
    internal string Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(genParams, parameters);

        using var sequences = _tokenizer.Encode(prompt);
        using var generator = new Generator(_model, genParams);
        generator.AppendTokenSequences(sequences);

        using var tokenizerStream = _tokenizer.CreateStream();
        var outputText = new System.Text.StringBuilder();

        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();

            var seq = generator.GetSequence(0);
            var tokenId = seq[^1];
            var decoded = tokenizerStream.Decode(tokenId);
            outputText.Append(decoded);
        }

        return outputText.ToString();
    }

    /// <summary>
    /// Streaming generation. Yields decoded token strings as they are produced.
    /// </summary>
    internal async IAsyncEnumerable<string> GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(genParams, parameters);

        using var sequences = _tokenizer.Encode(prompt);
        using var generator = new Generator(_model, genParams);
        generator.AppendTokenSequences(sequences);

        using var tokenizerStream = _tokenizer.CreateStream();

        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();

            var seq = generator.GetSequence(0);
            var tokenId = seq[^1];
            var tokenText = tokenizerStream.Decode(tokenId);
            if (!string.IsNullOrEmpty(tokenText))
            {
                yield return tokenText;
            }

            // Yield control to allow cooperative cancellation
            await Task.Yield();
        }
    }

    private static void ApplyParameters(GeneratorParams genParams, GenerationParameters parameters)
    {
        genParams.SetSearchOption("max_length", parameters.MaxLength);
        genParams.SetSearchOption("temperature", parameters.Temperature);
        genParams.SetSearchOption("top_p", parameters.TopP);

        if (parameters.TopK.HasValue)
        {
            genParams.SetSearchOption("top_k", parameters.TopK.Value);
        }

        if (parameters.RepetitionPenalty != 1.0f)
        {
            genParams.SetSearchOption("repetition_penalty", parameters.RepetitionPenalty);
        }

        genParams.SetSearchOption("do_sample", parameters.Temperature > 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tokenizer.Dispose();
        _model.Dispose();
    }
}
