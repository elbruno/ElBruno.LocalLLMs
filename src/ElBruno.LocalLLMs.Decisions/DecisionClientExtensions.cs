namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Convenience overloads for <see cref="IDecisionClient"/>.
/// </summary>
public static class DecisionClientExtensions
{
    /// <summary>
    /// Selects one of <paramref name="options"/> for the supplied text, without option descriptions.
    /// </summary>
    /// <param name="client">The decision client.</param>
    /// <param name="state">The text to reason about.</param>
    /// <param name="options">The option labels. Descriptions are omitted, so the labels must be self-explanatory.</param>
    /// <param name="instructions">Optional natural-language guidance, such as a question to answer.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The selected label and the full distribution over options.</returns>
    /// <remarks>
    /// Descriptions matter more than they look: the model reads the option text, so
    /// <c>"billing"</c> alone is weaker than <c>"billing"</c> described as
    /// <c>"payment, invoice and refund problems"</c>. Prefer the dictionary overload when accuracy matters.
    /// </remarks>
    public static Task<ChoiceResult> ChooseAsync(
        this IDecisionClient client,
        string state,
        IEnumerable<string> options,
        string? instructions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        var map = options.ToDictionary(option => option, _ => (string?)null, StringComparer.Ordinal);
        return client.ChooseAsync(state, map, instructions, cancellationToken);
    }

    /// <summary>
    /// Assesses a proposition and returns only the boolean verdict, using the client's configured threshold.
    /// </summary>
    /// <param name="client">The decision client.</param>
    /// <param name="state">The text to reason about.</param>
    /// <param name="proposition">The statement to assess.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns><see langword="true"/> when the probability meets the configured threshold.</returns>
    /// <remarks>
    /// Discarding the probability discards the information you need to tune the threshold later.
    /// Prefer <see cref="IDecisionClient.AskAsync"/> unless you are certain a boolean is enough.
    /// </remarks>
    public static async Task<bool> IsTrueAsync(
        this IDecisionClient client,
        string state,
        string proposition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ProbabilityResult result = await client
            .AskAsync(state, proposition, cancellationToken)
            .ConfigureAwait(false);

        return result.IsTrue;
    }
}
