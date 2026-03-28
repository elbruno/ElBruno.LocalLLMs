using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests.Exceptions;

/// <summary>
/// Tests for the custom exception hierarchy: LocalLLMException,
/// ExecutionProviderException, ModelCapacityExceededException, ModelNotAvailableException.
/// </summary>
public class ExceptionTests
{
    // ──────────────────────────────────────────────
    // Inheritance
    // ──────────────────────────────────────────────

    [Fact]
    public void ExecutionProviderException_InheritsFromLocalLLMException()
    {
        var ex = new ExecutionProviderException("fail", ExecutionProvider.Cuda);

        Assert.IsAssignableFrom<LocalLLMException>(ex);
    }

    [Fact]
    public void ModelCapacityExceededException_InheritsFromLocalLLMException()
    {
        var ex = new ModelCapacityExceededException("too big", 5000, 2048);

        Assert.IsAssignableFrom<LocalLLMException>(ex);
    }

    [Fact]
    public void ModelNotAvailableException_InheritsFromLocalLLMException()
    {
        var ex = new ModelNotAvailableException("missing");

        Assert.IsAssignableFrom<LocalLLMException>(ex);
    }

    [Fact]
    public void AllCustomExceptions_InheritFromSystemException()
    {
        Assert.IsAssignableFrom<Exception>(new ExecutionProviderException("a", ExecutionProvider.Cpu));
        Assert.IsAssignableFrom<Exception>(new ModelCapacityExceededException("b", 1, 2));
        Assert.IsAssignableFrom<Exception>(new ModelNotAvailableException("c"));
    }

    // ──────────────────────────────────────────────
    // ExecutionProviderException — properties
    // ──────────────────────────────────────────────

    [Fact]
    public void ExecutionProviderException_StoresProvider()
    {
        var ex = new ExecutionProviderException("CUDA failed", ExecutionProvider.Cuda);

        Assert.Equal(ExecutionProvider.Cuda, ex.Provider);
    }

    [Fact]
    public void ExecutionProviderException_StoresMessage()
    {
        var ex = new ExecutionProviderException("CUDA failed", ExecutionProvider.Cuda);

        Assert.Equal("CUDA failed", ex.Message);
    }

    [Fact]
    public void ExecutionProviderException_WithInnerException_StoresInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new ExecutionProviderException("wrap", ExecutionProvider.DirectML, inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ExecutionProviderException_WithoutInnerException_CreatesDefaultInner()
    {
        var ex = new ExecutionProviderException("wrap", ExecutionProvider.DirectML);

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void ExecutionProviderException_WithSuggestion_StoresSuggestion()
    {
        var ex = new ExecutionProviderException(
            "fail", ExecutionProvider.Cuda, "Install CUDA toolkit");

        Assert.Equal("Install CUDA toolkit", ex.Suggestion);
    }

    [Fact]
    public void ExecutionProviderException_WithSuggestionAndInner_StoresBoth()
    {
        var inner = new DllNotFoundException("cuda.dll");
        var ex = new ExecutionProviderException(
            "fail", ExecutionProvider.Cuda, "Install CUDA", inner);

        Assert.Equal("Install CUDA", ex.Suggestion);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(ExecutionProvider.Cuda, ex.Provider);
    }

    [Fact]
    public void ExecutionProviderException_NullSuggestion_IsAllowed()
    {
        var ex = new ExecutionProviderException(
            "fail", ExecutionProvider.Cpu, suggestion: null);

        Assert.Null(ex.Suggestion);
    }

    // ──────────────────────────────────────────────
    // Context dictionary (from LocalLLMException)
    // ──────────────────────────────────────────────

    [Fact]
    public void ExecutionProviderException_ContextDictionary_IsAvailable()
    {
        var ex = new ExecutionProviderException("fail", ExecutionProvider.Cuda);

        Assert.NotNull(ex.Context);
        Assert.Empty(ex.Context);
    }

    [Fact]
    public void ExecutionProviderException_ContextDictionary_CanStoreValues()
    {
        var ex = new ExecutionProviderException("fail", ExecutionProvider.Cuda);
        ex.Context["model_path"] = @"C:\models\phi3";
        ex.Context["retry_count"] = 3;

        Assert.Equal(@"C:\models\phi3", ex.Context["model_path"]);
        Assert.Equal(3, ex.Context["retry_count"]);
    }

    [Fact]
    public void ModelCapacityExceededException_ContextDictionary_IsAvailable()
    {
        var ex = new ModelCapacityExceededException("too big", 5000, 2048);

        Assert.NotNull(ex.Context);
    }

    [Fact]
    public void ModelNotAvailableException_ContextDictionary_IsAvailable()
    {
        var ex = new ModelNotAvailableException("missing");

        Assert.NotNull(ex.Context);
    }

    // ──────────────────────────────────────────────
    // ModelCapacityExceededException — properties
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelCapacityExceededException_StoresInputTokenCount()
    {
        var ex = new ModelCapacityExceededException("overflow", 5000, 2048);

        Assert.Equal(5000, ex.InputTokenCount);
    }

    [Fact]
    public void ModelCapacityExceededException_StoresMaxTokens()
    {
        var ex = new ModelCapacityExceededException("overflow", 5000, 2048);

        Assert.Equal(2048, ex.MaxTokens);
    }

    [Fact]
    public void ModelCapacityExceededException_StoresMessage()
    {
        var ex = new ModelCapacityExceededException("Input too long", 5000, 2048);

        Assert.Equal("Input too long", ex.Message);
    }

    [Fact]
    public void ModelCapacityExceededException_WithInnerException_StoresIt()
    {
        var inner = new ArgumentException("bad input");
        var ex = new ModelCapacityExceededException("overflow", 5000, 2048, inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ModelCapacityExceededException_WithoutInnerException_CreatesDefaultInner()
    {
        var ex = new ModelCapacityExceededException("overflow", 5000, 2048);

        Assert.NotNull(ex.InnerException);
    }

    // ──────────────────────────────────────────────
    // ModelNotAvailableException — properties
    // ──────────────────────────────────────────────

    [Fact]
    public void ModelNotAvailableException_StoresModelPath()
    {
        var ex = new ModelNotAvailableException("not found", @"C:\models\missing");

        Assert.Equal(@"C:\models\missing", ex.ModelPath);
    }

    [Fact]
    public void ModelNotAvailableException_NullModelPath_IsAllowed()
    {
        var ex = new ModelNotAvailableException("not found");

        Assert.Null(ex.ModelPath);
    }

    [Fact]
    public void ModelNotAvailableException_StoresMessage()
    {
        var ex = new ModelNotAvailableException("Model file missing");

        Assert.Equal("Model file missing", ex.Message);
    }

    [Fact]
    public void ModelNotAvailableException_WithInnerException_StoresIt()
    {
        var inner = new FileNotFoundException("genai_config.json");
        var ex = new ModelNotAvailableException("missing", @"C:\m", inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ModelNotAvailableException_WithoutInnerException_CreatesDefaultInner()
    {
        var ex = new ModelNotAvailableException("missing");

        Assert.NotNull(ex.InnerException);
    }
}
