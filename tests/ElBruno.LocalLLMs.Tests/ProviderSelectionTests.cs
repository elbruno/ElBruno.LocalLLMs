using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests;

public class ProviderSelectionTests
{
    [Fact]
    public void AutoProvider_UsesGpuFirstThenCpuFallbackOrder()
    {
        var order = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);

        Assert.Equal(3, order.Count);
        Assert.Equal(ExecutionProvider.Cuda, order[0]);
        Assert.Equal(ExecutionProvider.DirectML, order[1]);
        Assert.Equal(ExecutionProvider.Cpu, order[2]);
    }

    [Fact]
    public void AutoProvider_FallbackOrder_IsDeterministicAcrossCalls()
    {
        var first = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);
        var second = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);

        Assert.Equal(first, second);
        Assert.Equal([ExecutionProvider.Cuda, ExecutionProvider.DirectML, ExecutionProvider.Cpu], first);
    }

    [Theory]
    [InlineData(ExecutionProvider.Cpu)]
    [InlineData(ExecutionProvider.Cuda)]
    [InlineData(ExecutionProvider.DirectML)]
    public void ExplicitProvider_UsesOnlyRequestedProvider(ExecutionProvider provider)
    {
        var order = OnnxGenAIModel.GetProviderFallbackOrder(provider);

        Assert.Single(order);
        Assert.Equal(provider, order[0]);
    }

    [Fact]
    public void ShouldFallbackToNextProvider_ReturnsTrue_ForUnavailableCudaProvider()
    {
        var ex = new InvalidOperationException("Failed to load CUDA provider library: onnxruntime_providers_cuda.dll not found.");

        var shouldFallback = OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex);

        Assert.True(shouldFallback);
    }

    [Fact]
    public void ShouldFallbackToNextProvider_ReturnsTrue_ForUnavailableDirectMLProvider()
    {
        var ex = new InvalidOperationException("DirectML provider is unavailable on this machine.");

        var shouldFallback = OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex);

        Assert.True(shouldFallback);
    }

    [Fact]
    public void ShouldFallbackToNextProvider_ReturnsFalse_ForNonProviderErrors()
    {
        var ex = new InvalidOperationException("Model folder is missing genai_config.json.");

        var shouldFallback = OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex);

        Assert.False(shouldFallback);
    }
}
