using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Execution;

/// <summary>
/// Regression coverage for the ORT-GenAI native divide-by-zero fix: setting the native
/// <c>"temperature"</c> search option to exactly <c>0</c> (or any non-positive value)
/// crashes onnxruntime-genai's runtime even with <c>do_sample=false</c>. The fix (in
/// <see cref="OnnxVisionModel"/>'s private <c>ApplyParameters</c>, and mirrored in
/// <c>OnnxGenAIModel.ApplyParameters</c>) omits the <c>"temperature"</c> search option
/// entirely whenever <see cref="GenerationParameters.Temperature"/> is <c>&lt;= 0</c>,
/// relying on <c>do_sample=false</c> alone to select greedy decoding. This is a
/// systemic fix protecting every model that goes through <c>ApplyParameters</c>.
/// <para>
/// This test targets <see cref="OnnxVisionModel"/> because it is the only one of the two
/// <c>ApplyParameters</c> call sites reachable without a real ONNX model — it is exercised
/// through the <see cref="IVisionGenerationRuntime"/>/<see cref="IVisionSearchOptions"/>
/// test seam already used by <c>OnnxVisionModelTests</c>. <c>OnnxGenAIModel.ApplyParameters</c>
/// (the text-generation counterpart used by <c>LocalChatClient</c>) constructs a native
/// ORT-GenAI <c>GeneratorParams</c> directly from a real <c>Model</c> with no equivalent
/// runtime abstraction, so it cannot be unit-tested without a live model/weights — see
/// the coverage gap note below.
/// </para>
/// </summary>
public class OnnxVisionModelTemperatureTests
{
    [Fact]
    public void ApplyParameters_TemperatureZero_DoesNotSetNativeTemperatureOption()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 16L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string> { [101] = "ok" });

        OnnxVisionModel.GenerateWithImagesCore(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 2048, Temperature: 0f),
            CancellationToken.None,
            runtime);

        Assert.False(runtime.LastSearchOptions!.TemperatureWasSet,
            "Setting the native 'temperature' search option to 0 crashes onnxruntime-genai " +
            "with an integer divide-by-zero; it must be omitted for Temperature <= 0.");
        Assert.False(runtime.LastSearchOptions!.DoSampleValue,
            "do_sample must be false for greedy decoding when Temperature <= 0.");
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(-0.5f)]
    public void ApplyParameters_NegativeTemperature_DoesNotSetNativeTemperatureOption(float temperature)
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 16L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string> { [101] = "ok" });

        OnnxVisionModel.GenerateWithImagesCore(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 2048, Temperature: temperature),
            CancellationToken.None,
            runtime);

        Assert.False(runtime.LastSearchOptions!.TemperatureWasSet);
        Assert.False(runtime.LastSearchOptions!.DoSampleValue);
    }

    [Fact]
    public void ApplyParameters_PositiveTemperature_SetsNativeTemperatureOptionAndDoSample()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 16L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string> { [101] = "ok" });

        OnnxVisionModel.GenerateWithImagesCore(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 2048, Temperature: 0.7f),
            CancellationToken.None,
            runtime);

        Assert.True(runtime.LastSearchOptions!.TemperatureWasSet);
        Assert.Equal(0.7f, runtime.LastSearchOptions!.TemperatureValue);
        Assert.True(runtime.LastSearchOptions!.DoSampleValue);
    }

    // ──────────────────────────────────────────────
    // Coverage gap note (see class doc): OnnxGenAIModel (text-generation) has the
    // identical fix, but ApplyParameters there takes a native ORT-GenAI GeneratorParams
    // constructed from a real Model — there is no runtime-abstraction seam like
    // IVisionGenerationRuntime for it, so it cannot be unit-tested without a live ONNX
    // model/weights. This is an acknowledged integration-test gap, not something this
    // unit test suite can close; the reasoning is identical to OnnxVisionModel's (both
    // omit the native "temperature" option for Temperature <= 0 per matching doc
    // comments in both files), so this suite exercises the logic once via the reachable
    // (vision) seam as a proxy for both call sites.
    // ──────────────────────────────────────────────

    private static string GetVisionFixturePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        var path = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sourceFilePath)!,
            "..",
            "..",
            "ElBruno.LocalLLMs.IntegrationTests",
            "TestFixtures",
            "red-shape.png"));

        Assert.True(File.Exists(path), $"Expected fixture image at '{path}'.");
        return path;
    }

    private sealed class RecordingVisionRuntime : IVisionGenerationRuntime
    {
        private readonly int _promptTokenCount;
        private readonly long[] _inputShape;
        private readonly IReadOnlyList<int> _generatedTokenIds;
        private readonly IReadOnlyDictionary<int, string> _decodedTokens;

        internal RecordingVisionRuntime(
            int promptTokenCount,
            long[] inputShape,
            IReadOnlyList<int> generatedTokenIds,
            IReadOnlyDictionary<int, string> decodedTokens)
        {
            _promptTokenCount = promptTokenCount;
            _inputShape = inputShape;
            _generatedTokenIds = generatedTokenIds;
            _decodedTokens = decodedTokens;
        }

        internal RecordingVisionSearchOptions? LastSearchOptions { get; private set; }

        public IVisionImages LoadImages(string[] imagePaths) => new RecordingVisionImages();

        public IVisionInputs ProcessImages(string prompt, IVisionImages? images) => new RecordingVisionInputs();

        public int CountPromptTokens(string prompt) => _promptTokenCount;

        public IVisionSearchOptions CreateSearchOptions()
        {
            LastSearchOptions = new RecordingVisionSearchOptions();
            return LastSearchOptions;
        }

        public IVisionGenerator CreateGenerator(IVisionSearchOptions searchOptions)
            => new RecordingVisionGenerator(_inputShape, _generatedTokenIds);

        public IVisionGenerator CreateProbeGenerator(int maxLength)
            => new RecordingVisionGenerator(_inputShape, []);

        public IVisionTokenizerStream CreateTokenizerStream()
            => new RecordingVisionTokenizerStream(_decodedTokens);
    }

    private sealed class RecordingVisionImages : IVisionImages
    {
        public void Dispose()
        {
        }
    }

    private sealed class RecordingVisionInputs : IVisionInputs
    {
        public void Dispose()
        {
        }
    }

    private sealed class RecordingVisionSearchOptions : IVisionSearchOptions
    {
        internal bool TemperatureWasSet { get; private set; }

        internal float? TemperatureValue { get; private set; }

        internal bool DoSampleValue { get; private set; }

        public void SetSearchOption(string name, int value)
        {
        }

        public void SetSearchOption(string name, float value)
        {
            if (name == "temperature")
            {
                TemperatureWasSet = true;
                TemperatureValue = value;
            }
        }

        public void SetSearchOption(string name, bool value)
        {
            if (name == "do_sample")
            {
                DoSampleValue = value;
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingVisionGenerator : IVisionGenerator
    {
        private readonly long[] _inputShape;
        private readonly IReadOnlyList<int> _generatedTokenIds;
        private int _generatedCount;

        internal RecordingVisionGenerator(long[] inputShape, IReadOnlyList<int> generatedTokenIds)
        {
            _inputShape = inputShape;
            _generatedTokenIds = generatedTokenIds;
        }

        public void SetInputs(IVisionInputs inputs)
        {
        }

        public bool IsDone() => _generatedCount >= _generatedTokenIds.Count;

        public void GenerateNextToken() => _generatedCount++;

        public int[] GetSequence(int sequenceIndex) => _generatedTokenIds.Take(_generatedCount).ToArray();

        public long[]? GetInputShape(string name) => name == "input_ids" ? _inputShape : null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingVisionTokenizerStream : IVisionTokenizerStream
    {
        private readonly IReadOnlyDictionary<int, string> _decodedTokens;

        internal RecordingVisionTokenizerStream(IReadOnlyDictionary<int, string> decodedTokens)
        {
            _decodedTokens = decodedTokens;
        }

        public string Decode(int tokenId) => _decodedTokens[tokenId];

        public void Dispose()
        {
        }
    }
}
