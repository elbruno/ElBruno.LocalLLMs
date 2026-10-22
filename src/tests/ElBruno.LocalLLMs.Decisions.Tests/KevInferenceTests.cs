using System.Diagnostics;
using System.Text.Json;
using ElBruno.LocalLLMs.Decisions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ElBruno.LocalLLMs.Decisions.Tests;

[Collection("Laya inference")]
public sealed class KevInferenceTests(ITestOutputHelper output)
{
    [KevFact]
    [Trait("Category", "Integration")]
    public async Task SharedClient_MatchesOriginalReferenceFromFreeTextWithoutHttp()
    {
        var path = Environment.GetEnvironmentVariable("KEV_MODEL_PATH");
        Assert.False(string.IsNullOrWhiteSpace(path), "Set KEV_MODEL_PATH to a directory produced by convert_kev.py.");
        var httpAttempts = 0;
        using var guard = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = _ =>
            {
                Interlocked.Increment(ref httpAttempts);
                throw new InvalidOperationException("HTTP is disabled during Kev inference.");
            },
        };
        ActivitySource.AddActivityListener(guard);
        using var http = new HttpClient();
        var control = await Record.ExceptionAsync(() => http.GetStringAsync("https://huggingface.co"));
        Assert.NotNull(control);
        Assert.Contains("HTTP is disabled during Kev inference.", control.ToString());
        Assert.Equal(1, httpAttempts);

        var tokenizer = KevTokenizer.Load(Path.Combine(path!, "tokenizer.json"));
        using var tokenizerCases = JsonDocument.Parse(File.ReadAllText(Path.Combine(path!, "tokenizer_cases.json")));
        foreach (var item in tokenizerCases.RootElement.EnumerateArray())
        {
            Assert.Equal(item.GetProperty("ids").EnumerateArray().Select(value => value.GetInt32()),
                tokenizer.EncodeUserText(item.GetProperty("text").GetString()!));
        }
        var services = new ServiceCollection().AddLocalKevDecisions(path!);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IDecisionClient>();
        Assert.Same(client, provider.GetRequiredService<KevOnnxDecisionClient>());
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(path!, "reference_cases.json")));
        Assert.Equal(6, cases.RootElement.GetArrayLength());
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var state = item.GetProperty("state").GetString()!;
            var instruction = item.GetProperty("instruction").GetString()!;
            var options = item.GetProperty("options").EnumerateArray().Select(value => value.GetString()!).ToArray();
            var expected = item.GetProperty("probabilities").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            var sequence = tokenizer.Build(state, instruction, options);
            Assert.Equal(item.GetProperty("ids").EnumerateArray().Select(value => value.GetInt64()), sequence.TokenIds);
            Assert.Equal(item.GetProperty("option_positions").EnumerateArray().Select(value => value.GetInt64()), sequence.OptionPositions);
            var timer = Stopwatch.StartNew();
            double[] probabilities;
            if (name.StartsWith("score", StringComparison.Ordinal))
            {
                var score = await client.ScoreAsync(state, instruction, options);
                probabilities = score.LevelProbabilities.ToArray();
                Assert.InRange(Math.Abs(score.Score - expected.Select((value, index) => index * value).Sum()), 0, 1e-4);
            }
            else if (name.StartsWith("assess", StringComparison.Ordinal))
            {
                var assessment = await client.AssessAsync(state, instruction);
                probabilities = [1 - assessment.Probability, assessment.Probability];
                Assert.Equal(name == "assess-yes", assessment.Probability > 0.5);
            }
            else
            {
                var labels = options.Select(option => option.Split(": ", 2, StringSplitOptions.None))
                    .ToDictionary(parts => parts[0], parts => (string?)parts[1]);
                var choice = await client.ChooseAsync(state, instruction, labels);
                Assert.Equal(labels.Keys, choice.Probabilities.Keys);
                probabilities = choice.Probabilities.Values.ToArray();
                Assert.Equal(name == "billing" ? "Billing / Refunds" : "TECH.support", choice.Choice);
                Assert.InRange(choice.Confidence, 0, 1);
            }
            Assert.Equal(expected.Length, probabilities.Length);
            Assert.All(probabilities, probability => Assert.True(double.IsFinite(probability) && probability is >= 0 and <= 1));
            Assert.InRange(Math.Abs(probabilities.Sum() - 1), 0, 1e-10);
            var error = probabilities.Zip(expected, (actual, reference) => Math.Abs(actual - reference)).Max();
            Assert.InRange(error, 0, 1e-4);
            output.WriteLine($"{name}: {timer.Elapsed.TotalMilliseconds:F1} ms; maximum probability error {error:G9}");
        }
        var parallel = await Task.WhenAll(
            client.AssessAsync("The product arrived destroyed.", "The product is damaged."),
            client.AssessAsync("The product arrived undamaged.", "The product is damaged."));
        Assert.True(parallel[0].Probability > parallel[1].Probability);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AssessAsync(
            string.Join(" ", Enumerable.Repeat("The product arrived destroyed.", 150)),
            "The product is damaged.", cancellation.Token));
        var recovered = await client.AssessAsync("The product arrived destroyed.", "The product is damaged.");
        Assert.InRange(Math.Abs(recovered.Probability - parallel[0].Probability), 0, 1e-4);
        Assert.Equal(1, httpAttempts);
    }
}

internal sealed class KevFactAttribute : FactAttribute
{
    public KevFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_KEV_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Opt in with RUN_KEV_TESTS=true and KEV_MODEL_PATH pointing to an acquired Kev export.";
    }
}
