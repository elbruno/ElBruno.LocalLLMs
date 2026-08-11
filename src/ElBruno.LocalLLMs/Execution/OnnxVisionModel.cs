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
    // The probe never generates tokens; it only needs a ceiling that cannot undershoot
    // the expanded multimodal input_ids length before we can compute the real max_length.
    private const int ProbeMaxLength = int.MaxValue;
    private readonly Model _model;
    private readonly MultiModalProcessor _processor;
    private readonly IVisionGenerationRuntime _runtime;
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

        var initialization = ExecutionProviderSelection.InitializeModel(
            provider,
            _logger,
            candidate => CreateModel(modelPath, candidate, gpuDeviceId));

        _model = initialization.Model;
        ActiveProvider = initialization.ActiveProvider;
        ProviderSelectionDetails = initialization.ProviderSelectionDetails;

        _processor = new MultiModalProcessor(_model);
        _runtime = new OnnxVisionGenerationRuntime(_model, _processor);
        Metadata = GenAIConfigParser.TryParse(modelPath, optionsMaxSequenceLength);

        if (Metadata?.ModelName is not null &&
            Metadata.ModelName != "qwen_vl" &&
            Metadata.ModelName != "qwen3_vl")
        {
            _logger.LogWarning(
                "OnnxVisionModel: genai_config.json reports model.type='{ModelType}' but expected 'qwen_vl' or 'qwen3_vl'. " +
                "Vision token processing is tuned for Qwen-VL. Output quality may degrade.",
                Metadata.ModelName);
        }
    }

    internal OnnxVisionModel(IVisionGenerationRuntime runtime, ILogger? logger = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _model = null!;
        _processor = null!;
        _logger = logger ?? NullLogger.Instance;
        ActiveProvider = ExecutionProvider.Cpu;
    }

    // ── Vision generation ────────────────────────────────────────────────────

    internal GenerationResult GenerateWithImages(string prompt, string[] imagePaths, GenerationParameters parameters, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GenerateWithImagesCore(
            prompt,
            imagePaths,
            parameters,
            ct,
            _runtime,
            Metadata?.ConfigMaxSequenceLength > 0 ? Metadata.ConfigMaxSequenceLength : null,
            _logger);
    }

    internal async IAsyncEnumerable<string> GenerateWithImagesStreamingAsync(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await foreach (var token in GenerateWithImagesStreamingCore(
            prompt,
            imagePaths,
            parameters,
            ct,
            _runtime,
            Metadata?.ConfigMaxSequenceLength > 0 ? Metadata.ConfigMaxSequenceLength : null,
            _logger).ConfigureAwait(false))
        {
            yield return token;
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
        ExecutionProviderSelection.GetProviderFallbackOrder(provider);

    private static bool IsProviderNotInstalledError(ExecutionProvider provider, Exception ex)
        => ExecutionProviderSelection.IsProviderNotInstalledError(provider, ex);

    private static bool ShouldFallbackToNextProvider(ExecutionProvider provider, Exception ex, ExecutionProvider initialProvider)
        => ExecutionProviderSelection.ShouldFallbackToNextProvider(provider, ex, initialProvider);

    private static string BuildProviderFailureReason(ExecutionProvider provider, Exception ex)
        => ExecutionProviderSelection.BuildProviderFailureReason(provider, ex);

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

    private static void ApplyParameters(IVisionSearchOptions searchOptions, GenerationParameters parameters, int inputTokenCount = 0)
    {
        searchOptions.SetSearchOption("max_length", ResolveMaxLength(parameters.MaxLength, inputTokenCount, parameters.MaxOutputTokens));
        searchOptions.SetSearchOption("temperature", parameters.Temperature);
        searchOptions.SetSearchOption("top_p", parameters.TopP);

        if (parameters.TopK.HasValue)
            searchOptions.SetSearchOption("top_k", parameters.TopK.Value);

        if (parameters.RepetitionPenalty != 1.0f)
            searchOptions.SetSearchOption("repetition_penalty", parameters.RepetitionPenalty);

        searchOptions.SetSearchOption("do_sample", parameters.Temperature > 0);
    }

    private int CountPromptTokensInternal(string prompt)
    {
        return _runtime.CountPromptTokens(prompt);
    }

    internal static int ResolveInputTokenCount(Func<string, long[]?> inputShapeProvider, int fallbackTokenCount)
    {
        ArgumentNullException.ThrowIfNull(inputShapeProvider);

        try
        {
            var shape = inputShapeProvider("input_ids");
            if (shape is { Length: > 0 })
            {
                var tokenCount = shape[^1];
                if (tokenCount > 0 && tokenCount <= int.MaxValue)
                    return checked((int)tokenCount);
            }
        }
        catch
        {
            // Fall back to text-only tokenization if the multimodal input shape is not accessible.
        }

        return fallbackTokenCount;
    }

    internal static int ResolveMaxLength(int maxLength, int inputTokenCount, int? maxOutputTokens)
    {
        var effectiveMaxLength = maxOutputTokens.HasValue
            ? Math.Min(maxLength, inputTokenCount + maxOutputTokens.Value)
            : maxLength;

        return Math.Max(effectiveMaxLength, inputTokenCount + 1);
    }

    internal static GenerationResult GenerateWithImagesCore(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        CancellationToken ct,
        IVisionGenerationRuntime runtime,
        int? probeMaxLength = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        IVisionImages? images = imagePaths.Length > 0 ? runtime.LoadImages(imagePaths) : null;
        try
        {
            using var inputs = runtime.ProcessImages(prompt, images);

            var inputTokenCount = imagePaths.Length > 0
                ? ResolveVisionInputTokenCount(runtime, prompt, inputs, probeMaxLength, logger)
                : runtime.CountPromptTokens(prompt);
            logger?.LogDebug(
                "Vision input token count resolved to {InputTokenCount} using probe max length {ProbeMaxLength}.",
                inputTokenCount,
                probeMaxLength);

            using var searchOptions = runtime.CreateSearchOptions();
            ApplyParameters(searchOptions, parameters, inputTokenCount);

            using var generator = runtime.CreateGenerator(searchOptions);
            generator.SetInputs(inputs);

            using var tokenizerStream = runtime.CreateTokenizerStream();
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

    internal static async IAsyncEnumerable<string> GenerateWithImagesStreamingCore(
        string prompt,
        string[] imagePaths,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct,
        IVisionGenerationRuntime runtime,
        int? probeMaxLength = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        IVisionImages? images = imagePaths.Length > 0 ? runtime.LoadImages(imagePaths) : null;
        try
        {
            using var inputs = runtime.ProcessImages(prompt, images);

            var inputTokenCount = imagePaths.Length > 0
                ? ResolveVisionInputTokenCount(runtime, prompt, inputs, probeMaxLength, logger)
                : runtime.CountPromptTokens(prompt);
            logger?.LogDebug(
                "Vision input token count resolved to {InputTokenCount} using probe max length {ProbeMaxLength}.",
                inputTokenCount,
                probeMaxLength);

            using var searchOptions = runtime.CreateSearchOptions();
            ApplyParameters(searchOptions, parameters, inputTokenCount);

            using var generator = runtime.CreateGenerator(searchOptions);
            generator.SetInputs(inputs);

            using var tokenizerStream = runtime.CreateTokenizerStream();

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

    private static int ResolveVisionInputTokenCount(
        IVisionGenerationRuntime runtime,
        string prompt,
        IVisionInputs inputs,
        int? probeMaxLength,
        ILogger? logger)
    {
        var fallbackTokenCount = runtime.CountPromptTokens(prompt);
        var effectiveProbeMaxLength = probeMaxLength is > 0
            ? Math.Min(probeMaxLength.Value, ProbeMaxLength)
            : ProbeMaxLength;

        try
        {
            using var probeGenerator = runtime.CreateProbeGenerator(effectiveProbeMaxLength);
            probeGenerator.SetInputs(inputs);

            return ResolveInputTokenCount(
                name =>
                {
                    try
                    {
                        return probeGenerator.GetInputShape(name);
                    }
                    catch
                    {
                        return null;
                    }
                },
                fallbackTokenCount);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Vision input token probe failed; falling back to prompt token count.");
            return fallbackTokenCount;
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _processor?.Dispose();
        _model?.Dispose();
    }
}

internal interface IVisionGenerationRuntime
{
    IVisionImages LoadImages(string[] imagePaths);
    IVisionInputs ProcessImages(string prompt, IVisionImages? images);
    int CountPromptTokens(string prompt);
    IVisionSearchOptions CreateSearchOptions();
    IVisionGenerator CreateGenerator(IVisionSearchOptions searchOptions);
    IVisionGenerator CreateProbeGenerator(int maxLength);
    IVisionTokenizerStream CreateTokenizerStream();
}

internal interface IVisionImages : IDisposable;

internal interface IVisionInputs : IDisposable;

internal interface IVisionSearchOptions : IDisposable
{
    void SetSearchOption(string name, int value);
    void SetSearchOption(string name, float value);
    void SetSearchOption(string name, bool value);
}

internal interface IVisionGenerator : IDisposable
{
    void SetInputs(IVisionInputs inputs);
    bool IsDone();
    void GenerateNextToken();
    int[] GetSequence(int sequenceIndex);
    long[]? GetInputShape(string name);
}

internal interface IVisionTokenizerStream : IDisposable
{
    string Decode(int tokenId);
}

file sealed class OnnxVisionGenerationRuntime(Model model, MultiModalProcessor processor) : IVisionGenerationRuntime
{
    public IVisionImages LoadImages(string[] imagePaths)
        => new OnnxVisionImages(Images.Load(imagePaths));

    public IVisionInputs ProcessImages(string prompt, IVisionImages? images)
        => new OnnxVisionInputs(processor.ProcessImages(prompt, (images as OnnxVisionImages)?.Inner!));

    public int CountPromptTokens(string prompt)
    {
        using var tokenizer = new Tokenizer(model);
        using var sequence = tokenizer.Encode(prompt);
        return sequence[0].Length;
    }

    public IVisionSearchOptions CreateSearchOptions()
        => new OnnxVisionSearchOptions(model);

    public IVisionGenerator CreateGenerator(IVisionSearchOptions searchOptions)
        => new OnnxVisionGenerator(model, ((OnnxVisionSearchOptions)searchOptions).Inner);

    public IVisionGenerator CreateProbeGenerator(int maxLength)
    {
        var searchOptions = new OnnxVisionSearchOptions(model);
        searchOptions.SetSearchOption("max_length", maxLength);
        return new OnnxVisionGenerator(model, searchOptions.Inner, searchOptions);
    }

    public IVisionTokenizerStream CreateTokenizerStream()
        => new OnnxVisionTokenizerStream(processor.CreateStream());
}

file sealed class OnnxVisionSearchOptions(Model model) : IVisionSearchOptions
{
    internal GeneratorParams Inner { get; } = new(model);

    public void SetSearchOption(string name, int value) => Inner.SetSearchOption(name, value);
    public void SetSearchOption(string name, float value) => Inner.SetSearchOption(name, value);
    public void SetSearchOption(string name, bool value) => Inner.SetSearchOption(name, value);
    public void Dispose() => Inner.Dispose();
}

file sealed class OnnxVisionGenerator(Model model, GeneratorParams parameters, IDisposable? ownedResource = null) : IVisionGenerator
{
    private readonly Generator _generator = new(model, parameters);

    public void SetInputs(IVisionInputs inputs)
        => _generator.SetInputs(((OnnxVisionInputs)inputs).Inner);

    public bool IsDone() => _generator.IsDone();

    public void GenerateNextToken() => _generator.GenerateNextToken();

    public int[] GetSequence(int sequenceIndex) => _generator.GetSequence((ulong)sequenceIndex).ToArray();

    public long[]? GetInputShape(string name) => _generator.GetInput(name).Shape();

    public void Dispose()
    {
        _generator.Dispose();
        ownedResource?.Dispose();
    }
}

file sealed class OnnxVisionTokenizerStream(TokenizerStream inner) : IVisionTokenizerStream
{
    public string Decode(int tokenId) => inner.Decode(tokenId);

    public void Dispose() => inner.Dispose();
}

file sealed class OnnxVisionImages(Images inner) : IVisionImages
{
    internal Images Inner { get; } = inner;

    public void Dispose() => Inner.Dispose();
}

file sealed class OnnxVisionInputs(NamedTensors inner) : IVisionInputs
{
    internal NamedTensors Inner { get; } = inner;

    public void Dispose() => Inner.Dispose();
}
