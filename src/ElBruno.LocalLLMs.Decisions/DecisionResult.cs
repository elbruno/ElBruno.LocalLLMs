namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// The answers to a <see cref="DecisionRequest"/>, read back by question name.
/// </summary>
public sealed class DecisionResult
{
    private readonly IReadOnlyDictionary<string, object> _answers;

    internal DecisionResult(
        string model,
        long? inputTokens,
        TimeSpan duration,
        IReadOnlyDictionary<string, object> answers)
    {
        Model = model;
        InputTokens = inputTokens;
        Duration = duration;
        _answers = answers;
    }

    /// <summary>Gets the model identifier reported by the server.</summary>
    public string Model { get; }

    /// <summary>
    /// Gets the number of input tokens consumed by the whole batch, or <c>null</c> when the
    /// server did not report usage.
    /// </summary>
    public long? InputTokens { get; }

    /// <summary>Gets the wall-clock time taken by the call, including transport.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the question names present in this result.</summary>
    public IReadOnlyCollection<string> Names => (IReadOnlyCollection<string>)_answers.Keys;

    /// <summary>
    /// Reads a choice answer.
    /// </summary>
    /// <param name="name">The question name supplied to <see cref="DecisionRequest.Choose(string, IReadOnlyDictionary{string, string?}, string?)"/>.</param>
    /// <returns>The choice outcome.</returns>
    /// <exception cref="KeyNotFoundException">No question with this name was answered.</exception>
    /// <exception cref="InvalidOperationException">The named question was not a choice question.</exception>
    public ChoiceResult Choice(string name) => Get<ChoiceResult>(name, "choice");

    /// <summary>
    /// Reads a score answer.
    /// </summary>
    /// <param name="name">The question name supplied to <see cref="DecisionRequest.Score"/>.</param>
    /// <returns>The score outcome.</returns>
    /// <exception cref="KeyNotFoundException">No question with this name was answered.</exception>
    /// <exception cref="InvalidOperationException">The named question was not a score question.</exception>
    public ScoreResult Score(string name) => Get<ScoreResult>(name, "score");

    /// <summary>
    /// Reads a yes/no probability answer.
    /// </summary>
    /// <param name="name">The question name supplied to <see cref="DecisionRequest.Ask"/>.</param>
    /// <returns>The probability outcome.</returns>
    /// <exception cref="KeyNotFoundException">No question with this name was answered.</exception>
    /// <exception cref="InvalidOperationException">The named question was not a proposition.</exception>
    public ProbabilityResult Probability(string name) => Get<ProbabilityResult>(name, "probability");

    private T Get<T>(string name, string expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_answers.TryGetValue(name, out object? answer))
        {
            throw new KeyNotFoundException(
                $"No answer named '{name}'. Available answers: {string.Join(", ", _answers.Keys)}.");
        }

        if (answer is not T typed)
        {
            throw new InvalidOperationException(
                $"Answer '{name}' is not a {expected} answer; the model returned {answer.GetType().Name}.");
        }

        return typed;
    }
}
