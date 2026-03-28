using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Execution;

/// <summary>
/// Tests for the 3-argument <c>ShouldFallbackToNextProvider</c> overload
/// introduced for Issue #7 — Auto mode permits generic provider errors.
/// </summary>
public class OnnxGenAIModelFallbackTests
{
    // ──────────────────────────────────────────────
    // Auto mode — generic "specified provider" messages
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("Specified provider is not supported.")]
    [InlineData("Specified provider is not supported")]
    public void ThreeArg_Auto_SpecifiedProviderNotSupported_ReturnsTrue(string message)
    {
        var ex = new InvalidOperationException(message);

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.DirectML, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_ExplicitDirectML_SpecifiedProviderNotSupported_ReturnsFalse()
    {
        // When initialProvider is NOT Auto, generic "specified provider" requires
        // provider context AND a fallback keyword. "Specified provider is not supported."
        // has no dml/directml token, so strict path returns false.
        var ex = new InvalidOperationException("Specified provider is not supported.");

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.DirectML, ex, ExecutionProvider.DirectML));
    }

    // ──────────────────────────────────────────────
    // Auto mode — generic "not available" / "is unavailable"
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_Auto_GenericNotAvailable_ReturnsTrue()
    {
        var ex = new InvalidOperationException("The requested provider is not available on this machine.");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_Auto_GenericIsUnavailable_ReturnsTrue()
    {
        var ex = new InvalidOperationException("The provider is unavailable.");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_Auto_GenericIsNotSupported_ReturnsTrue()
    {
        var ex = new InvalidOperationException("Execution provider is not supported on this platform.");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.DirectML, ex, ExecutionProvider.Auto));
    }

    // ──────────────────────────────────────────────
    // Provider-specific errors — Auto vs explicit
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_Auto_DmlError_ReturnsTrue()
    {
        var ex = new InvalidOperationException("DML provider not found");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.DirectML, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_ExplicitDirectML_DmlError_ReturnsTrue()
    {
        // Provider-specific token + fallback keyword → true regardless of initialProvider
        var ex = new InvalidOperationException("DML provider not found");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.DirectML, ex, ExecutionProvider.DirectML));
    }

    [Fact]
    public void ThreeArg_Auto_CudaError_ReturnsTrue()
    {
        var ex = new InvalidOperationException("CUDA execution provider could not be created");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_ExplicitCuda_CudaError_ReturnsTrue()
    {
        var ex = new InvalidOperationException("CUDA execution provider could not be created");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Cuda));
    }

    // ──────────────────────────────────────────────
    // Null exception — ArgumentNullException
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => OnnxGenAIModel.ShouldFallbackToNextProvider(
                ExecutionProvider.Cuda, null!, ExecutionProvider.Auto));
    }

    // ──────────────────────────────────────────────
    // Empty / whitespace message
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_UnrelatedErrorMessage_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Model folder is missing genai_config.json.");

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }

    // ──────────────────────────────────────────────
    // CPU provider — no special fallback in Auto fast-path,
    // still uses strict path
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_CpuProvider_WithCpuTokenAndKeyword_ReturnsTrue()
    {
        var ex = new InvalidOperationException("cpu provider is unavailable");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cpu, ex, ExecutionProvider.Auto));
    }

    [Fact]
    public void ThreeArg_CpuProvider_WithoutCpuContext_ReturnsFalse_InStrictMode()
    {
        // No "cpu" token → strict path returns false even with fallback keyword
        var ex = new InvalidOperationException("provider is unavailable");

        // With Cpu as initialProvider (non-Auto), need provider context
        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cpu, ex, ExecutionProvider.Cpu));
    }

    // ──────────────────────────────────────────────
    // Backward-compat: 2-arg still works
    // ──────────────────────────────────────────────

    [Fact]
    public void TwoArg_CudaFallbackMessage_StillWorks()
    {
        var ex = new InvalidOperationException("CUDA library not found on this system");

        Assert.True(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, ex));
    }

    [Fact]
    public void TwoArg_NullException_StillThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.Cuda, null!));
    }

    [Fact]
    public void TwoArg_GenericProviderMessage_ReturnsFalse()
    {
        // 2-arg sets initialProvider = provider (non-Auto), so generic message without
        // provider context returns false.
        var ex = new InvalidOperationException("Specified provider is not supported.");

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(ExecutionProvider.DirectML, ex));
    }

    // ──────────────────────────────────────────────
    // Auto fast-path does NOT trigger for unrelated messages
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeArg_Auto_UnrelatedMessage_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Out of memory allocating tensor");

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }

    [Theory]
    [InlineData("CUDA out of memory")]
    [InlineData("DirectML device lost during execution")]
    public void ThreeArg_Auto_ProviderPresentButNoFallbackKeyword_UsesAutoFastPath(string message)
    {
        // These contain provider tokens but no auto fast-path keywords
        // ("not available", "is unavailable", "is not supported", "specified provider")
        // and no strict-path fallback keywords. So they should return false.
        var ex = new InvalidOperationException(message);

        Assert.False(OnnxGenAIModel.ShouldFallbackToNextProvider(
            ExecutionProvider.Cuda, ex, ExecutionProvider.Auto));
    }
}
