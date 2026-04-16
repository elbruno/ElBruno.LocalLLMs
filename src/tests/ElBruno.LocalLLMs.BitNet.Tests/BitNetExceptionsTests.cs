using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetNativeLibraryException"/> and <see cref="BitNetInferenceException"/>.
/// </summary>
public class BitNetExceptionsTests
{
    // ──────────────────────────────────────────────
    // BitNetNativeLibraryException
    // ──────────────────────────────────────────────

    [Fact]
    public void NativeLibraryException_StoresMessage()
    {
        var ex = new BitNetNativeLibraryException("libllama.so not found");

        Assert.Equal("libllama.so not found", ex.Message);
    }

    [Fact]
    public void NativeLibraryException_InheritsFromException()
    {
        var ex = new BitNetNativeLibraryException("fail");

        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void NativeLibraryException_IsSealed()
    {
        Assert.True(typeof(BitNetNativeLibraryException).IsSealed);
    }

    [Fact]
    public void NativeLibraryException_InnerException_IsNull()
    {
        var ex = new BitNetNativeLibraryException("msg");

        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void NativeLibraryException_EmptyMessage_IsPreserved()
    {
        var ex = new BitNetNativeLibraryException("");

        Assert.Equal("", ex.Message);
    }

    // ──────────────────────────────────────────────
    // BitNetInferenceException
    // ──────────────────────────────────────────────

    [Fact]
    public void InferenceException_StoresMessage()
    {
        var ex = new BitNetInferenceException("Tokenization failed");

        Assert.Equal("Tokenization failed", ex.Message);
    }

    [Fact]
    public void InferenceException_InheritsFromException()
    {
        var ex = new BitNetInferenceException("fail");

        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void InferenceException_IsSealed()
    {
        Assert.True(typeof(BitNetInferenceException).IsSealed);
    }

    [Fact]
    public void InferenceException_InnerException_IsNull()
    {
        var ex = new BitNetInferenceException("msg");

        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void InferenceException_EmptyMessage_IsPreserved()
    {
        var ex = new BitNetInferenceException("");

        Assert.Equal("", ex.Message);
    }

    // ──────────────────────────────────────────────
    // Both exceptions are distinct types
    // ──────────────────────────────────────────────

    [Fact]
    public void NativeLibraryException_IsNotInferenceException()
    {
        var ex = new BitNetNativeLibraryException("native fail");

        Assert.IsNotType<BitNetInferenceException>(ex);
    }

    [Fact]
    public void InferenceException_IsNotNativeLibraryException()
    {
        var ex = new BitNetInferenceException("inference fail");

        Assert.IsNotType<BitNetNativeLibraryException>(ex);
    }
}
