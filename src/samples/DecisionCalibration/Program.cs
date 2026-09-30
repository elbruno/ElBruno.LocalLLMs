using ElBruno.LocalLLMs.Decisions;

// Demonstrates the two numerical caveats of the Laya ONNX checkpoint that are easy to
// trip over and hard to notice: a clamped calibration bucket, and fp16 batch variance.
//
// Usage: dotnet run [path-to-local-model-directory]
//        With no argument the model is downloaded from HuggingFace on first use (~800 MB).

var options = new DecisionOptions();
if (args.Length > 0)
{
    options.ModelPath = args[0];
}

using var client = new LayaOnnxDecisionClient(options);

Console.WriteLine($"model: {(args.Length > 0 ? args[0] : options.ModelRepository)}");
Console.WriteLine("loading (first run downloads the checkpoint)...\n");

await ShowCalibrationClampAsync(client);
await ShowBatchVarianceAsync(client);

// ---------------------------------------------------------------------------------------
// 1. Calibration clamp
// ---------------------------------------------------------------------------------------
// Laya's English checkpoint fits a temperature per (question type, option count) bucket.
// The choice:11+ bucket ships as 0.1006, far below Laya's own TEMP_MIN of 0.5. Dividing
// the logits by it sharpens the distribution roughly tenfold, which would report a
// genuinely uncertain answer as near-certain. The client clamps it back to 0.5 and sets
// CalibrationClamped so you can tell the difference.
static async Task ShowCalibrationClampAsync(IDecisionClient client)
{
    Console.WriteLine("=== 1. Calibration clamp (choice:11+) ===\n");

    const string ticket = "The invoice I received this morning is for the wrong amount.";

    // Five options land in the choice:3-5 bucket, which ships a valid fitted temperature.
    var small = await client.ChooseAsync(
        ticket,
        BuildDepartments(5),
        "Route this ticket to the right team");

    // Twelve options land in choice:11+, whose fitted temperature is out of range.
    var large = await client.ChooseAsync(
        ticket,
        BuildDepartments(12),
        "Route this ticket to the right team");

    Report("5 options  (choice:3-5)", small);
    Report("12 options (choice:11+)", large);

    Console.WriteLine(
        large.CalibrationClamped
            ? "  The 12-option answer used a clamped temperature. Both answers are correct, but\n"
            + "  note the confidence: clamping to 0.5 bounds the damage, it does not fix the\n"
            + "  calibration - a temperature below 1.0 still sharpens, so the number saturates.\n"
            + "  Use the ranking for 11+ options; ChoiceOrNull will never abstain here.\n"
            : "  This checkpoint ships a valid choice:11+ temperature; nothing was clamped.\n");

    static void Report(string title, ChoiceResult result)
    {
        Console.WriteLine($"  {title}");
        Console.WriteLine($"    choice             : {result.Choice}");
        Console.WriteLine($"    probability        : {result.Probability:P1}");
        Console.WriteLine($"    confidence         : {result.Confidence:F4}");
        Console.WriteLine($"    CalibrationClamped : {result.CalibrationClamped}");
        Console.WriteLine();
    }

    static Dictionary<string, string?> BuildDepartments(int count)
    {
        // "billing" stays first so both sizes answer the same underlying question.
        string[] all =
        [
            "billing", "technical", "sales", "shipping", "returns",
            "accounts", "security", "legal", "partnerships", "press",
            "careers", "feedback",
        ];

        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < count && i < all.Length; i++)
        {
            map[all[i]] = null;
        }

        return map;
    }
}

// ---------------------------------------------------------------------------------------
// 2. fp16 batch variance
// ---------------------------------------------------------------------------------------
// The published checkpoint stores fp16 weights. Running a question alone and running the
// same question alongside others produce slightly different numbers, because the batched
// matrix multiplications accumulate in a different order. The difference is around 1e-4:
// far too small to flip a routing decision, but large enough to break an exact-equality
// assertion or a cache keyed on the probability value.
static async Task ShowBatchVarianceAsync(IDecisionClient client)
{
    Console.WriteLine("=== 2. fp16 batch variance ===\n");

    const string ticket = "I was charged twice for my subscription this month and want a refund.";
    var departments = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["billing"] = null,
        ["technical"] = null,
        ["sales"] = null,
    };

    // Alone: a batch of one.
    var alone = await client.ChooseAsync(ticket, departments, "Route this ticket to the right team");

    // Batched: the identical question, sharing a forward pass with three others.
    var batchedResult = await client.EvaluateAsync(
        new DecisionRequest(ticket)
            .Choose("route", departments, "Route this ticket to the right team")
            .Score("urgency", ["not urgent", "normal", "urgent", "critical"], "How urgent is this?")
            .Ask("refund", "the customer is asking for a refund")
            .Ask("angry", "the customer is angry"));

    var batched = batchedResult.Choice("route");

    Console.WriteLine($"  {"label",-12} {"alone",12} {"batched",12}   delta");
    var worst = 0.0;
    foreach (var pair in alone.Probabilities.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        var other = batched.Probabilities[pair.Key];
        var delta = Math.Abs(pair.Value - other);
        worst = Math.Max(worst, delta);
        Console.WriteLine($"  {pair.Key,-12} {pair.Value,12:F8} {other,12:F8}   {delta:E2}");
    }

    Console.WriteLine();
    Console.WriteLine($"  same choice : {alone.Choice == batched.Choice} ({alone.Choice})");
    Console.WriteLine($"  max delta   : {worst:E2}");
    Console.WriteLine($"  batch cost  : {batchedResult.Duration.TotalMilliseconds:N0} ms for 4 questions");
    Console.WriteLine();
    Console.WriteLine("  Compare probabilities with a tolerance rather than ==, and do not persist");
    Console.WriteLine("  them as if they were stable identifiers. The ranking is what is reliable.");
}
