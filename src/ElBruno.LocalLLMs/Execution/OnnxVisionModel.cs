using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Thin wrapper around ONNX Runtime GenAI for vision-language model (VLM) inference.
/// Manages Model and MultiModalProcessor lifecycle; uses SetInputs instead of AppendTokenSequences.
/// </summary>
internal sealed class OnnxVisionModel : IVisionGenerationModel
{
    private readonly Model _model;
    private readonly MultiModalProcessor _processor;
    private readonly ILogger _logger;
    private bool _disposed;

    internal ExecutionProvider ActiveProvider { get; }
    internal string? ProviderSelectionDetails { get; }
    internal ModelMetadata? Metadata { get; }

    ExecutionProvider ITextGenerationModel.ActiveProvider => ActiveProvider;
    string? ITextGenerationModel.ProviderSelectionDetails => ProviderSelectionDetails;
    ModelMetadata? ITextGenerationModel.Metadata => Metadata;

    internal OnnxVisionModel(string modelPath, ExecutionProvider provider, int gpuDeviceId, int? optionsMaxSequenceLength = null, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _logger = logger ?? NullLogger.Instance;

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
                    LogMessages.ProviderAttempt(_logger, candidate);
                    _model = CreateModel(modelPath, candidate, gpuDeviceId);
                    selectedProvider = candidate;
                    goto ModelInitialized;
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu && ShouldFallbackToNextProvider(candidate, ex, ExecutionProvider.Auto))
                {
                    var reason = BuildProviderFailureReason(candidate, ex);
                    providerFailures.Add(reason);
                    var nextProvider = i + 1 < candidates.Count ? candidates[i + 1] : ExecutionProvider.Cpu;
                    LogMessages.ProviderFallback(_logger, candidate, nextProvider, reason);
                }
                catch (Exception ex) when (candidate != ExecutionProvider.Cpu)
                {
                    LogMessages.ModelInitError(_logger, $"Hard error with provider {candidate}", ex);
                    throw new ExecutionProviderException(
                        $"Failed to initialize model with provider {candidate}. This was treated as a hard error (no fallback).",
                        candidate,
                        ex);
                }
            }

            var details = providerFailures.Count > 0
                ? " Failures: " + string.Join(" | ", providerFailures)
                : string.Empty;

            throw new ExecutionProviderException(
                "Unable to initialize model with any execution provider." + details,
                ExecutionProvider.Auto);
        }

        try
        {
            LogMessages.ProviderAttempt(_logger, provider);
            _model = CreateModel(modelPath, provider, gpuDeviceId);
        }
        catch (Exception ex) when (provider != ExecutionProvider.Cpu && IsProviderNotInstalledError(provider, ex))
        {
            var packageName = provider switch
            {
                ExecutionProvider.Cuda => "Microsoft.ML.OnnxRuntimeGenAI.Cuda",
                ExecutionProvider.DirectML => "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
                _ => $"Microsoft.ML.OnnxRuntimeGenAI.{provider}"
            };

            var suggestion = $"Add the '{packageName}' NuGet package to your application project and ensure the required runtime is installed. " +
                $"Replace 'Microsoft.ML.OnnxRuntimeGenAI' with '{packageName}' — do not reference both packages simultaneously.";

            LogMessages.ModelInitError(_logger, $"Provider {provider} not installed", ex);
            throw new ExecutionProviderException(
                $"The {provider} execution provider is not available. " +
                suggestion +
                $" Inner error: {ex.Message}",
                provider,
                suggestion,
                ex);
        }

    ModelInitialized:
        ActiveProvider = selectedProvider;
        if (provider == ExecutionProvider.Auto && providerFailures.Count > 0)
        {
            ProviderSelectionDetails =
                $"Auto selected {selectedProvider} after provider fallbacks: {string.Join(" | ", providerFailures)}";
        }

        _processor = new MultiModalProcessor(_model);
        Metadata = GenAIConfigParser.TryParse(modelPath, optionsMaxSequenceLength);

        if (Metadata?.ModelName is not null && Metadata.ModelName != "qwen_vl")
        {
            _logger.LogWarning(
                "OnnxVisionModel: genai_config.json reports model.type='{ModelType}' but expected 'qwen_vl'. " +
                "FaraFormatter vision tokens are tuned for Qwen-VL. Output quality may degrade.",
                Metadata.ModelName);
        }
    }

    // ── Vision generation ────────────────────────────────────────────────────

    internal GenerationResult GenerateWithImages(string prompt, string[] imagePaths, GenerationParameters parameters, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(genParams, parameters);

        var inputTokenCount = CountPromptTokensInternal(prompt);

        Images? images = imagePaths.Length > 0 ? Images.Load(imagePaths) : null;
        try
        {
            using var inputs = _processor.ProcessImages(prompt, images!);
            using var generator = new Generator(_model, genParams);
            generator.SetInputs(inputs);

            using var tokenizerStream = _processor.CreateStream();
            var outputText = new System.Text.StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var timeToFirstToken = TimeSpan.Zero;
            var outputTokenCount = 0;
            var firstTokenSeen = false;

            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();

                var seq = generator.GetSequence(0);
                var tokenId = seq[^1];
                var decoded = tokenizerStream.Decode(tokenId);
                outputText.Append(decoded);
                outputTokenCount++;

                if (!firstTokenSeen)
                {
                    firstTokenSeen = true;
                    timeToFirstToken = sw.Elapsed;
                }
            }

            return new GenerationResult(outputText.ToString(), inputTokenCount, outputTokenCount, timeToFirstToken);
        }
        finally
        {
            images?.Dispose();
        }
    }

    internal async IAsyncEnumerable<string> GenerateWithImagesStreamingAsync(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(genParams, parameters);

        Images? images = imagePaths.Length > 0 ? Images.Load(imagePaths) : null;
        try
        {
            using var inputs = _processor.ProcessImages(prompt, images!);
            using var generator = new Generator(_model, genParams);
            generator.SetInputs(inputs);

            using var tokenizerStream = _processor.CreateStream();

            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();
                ct.ThrowIfCancellationRequested();

                var seq = generator.GetSequence(0);
                var tokenId = seq[^1];
                var tokenText = tokenizerStream.Decode(tokenId);
                if (!string.IsNullOrEmpty(tokenText))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return tokenText;
                }

                await Task.Yield();
            }
        }
        finally
        {
            images?.Dispose();
        }
    }

    // ── Text-only methods (ITextGenerationModel) — delegate to vision path ──

    internal GenerationResult Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
        => GenerateWithImages(prompt, [], parameters, ct);

    internal int CountPromptTokens(string prompt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return CountPromptTokensInternal(prompt);
    }

    internal async IAsyncEnumerable<string> GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var token in GenerateWithImagesStreamingAsync(prompt, [], parameters, ct).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    // ── Explicit interface implementations ───────────────────────────────────

    GenerationResult ITextGenerationModel.Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
        => Generate(prompt, parameters, ct);

    int ITextGenerationModel.CountPromptTokens(string prompt)
        => CountPromptTokens(prompt);

    IAsyncEnumerable<string> ITextGenerationModel.GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        CancellationToken ct)
        => GenerateStreamingAsync(prompt, parameters, ct);

    GenerationResult IVisionGenerationModel.GenerateWithImages(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        CancellationToken ct)
        => GenerateWithImages(prompt, imagePaths, parameters, ct);

    IAsyncEnumerable<string> IVisionGenerationModel.GenerateWithImagesStreamingAsync(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        CancellationToken ct)
        => GenerateWithImagesStreamingAsync(prompt, imagePaths, parameters, ct);

    // ── Provider selection (mirrored from OnnxGenAIModel) ───────────────────

    private static IReadOnlyList<ExecutionProvider> GetProviderFallbackOrder(ExecutionProvider provider) =>
        provider switch
        {
            ExecutionProvider.Auto => OperatingSystem.IsWindows()
                ? [ExecutionProvider.DirectML, ExecutionProvider.Cuda, ExecutionProvider.Cpu]
                : [ExecutionProvider.Cuda, ExecutionProvider.Cpu],
            _ => [provider]
        };

    private static bool IsProviderNotInstalledError(ExecutionProvider provider, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ShouldFallbackToNextProvider(provider, ex, provider);
    }

    private static bool ShouldFallbackToNextProvider(ExecutionProvider provider, Exception ex, ExecutionProvider initialProvider)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var message = ex.ToString();
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var normalized = message.ToLowerInvariant();

        if (initialProvider == ExecutionProvider.Auto)
        {
            if (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
                return true;

            if (normalized.Contains("is not supported", StringComparison.Ordinal) ||
                normalized.Contains("not available", StringComparison.Ordinal) ||
                normalized.Contains("is unavailable", StringComparison.Ordinal) ||
                normalized.Contains("specified provider", StringComparison.Ordinal))
            {
                return true;
            }
        }

        var providerToken = provider switch
        {
            ExecutionProvider.Cuda => "cuda",
            ExecutionProvider.DirectML => "dml",
            _ => provider.ToString().ToLowerInvariant()
        };

        var hasProviderContext = normalized.Contains(providerToken, StringComparison.Ordinal) ||
            (provider == ExecutionProvider.DirectML && normalized.Contains("directml", StringComparison.Ordinal));

        if (!hasProviderContext)
            return false;

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

    private static string BuildProviderFailureReason(ExecutionProvider provider, Exception ex)
    {
        var message = ex.Message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        if (message.Length > 180)
            message = message[..180] + "...";
        return $"{provider}: {ex.GetType().Name}: {message}";
    }

    private static Model CreateModel(string modelPath, ExecutionProvider provider, int gpuDeviceId)
    {
        if (provider == ExecutionProvider.Cpu)
            return new Model(modelPath);

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

    private static void ApplyParameters(GeneratorParams genParams, GenerationParameters parameters)
    {
        genParams.SetSearchOption("max_length", parameters.MaxLength);
        genParams.SetSearchOption("temperature", parameters.Temperature);
        genParams.SetSearchOption("top_p", parameters.TopP);

        if (parameters.TopK.HasValue)
            genParams.SetSearchOption("top_k", parameters.TopK.Value);

        if (parameters.RepetitionPenalty != 1.0f)
            genParams.SetSearchOption("repetition_penalty", parameters.RepetitionPenalty);

        genParams.SetSearchOption("do_sample", parameters.Temperature > 0);
    }

    private int CountPromptTokensInternal(string prompt)
    {
        // Use a temporary tokenizer for token counting since NamedTensors
        // does not expose input_ids length.
        using var tmpTokenizer = new Tokenizer(_model);
        using var seq = tmpTokenizer.Encode(prompt);
        return seq[0].Length;
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _processor.Dispose();
        _model.Dispose();
    }
}
