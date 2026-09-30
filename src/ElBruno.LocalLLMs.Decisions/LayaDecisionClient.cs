using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ElBruno.AI.Jev;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// An <see cref="IDecisionClient"/> backed by a local <c>laya-serve</c> instance, reached over the
/// Jev System One wire protocol.
/// </summary>
/// <remarks>
/// Start the server before using this client:
/// <code>
/// pip install "laya[serve]"
/// python -m laya.serve
/// </code>
/// The instance is thread-safe and intended to be registered as a singleton.
/// </remarks>
public sealed class LayaDecisionClient : IDecisionClient, IDisposable
{
    private readonly IJevDecisionClient _jev;
    private readonly DecisionOptions _options;
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>
    /// Creates a client that connects to the Laya server described by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Connection and threshold settings. Defaults target <c>http://127.0.0.1:8000</c>.</param>
    public LayaDecisionClient(DecisionOptions? options = null)
    {
        _options = options ?? new DecisionOptions();
        _options.Validate();

        _jev = new JevClient(new JevClientOptions
        {
            Endpoint = _options.Endpoint,
            ApiKey = _options.ApiKey,
            Timeout = _options.Timeout,
            UseLocalLaya = true
        });

        _ownsClient = true;
    }

    /// <summary>
    /// Creates a client over a caller-supplied Jev client. Intended for testing and for hosts that
    /// manage the underlying transport themselves.
    /// </summary>
    /// <param name="jevClient">The Jev decision client to delegate to.</param>
    /// <param name="options">Threshold settings. Connection settings are ignored.</param>
    public LayaDecisionClient(IJevDecisionClient jevClient, DecisionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(jevClient);
        _jev = jevClient;
        _options = options ?? new DecisionOptions();
        _options.Validate();
        _ownsClient = false;
    }

    /// <inheritdoc />
    public async Task<ChoiceResult> ChooseAsync(
        string state,
        IReadOnlyDictionary<string, string?> options,
        string? instructions = null,
        CancellationToken cancellationToken = default)
    {
        DecisionResult result = await EvaluateAsync(
            new DecisionRequest(state).Choose("result", options, instructions),
            cancellationToken).ConfigureAwait(false);

        return result.Choice("result");
    }

    /// <inheritdoc />
    public async Task<ScoreResult> ScoreAsync(
        string state,
        IReadOnlyList<string> levels,
        string? instructions = null,
        CancellationToken cancellationToken = default)
    {
        DecisionResult result = await EvaluateAsync(
            new DecisionRequest(state).Score("result", levels, instructions),
            cancellationToken).ConfigureAwait(false);

        return result.Score("result");
    }

    /// <inheritdoc />
    public async Task<ProbabilityResult> AskAsync(
        string state,
        string proposition,
        CancellationToken cancellationToken = default)
    {
        DecisionResult result = await EvaluateAsync(
            new DecisionRequest(state).Ask("result", proposition),
            cancellationToken).ConfigureAwait(false);

        return result.Probability("result");
    }

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Questions.Count == 0)
        {
            throw new ArgumentException("A decision request must contain at least one question.", nameof(request));
        }

        JevDecisionRequest jevRequest = BuildJevRequest(request);

        var stopwatch = Stopwatch.StartNew();
        JevDecisionResponse response;
        try
        {
            response = await _jev.EvaluateAsync(jevRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new DecisionException(
                $"Could not reach the local decision model at {_options.Endpoint}. " +
                "Start it with 'python -m laya.serve', or point DecisionOptions.Endpoint at the right port.",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DecisionException(
                $"The local decision model at {_options.Endpoint} did not respond within {_options.Timeout}. " +
                "The first call after startup loads the checkpoint and is slower; raise DecisionOptions.Timeout if needed.",
                ex);
        }

        stopwatch.Stop();

        return BuildResult(request, response, stopwatch.Elapsed);
    }

    private JevDecisionRequest BuildJevRequest(DecisionRequest request)
    {
        var jevRequest = new JevDecisionRequest(request.State, _options.Model);

        foreach ((string name, DecisionQuestion question) in request.Questions)
        {
            jevRequest = question.Type switch
            {
                DecisionQuestionType.Choice => jevRequest.WithQuestion(
                    new JevQuestionKey<JevChoiceAnswer>(name),
                    new JevChoiceQuestion(question.Instructions, question.Options!)),

                DecisionQuestionType.Score => jevRequest.WithQuestion(
                    new JevQuestionKey<JevScoreAnswer>(name),
                    new JevScoreQuestion(question.Instructions, question.Levels!)),

                DecisionQuestionType.Probability => jevRequest.WithQuestion(
                    new JevQuestionKey<JevNoulAnswer>(name),
                    new JevNoulQuestion(question.Instructions)),

                _ => throw new InvalidOperationException($"Unsupported question type '{question.Type}'.")
            };
        }

        return jevRequest;
    }

    private DecisionResult BuildResult(
        DecisionRequest request,
        JevDecisionResponse response,
        TimeSpan duration)
    {
        var answers = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach ((string name, DecisionQuestion question) in request.Questions)
        {
            if (!response.Answers.TryGetValue(name, out JevAnswer? answer))
            {
                throw new DecisionException(
                    $"The model did not answer question '{name}'. " +
                    $"Answered: {string.Join(", ", response.Answers.Keys)}.");
            }

            answers[name] = question.Type switch
            {
                DecisionQuestionType.Choice => Convert(name, answer as JevChoiceAnswer),
                DecisionQuestionType.Score => Convert(name, answer as JevScoreAnswer),
                DecisionQuestionType.Probability => Convert(name, answer as JevNoulAnswer, _options.DecisionThreshold),
                _ => throw new InvalidOperationException($"Unsupported question type '{question.Type}'.")
            };
        }

        return new DecisionResult(response.Model, response.Usage.InputTokens, duration, answers);
    }

    private static ChoiceResult Convert(string name, JevChoiceAnswer? answer)
    {
        Require(name, answer, "choice");

        var probabilities = answer!.Probabilities.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);

        return new ChoiceResult(answer.Choice, probabilities, answer.Confidence);
    }

    private static ScoreResult Convert(string name, JevScoreAnswer? answer)
    {
        Require(name, answer, "score");

        var probabilities = new Dictionary<int, double>();
        foreach ((string key, double value) in answer!.Probabilities)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
            {
                throw new DecisionException(
                    $"Answer '{name}' returned a non-numeric rubric level key '{key}'.");
            }

            probabilities[level] = value;
        }

        if (answer.Legend.Count != probabilities.Count)
        {
            throw new DecisionException(
                $"Answer '{name}' returned {probabilities.Count} rubric probabilities but " +
                $"{answer.Legend.Count} legend entries.");
        }

        var legend = new string[probabilities.Count];
        foreach ((string key, JsonElement value) in answer.Legend)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                || level < 0
                || level >= legend.Length)
            {
                throw new DecisionException(
                    $"Answer '{name}' returned an out-of-range legend key '{key}'.");
            }

            legend[level] = value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : value.ToString();
        }

        return new ScoreResult(answer.Score, probabilities, legend, answer.Confidence);
    }

    private static ProbabilityResult Convert(string name, JevNoulAnswer? answer, double threshold)
    {
        Require(name, answer, "yes/no");
        return new ProbabilityResult(answer!.Probability, threshold);
    }

    private static void Require(string name, JevAnswer? answer, string expected)
    {
        if (answer is null)
        {
            throw new DecisionException(
                $"The model answered question '{name}' with a different type than the requested {expected} question. " +
                "This usually means the checkpoint does not support that question type.");
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
        if (_ownsClient && _jev is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
