using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Execution;

/// <summary>
/// Regression coverage for the ORT-GenAI native divide-by-zero fix on the text-generation path:
/// passing <c>temperature = 0</c> (or any non-positive value) to onnxruntime-genai's native
/// runtime crashes it with an integer divide-by-zero, even when <c>do_sample=false</c> is also
/// set. <c>OnnxGenAIModel.ApplyParameters</c> guards against this by omitting the native
/// <c>"temperature"</c> search option entirely whenever
/// <see cref="GenerationParameters.Temperature"/> is <c>&lt;= 0</c>, relying on
/// <c>do_sample=false</c> alone to select greedy decoding.
/// <para>
/// Until now this guard was only exercised on the vision call site (via
/// <c>OnnxVisionModelTemperatureTests</c>) because <c>OnnxGenAIModel.ApplyParameters</c> took a
/// native ORT-GenAI <c>GeneratorParams</c> built from a real <see cref="Microsoft.ML.OnnxRuntimeGenAI.Model"/>
/// directly, with no seam to fake. <see cref="IGenerationSearchOptions"/> — mirroring
/// <c>IVisionSearchOptions</c> on the vision side — closes that gap by abstracting the
/// <c>SetSearchOption</c> calls, so this suite can pin the guard on the text path directly:
/// the path every chat model flows through.
/// </para>
/// </summary>
public class OnnxGenAIModelTemperatureTests
{
    [Fact]
    public void ApplyParameters_TemperatureZero_DoesNotSetNativeTemperatureOption()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, Temperature: 0f),
            inputTokenCount: 12);

        Assert.False(options.TemperatureWasSet,
            "Setting the native 'temperature' search option to 0 crashes onnxruntime-genai " +
            "with an integer divide-by-zero; it must be omitted for Temperature <= 0.");
        Assert.False(options.DoSampleValue,
            "do_sample must be false for greedy decoding when Temperature <= 0.");
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(-0.5f)]
    public void ApplyParameters_NegativeTemperature_DoesNotSetNativeTemperatureOption(float temperature)
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, Temperature: temperature),
            inputTokenCount: 12);

        Assert.False(options.TemperatureWasSet);
        Assert.False(options.DoSampleValue);
    }

    [Fact]
    public void ApplyParameters_PositiveTemperature_SetsNativeTemperatureOptionAndDoSample()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, Temperature: 0.7f),
            inputTokenCount: 12);

        Assert.True(options.TemperatureWasSet);
        Assert.Equal(0.7f, options.TemperatureValue);
        Assert.True(options.DoSampleValue);
    }

    [Fact]
    public void ApplyParameters_UsesMaxOutputTokensToComputeEffectiveMaxLength()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 4096, MaxOutputTokens: 64, Temperature: 0f),
            inputTokenCount: 2876);

        Assert.Equal(2940, options.MaxLengthValue);
    }

    [Fact]
    public void ApplyParameters_ClampsMaxLengthToConfiguredContextLengthWhenMaxOutputTokensWouldExceedIt()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 4096, MaxOutputTokens: 128, Temperature: 0f),
            inputTokenCount: 4048);

        Assert.Equal(4096, options.MaxLengthValue);
    }

    [Fact]
    public void ApplyParameters_WithoutMaxOutputTokens_UsesMaxLengthAsIs()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, Temperature: 0f),
            inputTokenCount: 12);

        Assert.Equal(2048, options.MaxLengthValue);
    }

    [Fact]
    public void ApplyParameters_SetsTopP()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, TopP: 0.85f, Temperature: 0f),
            inputTokenCount: 12);

        Assert.Equal(0.85f, options.TopPValue);
    }

    [Fact]
    public void ApplyParameters_TopKNull_DoesNotSetNativeTopKOption()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, TopK: null, Temperature: 0f),
            inputTokenCount: 12);

        Assert.False(options.TopKWasSet);
    }

    [Fact]
    public void ApplyParameters_TopKSpecified_SetsNativeTopKOption()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, TopK: 40, Temperature: 0f),
            inputTokenCount: 12);

        Assert.True(options.TopKWasSet);
        Assert.Equal(40, options.TopKValue);
    }

    [Fact]
    public void ApplyParameters_RepetitionPenaltyDefault_DoesNotSetNativeRepetitionPenaltyOption()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, RepetitionPenalty: 1.0f, Temperature: 0f),
            inputTokenCount: 12);

        Assert.False(options.RepetitionPenaltyWasSet);
    }

    [Fact]
    public void ApplyParameters_RepetitionPenaltyNonDefault_SetsNativeRepetitionPenaltyOption()
    {
        var options = new RecordingGenerationSearchOptions();

        OnnxGenAIModel.ApplyParameters(
            options,
            new GenerationParameters(MaxLength: 2048, RepetitionPenalty: 1.2f, Temperature: 0f),
            inputTokenCount: 12);

        Assert.True(options.RepetitionPenaltyWasSet);
        Assert.Equal(1.2f, options.RepetitionPenaltyValue);
    }

    private sealed class RecordingGenerationSearchOptions : IGenerationSearchOptions
    {
        internal int? MaxLengthValue { get; private set; }

        internal bool TemperatureWasSet { get; private set; }
        internal float? TemperatureValue { get; private set; }

        internal float? TopPValue { get; private set; }

        internal bool TopKWasSet { get; private set; }
        internal int? TopKValue { get; private set; }

        internal bool RepetitionPenaltyWasSet { get; private set; }
        internal float? RepetitionPenaltyValue { get; private set; }

        internal bool DoSampleValue { get; private set; }

        public void SetSearchOption(string name, int value)
        {
            if (name == "max_length")
            {
                MaxLengthValue = value;
            }
            else if (name == "top_k")
            {
                TopKWasSet = true;
                TopKValue = value;
            }
        }

        public void SetSearchOption(string name, float value)
        {
            if (name == "temperature")
            {
                TemperatureWasSet = true;
                TemperatureValue = value;
            }
            else if (name == "top_p")
            {
                TopPValue = value;
            }
            else if (name == "repetition_penalty")
            {
                RepetitionPenaltyWasSet = true;
                RepetitionPenaltyValue = value;
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
}
