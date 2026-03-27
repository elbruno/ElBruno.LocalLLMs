using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests;

/// <summary>
/// Tests for <see cref="LocalLLMsOptions"/> — defaults, custom values, and validation.
/// </summary>
public class LocalLLMsOptionsTests
{
    // ──────────────────────────────────────────────
    // Default values
    // ──────────────────────────────────────────────

    [Fact]
    public void Defaults_Model_IsPhi35MiniInstruct()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(KnownModels.Phi35MiniInstruct, options.Model);
    }

    [Fact]
    public void Defaults_ModelPath_IsNull()
    {
        var options = new LocalLLMsOptions();

        Assert.Null(options.ModelPath);
    }

    [Fact]
    public void Defaults_CacheDirectory_IsNull()
    {
        var options = new LocalLLMsOptions();

        Assert.Null(options.CacheDirectory);
    }

    [Fact]
    public void Defaults_EnsureModelDownloaded_IsTrue()
    {
        var options = new LocalLLMsOptions();

        Assert.True(options.EnsureModelDownloaded);
    }

    [Fact]
    public void Defaults_ExecutionProvider_IsAuto()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(ExecutionProvider.Auto, options.ExecutionProvider);
    }

    [Fact]
    public void Defaults_GpuDeviceId_IsZero()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(0, options.GpuDeviceId);
    }

    [Fact]
    public void Defaults_MaxSequenceLength_Is2048()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(2048, options.MaxSequenceLength);
    }

    [Fact]
    public void Defaults_Temperature_Is07()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(0.7f, options.Temperature);
    }

    [Fact]
    public void Defaults_TopP_Is09()
    {
        var options = new LocalLLMsOptions();

        Assert.Equal(0.9f, options.TopP);
    }

    // ──────────────────────────────────────────────
    // Custom values
    // ──────────────────────────────────────────────

    [Fact]
    public void Custom_Model_CanBeSet()
    {
        var options = new LocalLLMsOptions
        {
            Model = KnownModels.Phi4
        };

        Assert.Equal(KnownModels.Phi4, options.Model);
    }

    [Fact]
    public void Custom_ModelPath_CanBeSet()
    {
        var options = new LocalLLMsOptions
        {
            ModelPath = @"C:\models\phi3"
        };

        Assert.Equal(@"C:\models\phi3", options.ModelPath);
    }

    [Fact]
    public void Custom_CacheDirectory_CanBeSet()
    {
        var options = new LocalLLMsOptions
        {
            CacheDirectory = @"C:\cache\models"
        };

        Assert.Equal(@"C:\cache\models", options.CacheDirectory);
    }

    [Fact]
    public void Custom_EnsureModelDownloaded_CanBeDisabled()
    {
        var options = new LocalLLMsOptions
        {
            EnsureModelDownloaded = false
        };

        Assert.False(options.EnsureModelDownloaded);
    }

    [Theory]
    [InlineData(ExecutionProvider.Cpu)]
    [InlineData(ExecutionProvider.Cuda)]
    [InlineData(ExecutionProvider.DirectML)]
    public void Custom_ExecutionProvider_AllValuesAccepted(ExecutionProvider provider)
    {
        var options = new LocalLLMsOptions
        {
            ExecutionProvider = provider
        };

        Assert.Equal(provider, options.ExecutionProvider);
    }

    [Fact]
    public void Custom_GpuDeviceId_CanBeSet()
    {
        var options = new LocalLLMsOptions
        {
            GpuDeviceId = 3
        };

        Assert.Equal(3, options.GpuDeviceId);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(4096)]
    [InlineData(8192)]
    public void Custom_MaxSequenceLength_CanBeSet(int length)
    {
        var options = new LocalLLMsOptions
        {
            MaxSequenceLength = length
        };

        Assert.Equal(length, options.MaxSequenceLength);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    public void Custom_Temperature_CanBeSet(float temp)
    {
        var options = new LocalLLMsOptions
        {
            Temperature = temp
        };

        Assert.Equal(temp, options.Temperature);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Custom_TopP_CanBeSet(float topP)
    {
        var options = new LocalLLMsOptions
        {
            TopP = topP
        };

        Assert.Equal(topP, options.TopP);
    }

    // ──────────────────────────────────────────────
    // Mutation: options are mutable, verify round-trip
    // ──────────────────────────────────────────────

    [Fact]
    public void Options_AreFullyMutable()
    {
        var options = new LocalLLMsOptions();

        options.Model = KnownModels.Phi4;
        options.ModelPath = @"C:\test";
        options.CacheDirectory = @"C:\cache";
        options.EnsureModelDownloaded = false;
        options.ExecutionProvider = ExecutionProvider.Cuda;
        options.GpuDeviceId = 1;
        options.MaxSequenceLength = 4096;
        options.Temperature = 0.5f;
        options.TopP = 0.8f;

        Assert.Equal(KnownModels.Phi4, options.Model);
        Assert.Equal(@"C:\test", options.ModelPath);
        Assert.Equal(@"C:\cache", options.CacheDirectory);
        Assert.False(options.EnsureModelDownloaded);
        Assert.Equal(ExecutionProvider.Cuda, options.ExecutionProvider);
        Assert.Equal(1, options.GpuDeviceId);
        Assert.Equal(4096, options.MaxSequenceLength);
        Assert.Equal(0.5f, options.Temperature);
        Assert.Equal(0.8f, options.TopP);
    }
}
