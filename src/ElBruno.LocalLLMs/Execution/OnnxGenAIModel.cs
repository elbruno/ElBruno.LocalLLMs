using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Generation configuration parameters for ONNX Runtime GenAI.
/// </summary>
/// <param name="MaxLength">
/// Maximum total sequence length (input tokens + output tokens). Mapped to ORT-GenAI's
/// <c>max_length</c> search option. Acts as the upper bound on context usage.
/// </param>
/// <param name="Temperature">Sampling temperature (0 = greedy).</param>
/// <param name="TopP">Top-p nucleus sampling threshold.</param>
/// <param name="TopK">Top-k sampling limit; null to disable.</param>
/// <param name="RepetitionPenalty">Repetition penalty multiplier (1.0 = no penalty).</param>
/// <param name="MaxOutputTokens">
/// When non-null, limits output tokens independently of input length. The effective
/// <c>max_length</c> passed to ORT-GenAI is <c>min(MaxLength, inputTokenCount + MaxOutputTokens)</c>.
/// This correctly maps <see cref="Microsoft.Extensions.AI.ChatOptions.MaxOutputTokens"/>:
/// a limit on <em>new</em> tokens, not total sequence length.
/// </param>
internal sealed record GenerationParameters(
    int MaxLength = 2048,
    float Temperature = 0.7f,
    float TopP = 0.9f,
    int? TopK = null,
    float RepetitionPenalty = 1.0f,
    int? MaxOutputTokens = null);

/// <summary>
/// Result of a buffered (non-streaming) generation call. Carries the token-boundary
/// information needed for generation-lifecycle diagnostics (time-to-first-token, input/output
/// token counts) without leaking a callback API — the model is the single source of truth for
/// counts since it owns both the encoded prompt sequence and the per-token generation loop.
/// </summary>
internal sealed record GenerationResult(
    string Text,
    int InputTokenCount,
    int OutputTokenCount,
    TimeSpan TimeToFirstToken);

/// <summary>
/// Thin wrapper around ONNX Runtime GenAI for model loading and inference.
/// Manages Model, Tokenizer, and generation lifecycle.
/// </summary>
internal sealed class OnnxGenAIModel : ITextGenerationModel
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;
    private readonly ILogger _logger;
    private bool _disposed;

    internal ExecutionProvider ActiveProvider { get; }
    internal string? ProviderSelectionDetails { get; }
    internal ModelMetadata? Metadata { get; }

    ExecutionProvider ITextGenerationModel.ActiveProvider => ActiveProvider;
    string? ITextGenerationModel.ProviderSelectionDetails => ProviderSelectionDetails;
    ModelMetadata? ITextGenerationModel.Metadata => Metadata;

    internal OnnxGenAIModel(string modelPath, ExecutionProvider provider, int gpuDeviceId, int? optionsMaxSequenceLength = null, ILogger? logger = null)
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

        _tokenizer = new Tokenizer(_model);
        Metadata = GenAIConfigParser.TryParse(modelPath, optionsMaxSequenceLength);
    }

    internal static IReadOnlyList<ExecutionProvider> GetProviderFallbackOrder(ExecutionProvider provider) =>
        ExecutionProviderSelection.GetProviderFallbackOrder(provider);

    /// <summary>
    /// Returns <see langword="true"/> when the exception indicates the requested execution
    /// provider's native runtime is not present (e.g. the GPU NuGet package is missing or
    /// the wrong variant is installed).
    /// </summary>
    internal static bool IsProviderNotInstalledError(ExecutionProvider provider, Exception ex)
    {
        return ExecutionProviderSelection.IsProviderNotInstalledError(provider, ex);
    }

    /// <summary>
    /// Two-argument overload for backward compatibility. Uses strict (non-Auto) matching.
    /// </summary>
    internal static bool ShouldFallbackToNextProvider(ExecutionProvider provider, Exception ex)
        => ShouldFallbackToNextProvider(provider, ex, provider);

    internal static bool ShouldFallbackToNextProvider(
        ExecutionProvider provider, Exception ex, ExecutionProvider initialProvider)
        => ExecutionProviderSelection.ShouldFallbackToNextProvider(provider, ex, initialProvider);

    internal static string BuildProviderFailureReason(ExecutionProvider provider, Exception ex)
        => ExecutionProviderSelection.BuildProviderFailureReason(provider, ex);

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
    /// Synchronous full generation. Returns the complete generated text (excluding the prompt)
    /// plus token-boundary diagnostics (input/output token counts, time-to-first-token).
    /// </summary>
    internal GenerationResult Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var sequences = _tokenizer.Encode(prompt);
        var inputTokenCount = sequences[0].Length;

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(new OnnxGenerationSearchOptions(genParams), parameters, inputTokenCount);

        using var generator = new Generator(_model, genParams);
        generator.AppendTokenSequences(sequences);

        using var tokenizerStream = _tokenizer.CreateStream();
        var outputText = new System.Text.StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timeToFirstToken = TimeSpan.Zero;
        var outputTokenCount = 0;
        var firstTokenSeen = false;

        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();

            var decoded = tokenizerStream.Decode(generator.GetNextTokens()[0]);
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

    /// <summary>
    /// Counts the number of tokens the tokenizer produces for <paramref name="prompt"/>,
    /// without running generation. Used by the streaming path to report input token counts
    /// for diagnostics (the streaming loop itself yields tokens one at a time, so it does not
    /// need a buffered result type to observe output token boundaries).
    /// </summary>
    internal int CountPromptTokens(string prompt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var sequences = _tokenizer.Encode(prompt);
        return sequences[0].Length;
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

        using var sequences = _tokenizer.Encode(prompt);
        var streamInputTokenCount = sequences[0].Length;

        using var genParams = new GeneratorParams(_model);
        ApplyParameters(new OnnxGenerationSearchOptions(genParams), parameters, streamInputTokenCount);

        using var generator = new Generator(_model, genParams);
        generator.AppendTokenSequences(sequences);

        using var tokenizerStream = _tokenizer.CreateStream();

        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();
            ct.ThrowIfCancellationRequested();

            // GetNextTokens() returns the token just produced. Reading GetSequence(0)[^1]
            // instead re-reads the previous token on the final iteration, duplicating it.
            var tokenText = tokenizerStream.Decode(generator.GetNextTokens()[0]);
            if (!string.IsNullOrEmpty(tokenText))
            {
                ct.ThrowIfCancellationRequested();
                yield return tokenText;
            }

            // Yield control to allow cooperative cancellation
            await Task.Yield();
        }
    }

    GenerationResult ITextGenerationModel.Generate(string prompt, GenerationParameters parameters, CancellationToken ct)
        => Generate(prompt, parameters, ct);

    int ITextGenerationModel.CountPromptTokens(string prompt)
        => CountPromptTokens(prompt);

    IAsyncEnumerable<string> ITextGenerationModel.GenerateStreamingAsync(
        string prompt,
        GenerationParameters parameters,
        CancellationToken ct)
        => GenerateStreamingAsync(prompt, parameters, ct);

    /// <summary>
    /// Internal (rather than private) so it can be exercised directly by
    /// <c>OnnxGenAIModelTemperatureTests</c> via a recording <see cref="IGenerationSearchOptions"/>
    /// fake, without constructing a real ONNX <see cref="Model"/>.
    /// </summary>
    internal static void ApplyParameters(IGenerationSearchOptions searchOptions, GenerationParameters parameters, int inputTokenCount = 0)
    {
        // When MaxOutputTokens is specified, compute effective max_length as
        // min(MaxLength, inputTokenCount + MaxOutputTokens) so that the limit applies to
        // *new* tokens only — matching Microsoft.Extensions.AI.ChatOptions.MaxOutputTokens semantics.
        // Without MaxOutputTokens, MaxLength is the total context cap as-is.
        var effectiveMaxLength = parameters.MaxOutputTokens.HasValue
            ? Math.Min(parameters.MaxLength, inputTokenCount + parameters.MaxOutputTokens.Value)
            : parameters.MaxLength;

        searchOptions.SetSearchOption("max_length", Math.Max(effectiveMaxLength, inputTokenCount + 1));

        // ORT-GenAI's native runtime crashes with an integer divide-by-zero if
        // "temperature" is set to exactly 0 (or any non-positive value), even when
        // do_sample is false. Greedy decoding (Temperature <= 0, per this record's own
        // "0 = greedy" contract) must be achieved by omitting the search option
        // entirely rather than passing 0 through — do_sample=false is sufficient on
        // its own to select greedy decoding.
        if (parameters.Temperature > 0)
        {
            searchOptions.SetSearchOption("temperature", parameters.Temperature);
        }

        searchOptions.SetSearchOption("top_p", parameters.TopP);

        if (parameters.TopK.HasValue)
        {
            searchOptions.SetSearchOption("top_k", parameters.TopK.Value);
        }

        if (parameters.RepetitionPenalty != 1.0f)
        {
            searchOptions.SetSearchOption("repetition_penalty", parameters.RepetitionPenalty);
        }

        searchOptions.SetSearchOption("do_sample", parameters.Temperature > 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tokenizer.Dispose();
        _model.Dispose();
    }
}

/// <summary>
/// Test seam mirroring <c>IVisionSearchOptions</c> (see <see cref="OnnxVisionModel"/>): abstracts
/// the native ORT-GenAI <c>GeneratorParams.SetSearchOption</c> family of calls
/// so that <c>OnnxGenAIModel.ApplyParameters</c> — in particular the ORT-GenAI native
/// divide-by-zero guard around <c>"temperature"</c> — can be unit-tested with a recording fake,
/// without constructing a real ONNX <see cref="Model"/>.
/// </summary>
internal interface IGenerationSearchOptions
{
    void SetSearchOption(string name, int value);
    void SetSearchOption(string name, float value);
    void SetSearchOption(string name, bool value);
}

/// <summary>
/// Default <see cref="IGenerationSearchOptions"/> implementation: delegates straight to the real
/// ORT-GenAI <see cref="GeneratorParams"/>. Used by every real (non-test) call site so runtime
/// behavior is unchanged by the seam.
/// </summary>
file sealed class OnnxGenerationSearchOptions(GeneratorParams genParams) : IGenerationSearchOptions
{
    public void SetSearchOption(string name, int value) => genParams.SetSearchOption(name, value);
    public void SetSearchOption(string name, float value) => genParams.SetSearchOption(name, value);
    public void SetSearchOption(string name, bool value) => genParams.SetSearchOption(name, value);
}
