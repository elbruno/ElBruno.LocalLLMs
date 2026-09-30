using System.Diagnostics;
using ElBruno.LocalLLMs.Decisions.Internal;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// An <see cref="IDecisionClient"/> that runs a Laya decision model in-process through ONNX Runtime.
/// </summary>
/// <remarks>
/// <para>
/// Nothing leaves the machine and there is no server to start. The model files are downloaded
/// from HuggingFace the first time the client is used and cached afterwards, so the first call
/// is slow and the rest are not.
/// </para>
/// <para>
/// A single instance is safe to share and expensive to create, so register it as a singleton.
/// </para>
/// </remarks>
public sealed class LayaOnnxDecisionClient : IDecisionClient, IDisposable
{
    private readonly DecisionOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private Runtime? _runtime;
    private bool _disposed;

    /// <summary>
    /// Creates a client using the supplied configuration. The model is not loaded until the
    /// first call.
    /// </summary>
    /// <param name="options">The model source and decoding settings.</param>
    public LayaOnnxDecisionClient(DecisionOptions? options = null)
    {
        _options = options ?? new DecisionOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public async Task<ChoiceResult> ChooseAsync(
        string state,
        IReadOnlyDictionary<string, string?> options,
        string? instructions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(
            new DecisionRequest(state).Choose("choice", options, instructions),
            cancellationToken).ConfigureAwait(false);

        return result.Choice("choice");
    }

    /// <inheritdoc />
    public async Task<ScoreResult> ScoreAsync(
        string state,
        IReadOnlyList<string> levels,
        string? instructions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(
            new DecisionRequest(state).Score("score", levels, instructions),
            cancellationToken).ConfigureAwait(false);

        return result.Score("score");
    }

    /// <inheritdoc />
    public async Task<ProbabilityResult> AskAsync(
        string state,
        string proposition,
        CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(
            new DecisionRequest(state).Ask("ask", proposition),
            cancellationToken).ConfigureAwait(false);

        return result.Probability("ask");
    }

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (request.Questions.Count == 0)
        {
            throw new ArgumentException("A decision request must contain at least one question.", nameof(request));
        }

        var runtime = await EnsureRuntimeAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();

        var sequences = runtime.Builder.Build(request);

        cancellationToken.ThrowIfCancellationRequested();

        // ONNX Runtime work is synchronous and CPU-bound, so move it off the calling thread
        // rather than blocking a request thread for the duration of the forward pass.
        var logits = await Task.Run(() => runtime.Model.Run(sequences), cancellationToken)
            .ConfigureAwait(false);

        var answers = new Dictionary<string, object>(sequences.Count, StringComparer.Ordinal);
        var tokens = 0L;

        for (var i = 0; i < sequences.Count; i++)
        {
            var sequence = sequences[i];
            tokens += sequence.TokenIds.Count;

            var temperature = runtime.Config.TemperatureFor(sequence.QuestionType, sequence.Labels.Count);
            var clamped = runtime.Config.WasClampedFor(sequence.QuestionType, sequence.Labels.Count);
            var probabilities = DecisionMath.Softmax(logits[i], temperature);

            answers[sequence.Name] = Decode(sequence, probabilities, clamped);
        }

        return new DecisionResult(
            runtime.ModelIdentifier,
            tokens,
            Stopwatch.GetElapsedTime(started),
            answers);
    }

    private object Decode(LayaSequence sequence, double[] probabilities, bool calibrationClamped)
    {
        switch (sequence.Question.Type)
        {
            case DecisionQuestionType.Choice:
            {
                var map = new Dictionary<string, double>(sequence.Labels.Count, StringComparer.Ordinal);
                var best = 0;
                for (var i = 0; i < sequence.Labels.Count; i++)
                {
                    map[sequence.Labels[i]] = probabilities[i];
                    if (probabilities[i] > probabilities[best])
                    {
                        best = i;
                    }
                }

                return new ChoiceResult(
                    sequence.Labels[best],
                    map,
                    DecisionMath.AnswerConfidence(probabilities),
                    calibrationClamped);
            }

            case DecisionQuestionType.Score:
            {
                var map = new Dictionary<int, double>(sequence.Labels.Count);
                for (var i = 0; i < sequence.Labels.Count; i++)
                {
                    map[i] = probabilities[i];
                }

                return new ScoreResult(
                    DecisionMath.ExpectedScore(probabilities),
                    map,
                    sequence.Labels,
                    DecisionMath.AnswerConfidence(probabilities),
                    calibrationClamped);
            }

            default:
                // Noul options are rendered false-then-true, so index 1 is the probability of true.
                return new ProbabilityResult(probabilities[1], _options.DecisionThreshold, calibrationClamped);
        }
    }

    private async Task<Runtime> EnsureRuntimeAsync(CancellationToken cancellationToken)
    {
        if (_runtime is not null)
        {
            return _runtime;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_runtime is not null)
            {
                return _runtime;
            }

            var files = await LayaModelFiles.EnsureAsync(_options, cancellationToken).ConfigureAwait(false);

            var tokenizer = LayaTokenizer.Load(files.TokenizerPath);
            var config = LayaRuntimeConfig.Load(files.ConfigPath);
            var model = LayaModel.Load(files.ModelPath, tokenizer.PadTokenId, _options.IntraOpNumThreads);

            var identifier = string.IsNullOrWhiteSpace(_options.ModelPath)
                ? _options.ModelRepository
                : files.ModelPath;

            _runtime = new Runtime(
                tokenizer,
                config,
                model,
                new LayaSequenceBuilder(tokenizer, config),
                identifier);

            return _runtime;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runtime?.Model.Dispose();
        _runtime = null;
        _initializationLock.Dispose();
    }

    private sealed record Runtime(
        LayaTokenizer Tokenizer,
        LayaRuntimeConfig Config,
        LayaModel Model,
        LayaSequenceBuilder Builder,
        string ModelIdentifier);
}
