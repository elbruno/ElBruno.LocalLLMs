using System.Globalization;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// One tokenized question, ready to be batched into a forward pass.
/// </summary>
/// <param name="Name">The caller's question name, used to key the answer.</param>
/// <param name="Question">The question this sequence was built from.</param>
/// <param name="TokenIds">The full input sequence.</param>
/// <param name="MarkerPositions">The index of each option's <c>[MASK]</c> marker within <paramref name="TokenIds"/>.</param>
/// <param name="Labels">The option labels in marker order, as the caller supplied them.</param>
/// <param name="QuestionType">The type index the model expects: 0 choice, 1 score, 2 noul.</param>
internal sealed record LayaSequence(
    string Name,
    DecisionQuestion Question,
    IReadOnlyList<int> TokenIds,
    IReadOnlyList<int> MarkerPositions,
    IReadOnlyList<string> Labels,
    int QuestionType);

/// <summary>
/// Builds the input sequence Laya's checkpoints were trained on.
/// </summary>
/// <remarks>
/// <para>
/// The format is <c>[CLS] {type} question: {instructions} [SEP] [MASK] opt0 [MASK] opt1 … [SEP] state [SEP]</c>.
/// Every option is rendered as ordinary text preceded by a <c>[MASK]</c> marker, and the model
/// emits one logit per marker. That is why arbitrary caller-supplied labels work: nothing in the
/// graph is indexed by label, so the option set can change per call.
/// </para>
/// <para>
/// The budgets are applied in Laya's order — options first, then instructions, then whatever
/// room is left goes to the state. Changing that order changes the token ids and therefore the
/// answer.
/// </para>
/// </remarks>
internal sealed class LayaSequenceBuilder(LayaTokenizer tokenizer, LayaRuntimeConfig config)
{
    /// <summary>
    /// Builds one sequence per question, reusing a single tokenization of the shared state.
    /// </summary>
    /// <param name="request">The questions and the text they share.</param>
    /// <returns>One sequence per question, in request order.</returns>
    public IReadOnlyList<LayaSequence> Build(DecisionRequest request)
    {
        // Laya tokenizes the state once and reuses it for every question in the batch.
        var stateIds = tokenizer.Encode(StripMaskToken(request.State));

        var sequences = new List<LayaSequence>(request.Questions.Count);
        foreach (var (name, question) in request.Questions)
        {
            sequences.Add(BuildOne(name, question, stateIds));
        }

        return sequences;
    }

    private LayaSequence BuildOne(string name, DecisionQuestion question, IReadOnlyList<int> stateIds)
    {
        var questionType = ToQuestionType(question.Type);
        var typeName = questionType switch
        {
            0 => "choice",
            1 => "score",
            _ => "noul",
        };

        var (labels, optionTexts) = RenderOptions(question);

        var instructions = StripMaskToken(question.Instructions ?? string.Empty);
        var headIds = tokenizer.Encode(
            string.Create(CultureInfo.InvariantCulture, $"{typeName} question: {instructions}"));

        var optionIds = new List<List<int>>(optionTexts.Count);
        foreach (var optionText in optionTexts)
        {
            // Truncate at the tokenizer rather than after the fact, so a very long description
            // costs the same as a short one.
            var tokens = tokenizer.EncodeTruncated(
                " " + StripMaskToken(optionText),
                LayaRuntimeConfig.MaxOptionTokens);

            var option = new List<int>(tokens.Count + 1) { tokenizer.MaskTokenId };
            option.AddRange(tokens);
            optionIds.Add(option);
        }

        var optionBudget = config.HeadMaxLength - optionIds.Sum(option => option.Count);
        if (optionBudget < 16)
        {
            // Shrink every option evenly rather than dropping the ones at the end, so each option
            // keeps a marker and the answer still covers the full label set.
            var perOption = Math.Max(4, (config.HeadMaxLength - 16) / Math.Max(1, optionIds.Count));
            for (var i = 0; i < optionIds.Count; i++)
            {
                if (optionIds[i].Count > perOption)
                {
                    optionIds[i] = optionIds[i].GetRange(0, perOption);
                }
            }

            optionBudget = config.HeadMaxLength - optionIds.Sum(option => option.Count);
        }

        var headBudget = Math.Max(8, optionBudget);
        if (headIds.Count > headBudget)
        {
            headIds = headIds.Take(headBudget).ToArray();
        }

        var ids = new List<int>(config.MaxLength) { tokenizer.ClsTokenId };
        ids.AddRange(headIds);
        ids.Add(tokenizer.SepTokenId);

        var markers = new List<int>(optionIds.Count);
        foreach (var option in optionIds)
        {
            markers.Add(ids.Count);
            ids.AddRange(option);
        }

        ids.Add(tokenizer.SepTokenId);

        var room = Math.Max(0, config.MaxLength - ids.Count - 1);
        var used = Math.Min(room, stateIds.Count);
        for (var i = 0; i < used; i++)
        {
            ids.Add(stateIds[i]);
        }

        ids.Add(tokenizer.SepTokenId);

        if (ids.Count > config.MaxLength)
        {
            ids = ids.GetRange(0, config.MaxLength);
        }

        var keptMarkers = markers.Where(marker => marker < config.MaxLength).ToArray();
        if (keptMarkers.Length != labels.Count)
        {
            throw new DecisionException(
                $"Question '{name}' does not fit the model's {config.MaxLength}-token window: " +
                $"{labels.Count} options were supplied but only {keptMarkers.Length} could be scored. " +
                "Use fewer or shorter options.");
        }

        return new LayaSequence(name, question, ids, keptMarkers, labels, questionType);
    }

    private static (IReadOnlyList<string> Labels, IReadOnlyList<string> Texts) RenderOptions(DecisionQuestion question)
    {
        switch (question.Type)
        {
            case DecisionQuestionType.Choice:
            {
                var options = question.Options
                    ?? throw new DecisionException("A choice question must define its options.");

                var labels = new List<string>(options.Count);
                var texts = new List<string>(options.Count);
                foreach (var (label, description) in options)
                {
                    labels.Add(label);
                    texts.Add(string.IsNullOrEmpty(description) ? label : $"{label}: {description}");
                }

                return (labels, texts);
            }

            case DecisionQuestionType.Score:
            {
                var levels = question.Levels
                    ?? throw new DecisionException("A score question must define its rubric levels.");

                var labels = new List<string>(levels.Count);
                var texts = new List<string>(levels.Count);
                for (var i = 0; i < levels.Count; i++)
                {
                    labels.Add(levels[i]);
                    texts.Add(string.Create(CultureInfo.InvariantCulture, $"level {i}: {levels[i]}"));
                }

                return (labels, texts);
            }

            default:
                // Noul options are fixed and always in this order, so index 1 is always "true".
                return (
                    ["false", "true"],
                    ["false: no, the statement does not hold", "true: yes, the statement holds"]);
        }
    }

    private static int ToQuestionType(DecisionQuestionType type) => type switch
    {
        DecisionQuestionType.Choice => 0,
        DecisionQuestionType.Score => 1,
        _ => 2,
    };

    private static string StripMaskToken(string text) =>
        text.Contains(LayaTokenizer.MaskToken, StringComparison.Ordinal)
            ? text.Replace(LayaTokenizer.MaskToken, " ", StringComparison.Ordinal)
            : text;
}
