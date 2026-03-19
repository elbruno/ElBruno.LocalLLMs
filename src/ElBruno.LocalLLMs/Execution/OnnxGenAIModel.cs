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

    internal ExecutionProvider ActiveProvider { get; }
    internal string? ProviderSelectionDetails { get; }

    internal OnnxGenAIModel(string modelPath, ExecutionProvider provider, int gpuDeviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var selectedProvider = provider;
        var providerFailures = new List<string>();
        if (provider == ExecutionProvider.Auto)
        {
            var candidates = GetProviderFallbackOrder(provider);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                try
                {
                    _model = CreateModel(modelPath, candidate, gpuDeviceId);
                    selectedProvider = candidate;
                    goto ModelInitialized;
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu && ShouldFallbackToNextProvider(candidate, ex))
                {
                    providerFailures.Add(BuildProviderFailureReason(candidate, ex));
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu)
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize model with provider {candidate}. This was treated as a hard error (no fallback).",
                        ex);
                }
            }

            var details = providerFailures.Count > 0
                ? " Failures: " + string.Join(" | ", providerFailures)
                : string.Empty;

            throw new InvalidOperationException("Unable to initialize model with any execution provider." + details);
        }

        _model = CreateModel(modelPath, provider, gpuDeviceId);

ModelInitialized:
        ActiveProvider = selectedProvider;
    if (provider == ExecutionProvider.Auto && providerFailures.Count > 0)
    {
        ProviderSelectionDetails =
        $"Auto selected {selectedProvider} after provider fallbacks: {string.Join(" | ", providerFailures)}";
    }

        _tokenizer = new Tokenizer(_model);
    }

    internal static IReadOnlyList<ExecutionProvider> GetProviderFallbackOrder(ExecutionProvider provider) =>
        provider switch
        {
            ExecutionProvider.Auto => OperatingSystem.IsWindows()
                ? [ExecutionProvider.DirectML, ExecutionProvider.Cuda, ExecutionProvider.Cpu]
                : [ExecutionProvider.Cuda, ExecutionProvider.Cpu],
            _ => [provider]
        };

    internal static bool ShouldFallbackToNextProvider(ExecutionProvider provider, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var message = ex.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var providerToken = provider switch
        {
            ExecutionProvider.Cuda => "cuda",
            ExecutionProvider.DirectML => "dml",
            _ => provider.ToString().ToLowerInvariant()
        };

        var normalized = message.ToLowerInvariant();
        var hasProviderContext = normalized.Contains(providerToken, StringComparison.Ordinal) ||
            (provider == ExecutionProvider.DirectML && normalized.Contains("directml", StringComparison.Ordinal));

        if (!hasProviderContext)
        {
            return false;
        }

        return normalized.Contains("failed to load", StringComparison.Ordinal) ||
               normalized.Contains("not found", StringComparison.Ordinal) ||
               normalized.Contains("not supported", StringComparison.Ordinal) ||
               normalized.Contains("is unavailable", StringComparison.Ordinal) ||
               normalized.Contains("provider is unavailable", StringComparison.Ordinal) ||
               normalized.Contains("is not enabled", StringComparison.Ordinal) ||
               normalized.Contains("not been built with", StringComparison.Ordinal) ||
               normalized.Contains("could not be created", StringComparison.Ordinal) ||
               normalized.Contains("no available provider", StringComparison.Ordinal) ||
               normalized.Contains("unable to find", StringComparison.Ordinal) ||
               normalized.Contains("cannot load", StringComparison.Ordinal) ||
               normalized.Contains("not available", StringComparison.Ordinal);
    }

    internal static string BuildProviderFailureReason(ExecutionProvider provider, Exception ex)
    {
        var message = ex.Message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        if (message.Length > 180)
        {
            message = message[..180] + "...";
        }

        return $"{provider}: {ex.GetType().Name}: {message}";
    }

    private static Model CreateModel(string modelPath, ExecutionProvider provider, int gpuDeviceId)
    {
        if (provider == ExecutionProvider.Cpu)
        {
            return new Model(modelPath);
        }

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

        return new Model(config);
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
