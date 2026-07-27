using System.Diagnostics;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// End-to-end lifecycle tests for all native-ONNX text (GenAI) models.
///
/// Each test runs 3 phases per model:
///   A. Fresh download — cleans cache, creates client, verifies inference, disposes.
///   B. Cache hit     — creates client again (same cache), verifies fast load, verifies inference, disposes.
///   C. Delete        — removes model from cache, asserts the directory is gone.
///
/// Gated by RUN_INTEGRATION_TESTS=true. Results are collected by TestRunReporter and written
/// to docs/tests/YYYY-MM-DD-HH-run-results.md after the run.
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTestRun")]
public class ModelLifecycleTests
{
    private readonly TestRunReporter _reporter;

    public ModelLifecycleTests(TestRunReporter reporter)
    {
        _reporter = reporter;
    }

    [SkippableTheory]
    [MemberData(nameof(PracticalTextModels))]
    public async Task TextModel_FullLifecycle_DownloadInferenceCacheHitDelete(ModelDefinition model)
    {
        if (!IsEnabled())
        {
            _reporter.RecordSkipped(model, "Text (GenAI)");
            Skip.If(true, "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");
        }

        var cacheDir = Path.Combine(Path.GetTempPath(), $"localllms-lifecycle-{model.Id.Replace('/', '-')}-{Guid.NewGuid():N}");
        ResultStatus phaseA = ResultStatus.Fail, phaseB = ResultStatus.Fail, phaseC = ResultStatus.Fail;
        TimeSpan? durationA = null, durationB = null, durationC = null;
        string? error = null;

        try
        {
            // ── Phase A: Fresh download ───────────────────────────────────────
            var swA = Stopwatch.StartNew();
            try
            {
                var options = BuildOptions(model, cacheDir);
                await using var client = await LocalChatClient.CreateAsync(options);

                var response = await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "What is 2+2? Reply with ONLY the digit.")],
                    new ChatOptions { MaxOutputTokens = 32 });

                Assert.False(string.IsNullOrWhiteSpace(response.Text), "Phase A: response must not be empty");
                Assert.Contains("4", response.Text, StringComparison.OrdinalIgnoreCase);
                phaseA = ResultStatus.Pass;
            }
            catch (Exception ex)
            {
                error = $"Phase A: {ex.Message}";
                throw;
            }
            finally
            {
                swA.Stop();
                durationA = swA.Elapsed;
            }

            // ── Phase B: Cache hit ────────────────────────────────────────────
            var swB = Stopwatch.StartNew();
            try
            {
                var options = BuildOptions(model, cacheDir);
                await using var client2 = await LocalChatClient.CreateAsync(options);
                swB.Stop();

                // A cache hit should be much faster than a full download (no network).
                // We can't assert exact time without knowing download speed, but we note it in the report.
                var response2 = await client2.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "What is 2+2? Reply with ONLY the digit.")],
                    new ChatOptions { MaxOutputTokens = 32 });

                Assert.False(string.IsNullOrWhiteSpace(response2.Text), "Phase B: response must not be empty");
                Assert.Contains("4", response2.Text, StringComparison.OrdinalIgnoreCase);
                phaseB = ResultStatus.Pass;
            }
            catch (Exception ex)
            {
                if (error is null) error = $"Phase B: {ex.Message}";
                throw;
            }
            finally
            {
                if (!swB.IsRunning) durationB = swB.Elapsed;
            }

            // ── Phase C: Delete from cache ────────────────────────────────────
            var swC = Stopwatch.StartNew();
            try
            {
                await LocalChatClient.DeleteModelFromCacheAsync(model, cacheDir);

                // The sanitized model directory should no longer exist
                var sanitizedId = model.Id.Replace('/', '-').Replace('\\', '-');
                var modelDir = Path.Combine(cacheDir, sanitizedId);
                Assert.False(Directory.Exists(modelDir), $"Phase C: model directory should be deleted: {modelDir}");

                phaseC = ResultStatus.Pass;
            }
            catch (Exception ex)
            {
                if (error is null) error = $"Phase C: {ex.Message}";
                throw;
            }
            finally
            {
                swC.Stop();
                durationC = swC.Elapsed;
            }
        }
        finally
        {
            _reporter.RecordResult(new ModelTestResult(
                model.Id, model.DisplayName, "Text (GenAI)",
                phaseA, phaseB, phaseC,
                durationA, durationB, durationC,
                error));

            // Always clean up temp cache root
            TryDeleteDirectory(cacheDir);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MemberData
    // ──────────────────────────────────────────────────────────────────────────

    public static TheoryData<ModelDefinition> PracticalTextModels()
        => IntegrationTestModels.PracticalTextModels;

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static LocalLLMsOptions BuildOptions(ModelDefinition model, string cacheDir) => new()
    {
        Model = model,
        EnsureModelDownloaded = true,
        CacheDirectory = cacheDir,
        Temperature = 0.1f,
        MaxSequenceLength = 64
    };

    private static bool IsEnabled()
    {
        var val = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }
}
