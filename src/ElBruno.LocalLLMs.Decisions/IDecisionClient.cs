namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Makes typed decisions about text using a local System One model.
/// </summary>
/// <remarks>
/// <para>
/// A decision model is not a chat model. It does not generate text; it produces a typed answer
/// with a probability distribution in a single forward pass, which makes it fast enough to sit on
/// a request path and cheap enough to call on every message.
/// </para>
/// <para>
/// Use it for routing, triage, moderation gates, intent detection and similar classification work,
/// and hand the result to a generative model when you actually need prose.
/// </para>
/// </remarks>
public interface IDecisionClient
{
    /// <summary>
    /// Selects one of <paramref name="options"/> for the supplied text.
    /// </summary>
    /// <param name="state">The text to reason about.</param>
    /// <param name="options">Option labels mapped to optional descriptions.</param>
    /// <param name="instructions">Optional natural-language guidance, such as a question to answer.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The selected label and the full distribution over options.</returns>
    Task<ChoiceResult> ChooseAsync(
        string state,
        IReadOnlyDictionary<string, string?> options,
        string? instructions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores the supplied text against an ordered rubric.
    /// </summary>
    /// <param name="state">The text to reason about.</param>
    /// <param name="levels">Between 2 and 10 rubric levels, ordered from lowest to highest.</param>
    /// <param name="instructions">Optional natural-language guidance.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The expected score and the distribution over rubric levels.</returns>
    Task<ScoreResult> ScoreAsync(
        string state,
        IReadOnlyList<string> levels,
        string? instructions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assesses how likely a proposition is to be true of the supplied text.
    /// </summary>
    /// <param name="state">The text to reason about.</param>
    /// <param name="proposition">The statement to assess, phrased so that "true" is unambiguous.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The probability that the proposition holds.</returns>
    Task<ProbabilityResult> AskAsync(
        string state,
        string proposition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers several independent questions about one piece of text in a single forward pass.
    /// </summary>
    /// <param name="request">The questions to answer and the text they share.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>All answers, read back by question name.</returns>
    /// <remarks>
    /// Batching is close to free: five questions cost roughly what one costs. Prefer this over
    /// issuing several single-question calls.
    /// </remarks>
    Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);
}
