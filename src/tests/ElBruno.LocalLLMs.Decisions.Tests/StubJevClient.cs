using System.Text.Json;
using ElBruno.AI.Jev;

namespace ElBruno.LocalLLMs.Decisions.Tests;

/// <summary>
/// A deterministic <see cref="IJevDecisionClient"/> that records the request it received and
/// returns a caller-supplied response, so the translation layer can be tested without a server.
/// </summary>
internal sealed class StubJevClient : IJevDecisionClient
{
    private readonly Func<JevDecisionRequest, JevDecisionResponse> _responder;

    public StubJevClient(Func<JevDecisionRequest, JevDecisionResponse> responder) => _responder = responder;

    public JevDecisionRequest? LastRequest { get; private set; }

    public int CallCount { get; private set; }

    public Task<JevDecisionResponse> EvaluateAsync(
        JevDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        CallCount++;
        return Task.FromResult(_responder(request));
    }

    public Task<JevModelList> ListModelsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <summary>Builds a response mirroring what laya-serve returns for the supplied questions.</summary>
    public static JevDecisionResponse LayaLike(JevDecisionRequest request)
    {
        var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);

        foreach ((string name, JevQuestion question) in request.Questions)
        {
            answers[name] = question switch
            {
                JevChoiceQuestion choice => BuildChoice(choice),
                JevScoreQuestion score => BuildScore(score),
                JevNoulQuestion => new JevNoulAnswer(0.7321, Raw("""{"type":"noul","noul":0.7321,"action":{"act_probability":0.42}}""")),
                _ => throw new InvalidOperationException("Unexpected question type.")
            };
        }

        return new JevDecisionResponse("laya-rl-agent", answers, new JevUsage(166, 0));
    }

    private static JevChoiceAnswer BuildChoice(JevChoiceQuestion question)
    {
        string[] labels = question.Criteria.Keys.ToArray();
        var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            probabilities[labels[i]] = i == 0 ? 0.9824 : Math.Round(0.0176 / (labels.Length - 1), 4);
        }

        return new JevChoiceAnswer(
            labels[0],
            probabilities,
            0.9085,
            Raw("""{"type":"choice","answer_confidence":0.9824,"action":{"act_probability":1.0}}"""));
    }

    private static JevScoreAnswer BuildScore(JevScoreQuestion question)
    {
        int count = question.Criteria.Count;
        var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
        var legend = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        // Deliberately non-uniform so that MostLikelyLevel differs from a naive first-index guess.
        for (int i = 0; i < count; i++)
        {
            probabilities[i.ToString()] = i == count - 2 ? 0.7 : Math.Round(0.3 / (count - 1), 4);
            legend[i.ToString()] = question.Criteria[i];
        }

        return new JevScoreAnswer(count - 2.0, probabilities, legend, 0.1015);
    }

    private static JsonElement Raw(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
