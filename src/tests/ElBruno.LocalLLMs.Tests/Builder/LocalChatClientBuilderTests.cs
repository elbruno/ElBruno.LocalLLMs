using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Builder;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests.Builder;

/// <summary>
/// Tests for <see cref="LocalChatClientBuilder"/> — verifies fluent configuration
/// without requiring the ONNX runtime (BuildAsync is not called).
/// </summary>
public class LocalChatClientBuilderTests
{
    // ──────────────────────────────────────────────
    // Fluent chaining — each method returns the same builder
    // ──────────────────────────────────────────────

    [Fact]
    public void WithModel_String_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithModel("tinyllama-1.1b-chat");

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithModel_ModelDefinition_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithModel(KnownModels.Phi35MiniInstruct);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithModelPath_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithModelPath(@"C:\models\phi3");

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithExecutionProvider_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithExecutionProvider(ExecutionProvider.Cuda);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithGpuDeviceId_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithGpuDeviceId(1);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithMaxSequenceLength_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithMaxSequenceLength(4096);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTemperature_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithTemperature(0.5f);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTopP_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithTopP(0.95f);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithCacheDirectory_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithCacheDirectory(@"C:\cache");

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithSystemPrompt_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.WithSystemPrompt("You are a helpful assistant.");

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithLogger_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();
        var loggerFactory = Substitute.For<ILoggerFactory>();

        var result = builder.WithLogger(loggerFactory);

        Assert.Same(builder, result);
    }

    [Fact]
    public void EnsureModelDownloaded_ReturnsSameBuilder()
    {
        var builder = new LocalChatClientBuilder();

        var result = builder.EnsureModelDownloaded(false);

        Assert.Same(builder, result);
    }

    // ──────────────────────────────────────────────
    // Full fluent chain
    // ──────────────────────────────────────────────

    [Fact]
    public void FullFluentChain_DoesNotThrow()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();

        var exception = Record.Exception(() =>
            new LocalChatClientBuilder()
                .WithModel(KnownModels.Phi35MiniInstruct)
                .WithExecutionProvider(ExecutionProvider.Cpu)
                .WithMaxSequenceLength(2048)
                .WithTemperature(0.7f)
                .WithTopP(0.9f)
                .WithCacheDirectory(@"C:\cache")
                .WithSystemPrompt("test")
                .WithLogger(loggerFactory)
                .EnsureModelDownloaded(false));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────────

    [Fact]
    public void WithModel_UnknownModelId_ThrowsArgumentException()
    {
        var builder = new LocalChatClientBuilder();

        Assert.Throws<ArgumentException>(
            () => builder.WithModel("nonexistent-model-xyz"));
    }

    [Fact]
    public void WithModel_NullModelDefinition_ThrowsArgumentNullException()
    {
        var builder = new LocalChatClientBuilder();

        Assert.Throws<ArgumentNullException>(
            () => builder.WithModel((ModelDefinition)null!));
    }
}
