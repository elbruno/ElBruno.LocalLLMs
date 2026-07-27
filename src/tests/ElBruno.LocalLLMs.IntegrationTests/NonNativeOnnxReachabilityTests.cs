using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Tests for models that do not have native ONNX support (<see cref="ModelDefinition.HasNativeOnnx"/> = false).
///
/// These models cannot be auto-downloaded — ONNX files must be obtained separately.
/// For each model:
/// 1. Verifies the HuggingFace repository is publicly reachable.
/// 2. If the environment variable <c>MODEL_PATH_{sanitized-id}</c> is set, runs a full
///    3-phase lifecycle test using the provided local path.
///
/// Gated by RUN_INTEGRATION_TESTS=true for the full lifecycle path.
/// Reachability checks always run when RUN_INTEGRATION_TESTS=true.
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTestRun")]
public class NonNativeOnnxReachabilityTests
{
    private readonly TestRunReporter _reporter;

    public NonNativeOnnxReachabilityTests(TestRunReporter reporter)
    {
        _reporter = reporter;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HuggingFace reachability for all non-native models
    // ──────────────────────────────────────────────────────────────────────────

    [SkippableTheory]
    [MemberData(nameof(NonNativeOnnxModels))]
    public async Task NonNativeModel_HuggingFaceRepo_IsReachable(ModelDefinition model)
    {
        Skip.IfNot(IsEnabled(), "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.Add("User-Agent", "ElBruno.LocalLLMs.Tests/1.0");

        var url = $"https://huggingface.co/api/models/{model.HuggingFaceRepoId}";
        var response = await http.GetAsync(url);

        Assert.True(response.IsSuccessStatusCode,
            $"Model '{model.Id}' repo '{model.HuggingFaceRepoId}' is not publicly accessible — " +
            $"got HTTP {(int)response.StatusCode}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Full lifecycle via ModelPath env var
    // ──────────────────────────────────────────────────────────────────────────

    [SkippableTheory]
    [MemberData(nameof(NonNativeOnnxModels))]
    public async Task NonNativeModel_WithModelPath_FullLifecycle(ModelDefinition model)
    {
        Skip.IfNot(IsEnabled(), "Integration tests disabled. Set RUN_INTEGRATION_TESTS=true to enable.");

        var envKey = $"MODEL_PATH_{SanitizeEnvKey(model.Id)}";
        var modelPath = Environment.GetEnvironmentVariable(envKey);

        if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
        {
            _reporter.RecordResult(new ModelTestResult(
                model.Id, model.DisplayName, "Non-Native ONNX",
                ResultStatus.Skip, ResultStatus.Skip, ResultStatus.Skip,
                null, null, null,
                $"Set {envKey}=<path> to enable lifecycle test"));
            Skip.If(true, $"Env var {envKey} not set. Provide the local ONNX model path to run lifecycle tests.");
        }

        ResultStatus phaseA = ResultStatus.Fail, phaseB = ResultStatus.Fail, phaseC = ResultStatus.NotApplicable;
        TimeSpan? durationA = null, durationB = null;
        string? error = null;

        try
        {
            // Phase A — inference with provided ModelPath
            var swA = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var options = new LocalLLMsOptions
                {
                    Model = model,
                    ModelPath = modelPath,
                    EnsureModelDownloaded = false,
                    Temperature = 0.1f,
                    MaxSequenceLength = 64
                };

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

            // Phase B — same ModelPath, verify inference works again (no download involved)
            var swB = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var options = new LocalLLMsOptions
                {
                    Model = model,
                    ModelPath = modelPath,
                    EnsureModelDownloaded = false,
                    Temperature = 0.1f,
                    MaxSequenceLength = 64
                };

                await using var client2 = await LocalChatClient.CreateAsync(options);
                swB.Stop();

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
        }
        finally
        {
            _reporter.RecordResult(new ModelTestResult(
                model.Id, model.DisplayName, "Non-Native ONNX",
                phaseA, phaseB, phaseC,
                durationA, durationB, null,
                error));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MemberData
    // ──────────────────────────────────────────────────────────────────────────

    public static TheoryData<ModelDefinition> NonNativeOnnxModels()
        => IntegrationTestModels.NonNativeOnnxModels;

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static bool IsEnabled()
    {
        var val = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Converts a model ID to a valid environment variable key component.</summary>
    private static string SanitizeEnvKey(string modelId)
        => modelId.ToUpperInvariant()
                  .Replace('.', '_')
                  .Replace('-', '_')
                  .Replace('/', '_');
}
