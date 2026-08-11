using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Execution;

public class OnnxVisionModelTests
{
    [Fact]
    public void ResolveInputTokenCount_UsesMultimodalInputIdsLength()
    {
        var tokenCount = OnnxVisionModel.ResolveInputTokenCount(
            name => name == "input_ids" ? [1L, 2876L] : null,
            fallbackTokenCount: 65);

        Assert.Equal(2876, tokenCount);
    }

    [Fact]
    public void ResolveInputTokenCount_FallsBackWhenInputIdsAreUnavailable()
    {
        var tokenCount = OnnxVisionModel.ResolveInputTokenCount(
            _ => throw new InvalidOperationException("input_ids unavailable"),
            fallbackTokenCount: 65);

        Assert.Equal(65, tokenCount);
    }

    [Theory]
    [InlineData(4096, 2876, 64, 2940)]
    [InlineData(4096, 4048, 128, 4096)]
    [InlineData(4096, 2876, 0, 2877)]
    public void ResolveMaxLength_UsesMultimodalInputLengthAndClamps(
        int maxLength,
        int inputTokenCount,
        int? maxOutputTokens,
        int expected)
    {
        var resolved = OnnxVisionModel.ResolveMaxLength(maxLength, inputTokenCount, maxOutputTokens);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void GenerateWithImages_UsesMultimodalInputIdsLengthForMaxOutputTokens()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 2876L],
            generatedTokenIds: [101, 202],
            decodedTokens: new Dictionary<int, string>
            {
                [101] = "vision",
                [202] = " ok"
            });

        using var model = new OnnxVisionModel(runtime);

        var result = model.GenerateWithImages(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 4096, MaxOutputTokens: 64, Temperature: 0),
            CancellationToken.None);

        Assert.Equal("vision ok", result.Text);
        Assert.Equal(2876, result.InputTokenCount);
        Assert.Equal(2, result.OutputTokenCount);
        Assert.Equal(int.MaxValue, runtime.ProbeMaxLength);
        Assert.Equal(2940, runtime.GenerationMaxLength);
        Assert.Equal([imagePath], runtime.LoadedImagePaths);
        Assert.True(runtime.GenerationInputsWereSet);
        Assert.True(runtime.ProbeInputsWereSet);
    }

    [Fact]
    public void GenerateWithImages_UsesGuaranteedProbeWhenExpandedInputIdsExceedConfiguredMaxLength()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 2876L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string>
            {
                [101] = "vision"
            },
            failWhenProbeMaxLengthIsTooSmall: true);

        using var model = new OnnxVisionModel(runtime);

        var result = model.GenerateWithImages(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 1024, MaxOutputTokens: 64, Temperature: 0),
            CancellationToken.None);

        Assert.Equal("vision", result.Text);
        Assert.Equal(2876, result.InputTokenCount);
        Assert.Equal(1, result.OutputTokenCount);
        Assert.Equal(int.MaxValue, runtime.ProbeMaxLength);
        Assert.Equal(2877, runtime.GenerationMaxLength);
        Assert.True(runtime.GenerationInputsWereSet);
        Assert.True(runtime.ProbeInputsWereSet);
    }

    [Fact]
    public void GenerateWithImages_ClampsProbeToConfiguredContextLength()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 2876L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string> { [101] = "vision" });

        var result = OnnxVisionModel.GenerateWithImagesCore(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 4096, MaxOutputTokens: 64, Temperature: 0),
            CancellationToken.None,
            runtime,
            probeMaxLength: 32768);

        Assert.Equal("vision", result.Text);
        Assert.Equal(32768, runtime.ProbeMaxLength);
    }

    [Fact]
    public void GenerateWithImages_WhenProbeCreationFails_FallsBackToPromptTokenCount()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 12,
            inputShape: [1L, 2876L],
            generatedTokenIds: [101],
            decodedTokens: new Dictionary<int, string> { [101] = "vision" },
            failProbeCreation: true);

        var result = OnnxVisionModel.GenerateWithImagesCore(
            "Describe the image",
            [imagePath],
            new GenerationParameters(MaxLength: 4096, MaxOutputTokens: 64, Temperature: 0),
            CancellationToken.None,
            runtime);

        Assert.Equal("vision", result.Text);
        Assert.Equal(12, result.InputTokenCount);
        Assert.Equal(76, runtime.GenerationMaxLength);
    }

    [Fact]
    public async Task GenerateWithImagesStreamingAsync_UsesMultimodalInputIdsLengthForMaxOutputTokens()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 9,
            inputShape: [1L, 1536L],
            generatedTokenIds: [11, 22],
            decodedTokens: new Dictionary<int, string>
            {
                [11] = "alpha",
                [22] = " beta"
            });

        using var model = new OnnxVisionModel(runtime);
        var chunks = new List<string>();

        await foreach (var chunk in model.GenerateWithImagesStreamingAsync(
            "What do you see?",
            [imagePath],
            new GenerationParameters(MaxLength: 2048, MaxOutputTokens: 32, Temperature: 0),
            CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["alpha", " beta"], chunks);
        Assert.Equal(int.MaxValue, runtime.ProbeMaxLength);
        Assert.Equal(1568, runtime.GenerationMaxLength);
        Assert.True(runtime.GenerationInputsWereSet);
        Assert.True(runtime.ProbeInputsWereSet);
    }

    [Fact]
    public async Task GenerateWithImagesStreamingAsync_UsesGuaranteedProbeWhenExpandedInputIdsExceedConfiguredMaxLength()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 9,
            inputShape: [1L, 1536L],
            generatedTokenIds: [11],
            decodedTokens: new Dictionary<int, string>
            {
                [11] = "alpha"
            },
            failWhenProbeMaxLengthIsTooSmall: true);

        using var model = new OnnxVisionModel(runtime);
        var chunks = new List<string>();

        await foreach (var chunk in model.GenerateWithImagesStreamingAsync(
            "What do you see?",
            [imagePath],
            new GenerationParameters(MaxLength: 512, MaxOutputTokens: 32, Temperature: 0),
            CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["alpha"], chunks);
        Assert.Equal(int.MaxValue, runtime.ProbeMaxLength);
        Assert.Equal(1537, runtime.GenerationMaxLength);
        Assert.True(runtime.GenerationInputsWereSet);
        Assert.True(runtime.ProbeInputsWereSet);
    }

    [Fact]
    public async Task GenerateWithImagesStreamingAsync_WhenDisposedAfterGettingEnumerable_ThrowsObjectDisposedExceptionOnEnumeration()
    {
        var imagePath = GetVisionFixturePath();
        var runtime = new RecordingVisionRuntime(
            promptTokenCount: 9,
            inputShape: [1L, 1536L],
            generatedTokenIds: [11],
            decodedTokens: new Dictionary<int, string>
            {
                [11] = "alpha"
            });

        using var model = new OnnxVisionModel(runtime);
        var stream = model.GenerateWithImagesStreamingAsync(
            "What do you see?",
            [imagePath],
            new GenerationParameters(MaxLength: 2048, MaxOutputTokens: 32, Temperature: 0),
            CancellationToken.None);

        model.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in stream.ConfigureAwait(false))
            {
            }
        });
    }

    private static string GetVisionFixturePath([CallerFilePath] string sourceFilePath = "")
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
        private readonly bool _failWhenProbeMaxLengthIsTooSmall;
        private readonly bool _failProbeCreation;

        internal RecordingVisionRuntime(
            int promptTokenCount,
            long[] inputShape,
            IReadOnlyList<int> generatedTokenIds,
            IReadOnlyDictionary<int, string> decodedTokens,
            bool failWhenProbeMaxLengthIsTooSmall = false,
            bool failProbeCreation = false)
        {
            _promptTokenCount = promptTokenCount;
            _inputShape = inputShape;
            _generatedTokenIds = generatedTokenIds;
            _decodedTokens = decodedTokens;
            _failWhenProbeMaxLengthIsTooSmall = failWhenProbeMaxLengthIsTooSmall;
            _failProbeCreation = failProbeCreation;
        }

        internal IReadOnlyList<string>? LoadedImagePaths { get; private set; }
        internal int? ProbeMaxLength { get; private set; }
        internal int? GenerationMaxLength { get; private set; }
        internal bool ProbeInputsWereSet { get; private set; }
        internal bool GenerationInputsWereSet { get; private set; }

        public IVisionImages LoadImages(string[] imagePaths)
        {
            LoadedImagePaths = imagePaths;
            return new RecordingVisionImages();
        }

        public IVisionInputs ProcessImages(string prompt, IVisionImages? images)
        {
            Assert.False(string.IsNullOrWhiteSpace(prompt));
            Assert.NotNull(images);
            return new RecordingVisionInputs();
        }

        public int CountPromptTokens(string prompt)
        {
            Assert.False(string.IsNullOrWhiteSpace(prompt));
            return _promptTokenCount;
        }

        public IVisionSearchOptions CreateSearchOptions() => new RecordingVisionSearchOptions();

        public IVisionGenerator CreateGenerator(IVisionSearchOptions searchOptions)
        {
            var options = Assert.IsType<RecordingVisionSearchOptions>(searchOptions);
            GenerationMaxLength = options.MaxLength;
            return new RecordingVisionGenerator(
                inputShape: _inputShape,
                generatedTokenIds: _generatedTokenIds,
                onSetInputs: () => GenerationInputsWereSet = true,
                maxLength: options.MaxLength);
        }

        public IVisionGenerator CreateProbeGenerator(int maxLength)
        {
            ProbeMaxLength = maxLength;
            if (_failProbeCreation)
                throw new InvalidOperationException("probe unavailable");

            return new RecordingVisionGenerator(
                inputShape: _inputShape,
                generatedTokenIds: [],
                onSetInputs: () => ProbeInputsWereSet = true,
                maxLength: maxLength,
                failWhenMaxLengthIsTooSmall: _failWhenProbeMaxLengthIsTooSmall);
        }

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
        internal int? MaxLength { get; private set; }

        public void SetSearchOption(string name, int value)
        {
            if (name == "max_length")
                MaxLength = value;
        }

        public void SetSearchOption(string name, float value)
        {
        }

        public void SetSearchOption(string name, bool value)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingVisionGenerator : IVisionGenerator
    {
        private readonly long[] _inputShape;
        private readonly IReadOnlyList<int> _generatedTokenIds;
        private readonly Action _onSetInputs;
        private readonly int? _maxLength;
        private readonly bool _failWhenMaxLengthIsTooSmall;
        private int _generatedCount;

        internal RecordingVisionGenerator(
            long[] inputShape,
            IReadOnlyList<int> generatedTokenIds,
            Action onSetInputs,
            int? maxLength = null,
            bool failWhenMaxLengthIsTooSmall = false)
        {
            _inputShape = inputShape;
            _generatedTokenIds = generatedTokenIds;
            _onSetInputs = onSetInputs;
            _maxLength = maxLength;
            _failWhenMaxLengthIsTooSmall = failWhenMaxLengthIsTooSmall;
        }

        public void SetInputs(IVisionInputs inputs)
        {
            Assert.IsType<RecordingVisionInputs>(inputs);
            if (_failWhenMaxLengthIsTooSmall && _maxLength.HasValue && _inputShape[^1] > _maxLength.Value)
                throw new InvalidOperationException("probe max length too small");
            _onSetInputs();
        }

        public bool IsDone() => _generatedCount >= _generatedTokenIds.Count;

        public void GenerateNextToken() => _generatedCount++;

        public int[] GetSequence(int sequenceIndex)
        {
            Assert.Equal(0, sequenceIndex);
            return _generatedTokenIds.Take(_generatedCount).ToArray();
        }

        public long[]? GetInputShape(string name)
            => name == "input_ids" ? _inputShape : null;

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
