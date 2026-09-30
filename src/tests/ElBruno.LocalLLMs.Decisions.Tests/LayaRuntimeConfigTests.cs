using ElBruno.LocalLLMs.Decisions.Internal;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class LayaRuntimeConfigTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("laya-config-tests").FullName;

    private string WriteConfig(string json)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ReadsRootLevelLayout()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            {
              "max_len": 512,
              "head_max_len": 192,
              "temperature": [1.6369, 1.2514, 1.9834],
              "temperature_by_options": { "choice:3-5": 1.7602 }
            }
            """));

        Assert.Equal(512, config.MaxLength);
        Assert.Equal(192, config.HeadMaxLength);
        Assert.Equal(1.7602, config.TemperatureFor(0, 3), 4);
        Assert.False(config.TemperaturesWereClamped);
    }

    [Fact]
    public void Load_ReadsNestedLayaLayout()
    {
        // The onnx-community exports nest the same values under a "laya" key.
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            { "model_type": "laya", "laya": { "max_len": 256, "head_max_len": 96 } }
            """));

        Assert.Equal(256, config.MaxLength);
        Assert.Equal(96, config.HeadMaxLength);
    }

    [Fact]
    public void Load_FallsBackToDefaultBudgetsWhenAbsent()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig("{}"));

        Assert.Equal(512, config.MaxLength);
        Assert.Equal(192, config.HeadMaxLength);
    }

    [Fact]
    public void Load_DefaultsToNeutralTemperatureWhenNoneAreFitted()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig("{}"));

        Assert.Equal(1.0, config.TemperatureFor(0, 3));
        Assert.Equal(1.0, config.TemperatureFor(1, 5));
        Assert.Equal(1.0, config.TemperatureFor(2, 2));
    }

    [Theory]
    [InlineData(2, "choice:2")]
    [InlineData(3, "choice:3-5")]
    [InlineData(5, "choice:3-5")]
    [InlineData(6, "choice:6-10")]
    [InlineData(10, "choice:6-10")]
    [InlineData(11, "choice:11+")]
    [InlineData(40, "choice:11+")]
    public void TemperatureFor_SelectsTheBucketMatchingTheOptionCount(int optionCount, string bucket)
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            $$"""
            {
              "temperature": [9.9, 9.9, 9.9],
              "temperature_by_options": { "{{bucket}}": 2.5 }
            }
            """));

        Assert.Equal(2.5, config.TemperatureFor(0, optionCount));
    }

    [Fact]
    public void TemperatureFor_FallsBackToTheTypeTemperatureWhenNoBucketMatches()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            { "temperature": [1.6369, 1.2514, 1.9834], "temperature_by_options": { "choice:2": 1.9 } }
            """));

        Assert.Equal(1.6369, config.TemperatureFor(0, 7), 4);
        Assert.Equal(1.2514, config.TemperatureFor(1, 4), 4);
        Assert.Equal(1.9834, config.TemperatureFor(2, 2), 4);
    }

    [Fact]
    public void Load_ClampsTheInvalidTemperatureShippedByThePublicEnglishCheckpoint()
    {
        // choice:11+ ships as 0.1006, which sharpens the logits roughly tenfold and turns a 24%
        // top probability into a reported 99%.
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            { "temperature_by_options": { "choice:11+": 0.1006 } }
            """));

        Assert.Equal(LayaRuntimeConfig.MinimumTemperature, config.TemperatureFor(0, 12));
        Assert.True(config.TemperaturesWereClamped);
    }

    [Fact]
    public void Load_ClampsTemperaturesAboveTheMaximum()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            { "temperature_by_options": { "choice:2": 99.0 } }
            """));

        Assert.Equal(LayaRuntimeConfig.MaximumTemperature, config.TemperatureFor(0, 2));
        Assert.True(config.TemperaturesWereClamped);
    }

    [Fact]
    public void Load_TreatsNonNumericTemperaturesAsNeutral()
    {
        LayaRuntimeConfig config = LayaRuntimeConfig.Load(WriteConfig(
            """
            { "temperature_by_options": { "choice:2": null } }
            """));

        Assert.Equal(1.0, config.TemperatureFor(0, 2));
        Assert.True(config.TemperaturesWereClamped);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
