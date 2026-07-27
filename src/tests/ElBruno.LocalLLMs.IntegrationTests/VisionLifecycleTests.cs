using System.Diagnostics;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// End-to-end lifecycle tests for native-ONNX vision models (currently Fara1.5-9B).
///
/// Same 3-phase lifecycle as <see cref="ModelLifecycleTests"/> but uses
/// <see cref="LocalVisionChatClient"/> with a fixture PNG image.
///
/// Gated by RUN_INTEGRATION_TESTS=true.
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTestRun")]
public class VisionLifecycleTests
{
    private readonly TestRunReporter _reporter;

    public VisionLifecycleTests(TestRunReporter reporter)
    {
        _reporter = reporter;
    }

    [SkippableTheory]
    [MemberData(nameof(NativeOnnxVisionModels))]
    public async Task VisionModel_FullLifecycle_DownloadInferenceCacheHitDelete(ModelDefinition? model)
    {
        // Null sentinel: no vision models are currently available for testing.
        Skip.If(model is null, "No native ONNX vision models are available for lifecycle testing. " +
            "Check IntegrationTestModels.KnownExportIssueModelIds for excluded models.");

        if (!IsEnabled())
        {
            _reporter.RecordSkipped(model, "Vision");
            Skip.If(true, "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");
        }

        var cacheDir = Path.Combine(Path.GetTempPath(), $"localllms-vision-{model.Id.Replace('/', '-')}-{Guid.NewGuid():N}");
        ResultStatus phaseA = ResultStatus.Fail, phaseB = ResultStatus.Fail, phaseC = ResultStatus.Fail;
        TimeSpan? durationA = null, durationB = null, durationC = null;
        string? error = null;

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestFixtures", "red-shape.png");

        try
        {
            // ── Phase A: Fresh download ───────────────────────────────────────
            var swA = Stopwatch.StartNew();
            try
            {
                var options = BuildOptions(model, cacheDir);
                await using var client = await LocalVisionChatClient.CreateAsync(options);

                var visionOpts = new VisionChatOptions
                {
                    ImagePaths = [fixturePath],
                    MaxOutputTokens = 64
                };

                var response = await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Describe the image in one sentence.")],
                    visionOpts);

                Assert.False(string.IsNullOrWhiteSpace(response.Text),
                    "Phase A: vision response must not be empty");

                // Verify cache is populated after first download
                var sizeAfterDownload = LocalVisionChatClient.GetModelCacheSize(model, cacheDir);
                Assert.True(sizeAfterDownload > 0, $"Phase A: cache size should be > 0, got {sizeAfterDownload}");
                var listAfterDownload = LocalVisionChatClient.ListCachedModels(cacheDir);
                Assert.Contains(listAfterDownload, r => r.LocalDirectory.EndsWith(model.Id, StringComparison.OrdinalIgnoreCase));

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
                await using var client2 = await LocalVisionChatClient.CreateAsync(options);
                swB.Stop();

                var visionOpts = new VisionChatOptions
                {
                    ImagePaths = [fixturePath],
                    MaxOutputTokens = 64
                };

                var response2 = await client2.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Describe the image in one sentence.")],
                    visionOpts);

                Assert.False(string.IsNullOrWhiteSpace(response2.Text),
                    "Phase B: vision response must not be empty");
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
                await LocalVisionChatClient.DeleteModelFromCacheAsync(model, cacheDir);

                var modelDir = Path.Combine(cacheDir, model.Id);
                Assert.False(Directory.Exists(modelDir),
                    $"Phase C: model directory should be deleted: {modelDir}");
                var sizeAfterDelete = LocalVisionChatClient.GetModelCacheSize(model, cacheDir);
                Assert.Equal(0, sizeAfterDelete);
                var listAfterDelete = LocalVisionChatClient.ListCachedModels(cacheDir);
                Assert.DoesNotContain(listAfterDelete, r => r.LocalDirectory.EndsWith(model.Id, StringComparison.OrdinalIgnoreCase));

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
                model.Id, model.DisplayName, "Vision",
                phaseA, phaseB, phaseC,
                durationA, durationB, durationC,
                error));

            TryDeleteDirectory(cacheDir);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MemberData
    // ──────────────────────────────────────────────────────────────────────────

    public static TheoryData<ModelDefinition?> NativeOnnxVisionModels()
    {
        // When all vision models are excluded (e.g. due to known export issues),
        // return a null sentinel so xUnit doesn't fail with "0 parameter values provided".
        // The test body skips immediately when model is null.
        var source = IntegrationTestModels.NativeOnnxVisionModels;
        var data = new TheoryData<ModelDefinition?>();
        if (source.Count == 0)
        {
            data.Add(null);
        }
        else
        {
            foreach (var m in source)
                data.Add((ModelDefinition)m[0]);
        }
        return data;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static LocalLLMsOptions BuildOptions(ModelDefinition model, string cacheDir) => new()
    {
        Model = model,
        EnsureModelDownloaded = true,
        CacheDirectory = cacheDir,
        Temperature = 0.1f,
        MaxSequenceLength = 512
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
