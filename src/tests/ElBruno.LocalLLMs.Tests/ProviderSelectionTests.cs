using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests;

public class ProviderSelectionTests
{
    // ──────────────────────────────────────────────
    // GetProviderFallbackOrder
    // ──────────────────────────────────────────────

    [Fact]
    public void AutoProvider_UsesGpuFirstThenCpuFallbackOrder()
    {
        var order = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(3, order.Count);
            Assert.Equal(ExecutionProvider.DirectML, order[0]);
            Assert.Equal(ExecutionProvider.Cuda, order[1]);
            Assert.Equal(ExecutionProvider.Cpu, order[2]);
        }
        else
        {
            Assert.Equal(2, order.Count);
            Assert.Equal(ExecutionProvider.Cuda, order[0]);
            Assert.Equal(ExecutionProvider.Cpu, order[1]);
        }
    }

    [Fact]
    public void AutoProvider_FallbackOrder_IsDeterministicAcrossCalls()
    {
        var first = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);
        var second = OnnxGenAIModel.GetProviderFallbackOrder(ExecutionProvider.Auto);

        Assert.Equal(first, second);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal([ExecutionProvider.DirectML, ExecutionProvider.Cuda, ExecutionProvider.Cpu], first);
        }
        else
        {
            Assert.Equal([ExecutionProvider.Cuda, ExecutionProvider.Cpu], first);
        }
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

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — original tests
    // ──────────────────────────────────────────────

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

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — CUDA message patterns
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("Failed to load CUDA provider")]
    [InlineData("CUDA library not found on this system")]
    [InlineData("CUDA execution is not supported")]
    [InlineData("CUDA provider is unavailable")]
    [InlineData("CUDA is unavailable on this machine")]
    [InlineData("CUDA provider is not enabled in this build")]
    [InlineData("Runtime has not been built with CUDA support")]
    [InlineData("CUDA execution provider could not be created")]
    public void ShouldFallback_Cuda_ReturnsTrue_ForEachFallbackPattern(string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — DirectML message patterns
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("Failed to load DML provider library")]
    [InlineData("DML provider not found")]
    [InlineData("DML execution not supported on this hardware")]
    [InlineData("DML provider is unavailable")]
    [InlineData("DML is unavailable on this machine")]
    [InlineData("DML provider is not enabled")]
    [InlineData("Runtime has not been built with DML support")]
    [InlineData("DML execution provider could not be created")]
    public void ShouldFallback_DirectML_WithDmlToken_ReturnsTrue_ForEachFallbackPattern(string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex));
    }

    [Theory]
    [InlineData("Failed to load DirectML provider library")]
    [InlineData("DirectML provider not found")]
    [InlineData("DirectML execution not supported")]
    [InlineData("DirectML provider is unavailable")]
    [InlineData("DirectML is unavailable on this machine")]
    [InlineData("DirectML provider is not enabled")]
    [InlineData("Runtime has not been built with DirectML support")]
    [InlineData("DirectML execution provider could not be created")]
    public void ShouldFallback_DirectML_WithDirectmlToken_ReturnsTrue_ForEachFallbackPattern(string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — missing provider context
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("Failed to load provider library")]
    [InlineData("Provider not found")]
    [InlineData("Execution not supported")]
    [InlineData("Provider is unavailable")]
    [InlineData("Provider is not enabled")]
    [InlineData("Runtime has not been built with support")]
    [InlineData("Execution provider could not be created")]
    public void ShouldFallback_ReturnsFalse_WhenMessageLacksProviderContext(string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex));
        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — case sensitivity
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ExecutionProvider.Cuda, "FAILED TO LOAD CUDA PROVIDER")]
    [InlineData(ExecutionProvider.Cuda, "Failed To Load Cuda Provider")]
    [InlineData(ExecutionProvider.Cuda, "failed to load cuda provider")]
    [InlineData(ExecutionProvider.DirectML, "FAILED TO LOAD DML PROVIDER")]
    [InlineData(ExecutionProvider.DirectML, "Failed To Load DirectML Provider")]
    [InlineData(ExecutionProvider.DirectML, "failed to load directml provider")]
    public void ShouldFallback_ReturnsTrue_RegardlessOfCase(ExecutionProvider provider, string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(provider, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — null exception
    // ──────────────────────────────────────────────

    [Fact]
    public void ShouldFallback_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, null!));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — CPU provider
    // ──────────────────────────────────────────────

    [Fact]
    public void ShouldFallback_CpuProvider_ReturnsTrueWhenMessageContainsCpuAndFallbackKeyword()
    {
        // The method itself doesn't special-case CPU; the constructor guards it.
        // If called directly, CPU is treated like any other provider.
        var ex = new InvalidOperationException("cpu provider is unavailable");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cpu, ex));
    }

    [Fact]
    public void ShouldFallback_CpuProvider_ReturnsFalse_WhenMessageLacksCpuContext()
    {
        var ex = new InvalidOperationException("provider is unavailable");

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cpu, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — message with NO fallback keyword
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ExecutionProvider.Cuda, "CUDA out of memory")]
    [InlineData(ExecutionProvider.Cuda, "CUDA internal error occurred")]
    [InlineData(ExecutionProvider.DirectML, "DML device lost during execution")]
    [InlineData(ExecutionProvider.DirectML, "DirectML encountered a fatal error")]
    public void ShouldFallback_ReturnsFalse_WhenProviderPresentButNoFallbackKeyword(
        ExecutionProvider provider, string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(provider, ex));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — inner exceptions
    // ──────────────────────────────────────────────

    [Fact]
    public void ShouldFallback_ReturnsTrue_WhenInnerExceptionContainsFallbackMessage()
    {
        // ex.ToString() includes inner exception text
        var inner = new DllNotFoundException("cuda library not found");
        var outer = new InvalidOperationException("Model init failed", inner);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, outer));
    }

    [Fact]
    public void ShouldFallback_ReturnsFalse_WhenOnlyOuterHasProviderButNoKeyword()
    {
        var inner = new Exception("something generic");
        var outer = new InvalidOperationException("cuda device error", inner);

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, outer));
    }

    // ──────────────────────────────────────────────
    // ShouldFallbackToNextProvider — mixed provider + keyword
    // ──────────────────────────────────────────────

    [Fact]
    public void ShouldFallback_ReturnsTrue_WhenBothProviderAndKeywordPresent()
    {
        var ex = new InvalidOperationException("cuda provider is unavailable and dml is also not found");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex));
        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex));
    }

    // ──────────────────────────────────────────────
    // BuildProviderFailureReason — formatting
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildProviderFailureReason_FormatsCorrectly()
    {
        var ex = new InvalidOperationException("CUDA provider not found");

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        Assert.Equal("Cuda: InvalidOperationException: CUDA provider not found", reason);
    }

    [Fact]
    public void BuildProviderFailureReason_DirectML_FormatsCorrectly()
    {
        var ex = new DllNotFoundException("DirectML.dll is missing");

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.DirectML, ex);

        Assert.Equal("DirectML: DllNotFoundException: DirectML.dll is missing", reason);
    }

    // ──────────────────────────────────────────────
    // BuildProviderFailureReason — truncation
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildProviderFailureReason_TruncatesLongMessages()
    {
        var longMessage = new string('x', 300);
        var ex = new InvalidOperationException(longMessage);

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        // Format: "Cuda: InvalidOperationException: " + truncated message
        var expectedPrefix = "Cuda: InvalidOperationException: " + longMessage[..180] + "...";
        Assert.Equal(expectedPrefix, reason);
    }

    [Fact]
    public void BuildProviderFailureReason_DoesNotTruncateAt180OrLess()
    {
        var message = new string('y', 180);
        var ex = new InvalidOperationException(message);

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        Assert.Equal($"Cuda: InvalidOperationException: {message}", reason);
        Assert.DoesNotContain("...", reason);
    }

    [Fact]
    public void BuildProviderFailureReason_TruncatesAtExactly181Chars()
    {
        var message = new string('z', 181);
        var ex = new InvalidOperationException(message);

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        Assert.EndsWith("...", reason);
    }

    // ──────────────────────────────────────────────
    // BuildProviderFailureReason — newline replacement
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildProviderFailureReason_ReplacesNewlinesWithSpaces()
    {
        var message = $"Line one{Environment.NewLine}Line two{Environment.NewLine}Line three";
        var ex = new InvalidOperationException(message);

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        Assert.DoesNotContain(Environment.NewLine, reason);
        Assert.Contains("Line one Line two Line three", reason);
    }

    [Fact]
    public void BuildProviderFailureReason_ReplacesNewlinesAndTruncates()
    {
        var lineContent = new string('a', 100);
        var message = $"{lineContent}{Environment.NewLine}{lineContent}{Environment.NewLine}{lineContent}";
        var ex = new InvalidOperationException(message);

        var reason = OnnxGenAIModel.BuildProviderFailureReason(ExecutionProvider.Cuda, ex);

        Assert.DoesNotContain(Environment.NewLine, reason);
        Assert.EndsWith("...", reason);
    }

    // ──────────────────────────────────────────────
    // IsProviderNotInstalledError
    // ──────────────────────────────────────────────

    [Fact]
    public void IsProviderNotInstalledError_ReturnsTrue_ForCudaNotEnabledInThisBuild()
    {
        // This is the exact error reported by the user in the GitHub issue.
        var ex = new InvalidOperationException("CUDA execution provider is not enabled in this build.");

        Assert.True(OnnxGenAIModel.IsProviderNotInstalledError(ExecutionProvider.Cuda, ex));
    }

    [Theory]
    [InlineData(ExecutionProvider.Cuda, "CUDA execution provider is not enabled in this build.")]
    [InlineData(ExecutionProvider.Cuda, "Failed to load CUDA provider library")]
    [InlineData(ExecutionProvider.Cuda, "CUDA library not found")]
    [InlineData(ExecutionProvider.DirectML, "DirectML provider is not enabled in this build.")]
    [InlineData(ExecutionProvider.DirectML, "DML provider not found")]
    public void IsProviderNotInstalledError_ReturnsTrue_ForKnownMissingProviderMessages(
        ExecutionProvider provider, string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.IsProviderNotInstalledError(provider, ex));
    }

    [Fact]
    public void IsProviderNotInstalledError_ReturnsFalse_ForUnrelatedErrors()
    {
        var ex = new InvalidOperationException("Model folder is missing genai_config.json.");

        Assert.False(OnnxGenAIModel.IsProviderNotInstalledError(ExecutionProvider.Cuda, ex));
    }

    [Fact]
    public void IsProviderNotInstalledError_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => OnnxGenAIModel.IsProviderNotInstalledError(ExecutionProvider.Cuda, null!));
    }
}
