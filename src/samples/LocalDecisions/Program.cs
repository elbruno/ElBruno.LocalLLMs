using ElBruno.LocalLLMs.Decisions;

// Triages a support ticket with a local System One decision model.
//
// Nothing to install and nothing to start: the model runs in-process on ONNX
// Runtime. It is downloaded from HuggingFace on first run (about 800 MB) and
// cached afterwards, so the first run is slow and the rest are not.
//
// Pass a local model directory as the first argument to skip the download.
//
// Every question below is answered in ONE forward pass, so asking four costs
// about what asking one costs.

using var client = new LayaOnnxDecisionClient(new DecisionOptions
{
    ModelPath = args.Length > 0 ? args[0] : null,
    DecisionThreshold = 0.5
});

string[] tickets =
{
    "My invoice charged me twice for the same subscription month and I want my money back.",
    "The export endpoint returns a 500 every time I pass more than 50 ids. Here is the stack trace.",
    "Hi! Just wondering what the difference is between the Pro and Team plans before we upgrade."
};

var teams = new Dictionary<string, string?>
{
    ["billing"] = "Payment, invoice and refund problems",
    ["technical"] = "Bugs, outages, API errors and anything with a stack trace",
    ["sales"] = "Pricing, plan comparisons, upgrades and new purchases"
};

string[] urgencyLevels =
{
    "no rush, answer whenever",
    "normal, answer within a day",
    "urgent, answer within an hour",
    "critical, the customer is blocked right now"
};

foreach (string ticket in tickets)
{
    Console.WriteLine(new string('-', 78));
    Console.WriteLine(Truncate(ticket, 76));
    Console.WriteLine();

    DecisionResult result;
    try
    {
        result = await client.EvaluateAsync(
            new DecisionRequest(ticket)
                .Choose("team", teams, "Which team should handle this support ticket?")
                .Score("urgency", urgencyLevels, "How urgent is this ticket?")
                .Ask("refund", "The customer is asking for a refund.")
                .Ask("angry", "The customer is angry or frustrated."));
    }
    catch (DecisionException ex)
    {
        Console.WriteLine($"  {ex.Message}");
        return 1;
    }

    ChoiceResult team = result.Choice("team");
    ScoreResult urgency = result.Score("urgency");
    ProbabilityResult refund = result.Probability("refund");
    ProbabilityResult angry = result.Probability("angry");

    // Routing on the probability rather than the label lets ambiguous tickets
    // fall through to a human instead of being confidently misrouted.
    string route = team.ChoiceOrNull(0.6) ?? "human-review";

    Console.WriteLine($"  route   : {route,-14} ({team.Choice} @ {team.Probability:P1})");
    Console.WriteLine($"  urgency : {urgency.MostLikelyLabel}");
    Console.WriteLine($"            score {urgency.Score:F2} of {urgency.Legend.Count - 1}");
    Console.WriteLine($"  refund  : {YesNo(refund)}");
    Console.WriteLine($"  angry   : {YesNo(angry)}");
    Console.WriteLine();
    Console.WriteLine($"  distribution: {string.Join("  ", team.Probabilities
        .OrderByDescending(p => p.Value)
        .Select(p => $"{p.Key} {p.Value:P0}"))}");
    Console.WriteLine($"  {result.InputTokens} input tokens, {result.Duration.TotalMilliseconds:F0} ms, model '{result.Model}'");
}

Console.WriteLine(new string('-', 78));
Console.WriteLine();
Console.WriteLine("Note: confidence values from Laya's public checkpoints are not reliably");
Console.WriteLine("calibrated. Validate any threshold against your own labelled tickets");
Console.WriteLine("before trusting it in production.");

return 0;

static string YesNo(ProbabilityResult result) =>
    $"{(result.IsTrue ? "yes" : "no"),-4} (p = {result.Probability:F3})";

static string Truncate(string value, int length) =>
    value.Length <= length ? value : string.Concat(value.AsSpan(0, length - 1), "\u2026");
