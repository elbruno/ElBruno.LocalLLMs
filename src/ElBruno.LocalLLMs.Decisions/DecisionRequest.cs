using System.Collections.ObjectModel;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// The kind of answer a question expects.
/// </summary>
public enum DecisionQuestionType
{
    /// <summary>Selects one caller-defined label from a set.</summary>
    Choice,

    /// <summary>Scores against an ordered rubric.</summary>
    Score,

    /// <summary>Assesses the probability of a proposition being true.</summary>
    Probability
}

/// <summary>
/// A single named question within a <see cref="DecisionRequest"/>.
/// </summary>
public sealed class DecisionQuestion
{
    internal DecisionQuestion(
        DecisionQuestionType type,
        string? instructions,
        IReadOnlyDictionary<string, string?>? options,
        IReadOnlyList<string>? levels)
    {
        Type = type;
        Instructions = instructions;
        Options = options;
        Levels = levels;
    }

    /// <summary>Gets the answer kind this question expects.</summary>
    public DecisionQuestionType Type { get; }

    /// <summary>Gets the natural-language instructions, or <c>null</c> when omitted.</summary>
    public string? Instructions { get; }

    /// <summary>Gets the option labels and descriptions for a choice question, otherwise <c>null</c>.</summary>
    public IReadOnlyDictionary<string, string?>? Options { get; }

    /// <summary>Gets the ordered rubric levels for a score question, otherwise <c>null</c>.</summary>
    public IReadOnlyList<string>? Levels { get; }
}

/// <summary>
/// A batch of independent questions evaluated against one shared piece of text.
/// </summary>
/// <remarks>
/// Every question in a request is answered in a single forward pass, so asking five questions
/// together costs roughly the same as asking one. Prefer batching over sequential calls.
/// Instances are immutable; each <c>Choose</c>, <c>Score</c> or <c>Ask</c> returns a new request.
/// </remarks>
public sealed class DecisionRequest
{
    private readonly Dictionary<string, DecisionQuestion> _questions;

    /// <summary>
    /// Creates a request for the supplied text with no questions attached.
    /// </summary>
    /// <param name="state">The text to reason about. Must not be null or whitespace.</param>
    public DecisionRequest(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        State = state;
        _questions = new Dictionary<string, DecisionQuestion>(StringComparer.Ordinal);
        Questions = new ReadOnlyDictionary<string, DecisionQuestion>(_questions);
    }

    private DecisionRequest(string state, Dictionary<string, DecisionQuestion> questions)
    {
        State = state;
        _questions = questions;
        Questions = new ReadOnlyDictionary<string, DecisionQuestion>(questions);
    }

    /// <summary>Gets the shared text all questions are evaluated against.</summary>
    public string State { get; }

    /// <summary>Gets the questions, keyed by the names supplied when they were added.</summary>
    public IReadOnlyDictionary<string, DecisionQuestion> Questions { get; }

    /// <summary>
    /// Adds a choice question that selects one of <paramref name="options"/>.
    /// </summary>
    /// <param name="name">The result key used to read the answer back. Must be unique within the request.</param>
    /// <param name="options">Option labels mapped to optional descriptions. Between 1 and 255 entries.</param>
    /// <param name="instructions">Optional natural-language guidance, such as a question to answer.</param>
    /// <returns>A new request including this question.</returns>
    public DecisionRequest Choose(
        string name,
        IReadOnlyDictionary<string, string?> options,
        string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A choice question requires 1-255 options.");
        }

        var copy = new Dictionary<string, string?>(options, StringComparer.Ordinal);
        return Add(name, new DecisionQuestion(DecisionQuestionType.Choice, instructions, copy, null));
    }

    /// <summary>
    /// Adds a choice question whose options carry no descriptions.
    /// </summary>
    /// <param name="name">The result key used to read the answer back.</param>
    /// <param name="options">The option labels, in any order.</param>
    /// <param name="instructions">Optional natural-language guidance.</param>
    /// <returns>A new request including this question.</returns>
    public DecisionRequest Choose(string name, IEnumerable<string> options, string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var map = options.ToDictionary(option => option, _ => (string?)null, StringComparer.Ordinal);
        return Choose(name, map, instructions);
    }

    /// <summary>
    /// Adds a score question against an ordered rubric.
    /// </summary>
    /// <param name="name">The result key used to read the answer back.</param>
    /// <param name="levels">
    /// Between 2 and 10 rubric levels, ordered from lowest to highest. Each level should be
    /// self-contained, because the model sees the level text rather than its position.
    /// </param>
    /// <param name="instructions">Optional natural-language guidance.</param>
    /// <returns>A new request including this question.</returns>
    public DecisionRequest Score(string name, IReadOnlyList<string> levels, string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count is < 2 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), "A score question requires 2-10 ordered levels.");
        }

        return Add(name, new DecisionQuestion(DecisionQuestionType.Score, instructions, null, levels.ToArray()));
    }

    /// <summary>
    /// Adds a yes/no proposition answered with a probability.
    /// </summary>
    /// <param name="name">The result key used to read the answer back.</param>
    /// <param name="proposition">The statement to assess, phrased so that "true" is unambiguous.</param>
    /// <returns>A new request including this question.</returns>
    public DecisionRequest Ask(string name, string proposition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposition);
        return Add(name, new DecisionQuestion(DecisionQuestionType.Probability, proposition, null, null));
    }

    private DecisionRequest Add(string name, DecisionQuestion question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_questions.ContainsKey(name))
        {
            throw new ArgumentException($"A question named '{name}' was already added.", nameof(name));
        }

        var questions = new Dictionary<string, DecisionQuestion>(_questions, StringComparer.Ordinal)
        {
            [name] = question
        };

        return new DecisionRequest(State, questions);
    }
}
