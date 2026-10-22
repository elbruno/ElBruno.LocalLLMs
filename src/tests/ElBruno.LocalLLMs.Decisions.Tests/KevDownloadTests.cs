using System.Diagnostics;
using ElBruno.LocalLLMs.Decisions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ElBruno.LocalLLMs.Decisions.Tests;

[Collection("Laya inference")]
public sealed class KevDownloadTests(ITestOutputHelper output)
{
    [KevDownloadFact]
    [Trait("Category", "Integration")]
    public async Task EmptyCache_DownloadsPublicExportAndNewClientRunsOfflineWithReferenceParity()
    {
        var cache = Environment.GetEnvironmentVariable("KEV_DOWNLOAD_CACHE");
        var temporary = cache is null;
        cache ??= Directory.CreateTempSubdirectory("kev-live-download-").FullName;
        try
        {
            var options = new KevDecisionOptions { CacheDirectory = cache };
            Assert.False(Directory.Exists(options.ResolveModelDirectory()), "Use a new KEV_DOWNLOAD_CACHE to exercise a real cold download.");
            var timer = Stopwatch.StartNew();
            var services = new ServiceCollection();
            services.AddLocalKevDecisions(configure => configure.CacheDirectory = cache);
            using (var provider = services.BuildServiceProvider())
            {
                Assert.False(Directory.Exists(options.ResolveModelDirectory()));
                await AssertReferenceAsync(provider.GetRequiredService<IDecisionClient>());
                output.WriteLine($"Public acquisition plus initialization and six requests: {timer.Elapsed.TotalSeconds:F2} seconds.");
            }

            var attempts = 0;
            using var guard = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "System.Net.Http",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = _ =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("HTTP disabled for Kev cache reuse.");
                },
            };
            ActivitySource.AddActivityListener(guard);
            using var http = new HttpClient();
            var control = await Record.ExceptionAsync(() => http.GetStringAsync("https://huggingface.co"));
            Assert.NotNull(control);
            Assert.Contains("HTTP disabled for Kev cache reuse.", control.ToString());
            Assert.Equal(1, attempts);
            timer.Restart();
            using var cached = new KevOnnxDecisionClient(options);
            await AssertReferenceAsync(cached);
            output.WriteLine($"Fresh client, existing cache, HTTP blocked: {timer.Elapsed.TotalSeconds:F2} seconds.");
            Assert.Equal(1, attempts);
        }
        finally
        {
            if (temporary) Directory.Delete(cache, recursive: true);
        }
    }

    private static async Task AssertReferenceAsync(IDecisionClient client)
    {
        const string instruction = "Which team should handle this support ticket?";
        var labels = new Dictionary<string, string?>
        {
            ["Billing / Refunds"] = "Payment, invoice and refund problems",
            ["TECH.support"] = "Software bugs, outages and API errors",
            ["sales"] = "Pricing, upgrades and new purchases",
        };
        var billing = await client.ChooseAsync("I need help with a billing problem.", instruction, labels);
        var technical = await client.ChooseAsync("I need help with a technical problem.", instruction, labels);
        Assert.Equal("Billing / Refunds", billing.Choice);
        Assert.Equal("TECH.support", technical.Choice);
        Assert.Equal(labels.Keys, billing.Probabilities.Keys);
        Distribution(billing.Probabilities.Values, [0.95789039135, 0.01767352223, 0.02443610504]);
        Distribution(technical.Probabilities.Values, [0.00719668483, 0.98602491617, 0.00677836221]);
        string[] rubric = ["Undamaged", "Minor damage", "Destroyed"];
        var low = await client.ScoreAsync("The product arrived undamaged.", "How damaged is the product?", rubric);
        var high = await client.ScoreAsync("The product arrived destroyed.", "How damaged is the product?", rubric);
        Distribution(low.LevelProbabilities, [0.92006760836, 0.04287253320, 0.03705981746]);
        Distribution(high.LevelProbabilities, [0.01052887551, 0.01166078914, 0.97781026363]);
        var yes = await client.AssessAsync("The product arrived destroyed.", "The product is damaged.");
        var no = await client.AssessAsync("The product arrived undamaged.", "The product is damaged.");
        Assert.InRange(Math.Abs(yes.Probability - 0.71598082781), 0, 1e-4);
        Assert.InRange(Math.Abs(no.Probability - 0.31653368473), 0, 1e-4);
    }

    private static void Distribution(IEnumerable<double> actual, double[] expected)
    {
        var values = actual.ToArray();
        Assert.Equal(expected.Length, values.Length);
        Assert.All(values, value => Assert.True(double.IsFinite(value) && value is >= 0 and <= 1));
        Assert.InRange(Math.Abs(values.Sum() - 1), 0, 1e-10);
        Assert.All(values.Zip(expected), pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 1e-4));
    }
}

internal sealed class KevDownloadFactAttribute : FactAttribute
{
    public KevDownloadFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_KEV_DOWNLOAD_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Opt in with RUN_KEV_DOWNLOAD_TESTS=true to download the public 3.1 GB Kev export into an empty cache.";
    }
}
