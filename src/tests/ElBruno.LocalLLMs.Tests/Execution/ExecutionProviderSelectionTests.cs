using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.LocalLLMs.Tests.Execution;

public class ExecutionProviderSelectionTests
{
    [Fact]
    public void InitializeModel_AutoSkipsUnavailableCudaAndFallsBackToCpu()
    {
        var createdProviders = new List<ExecutionProvider>();

        var result = ExecutionProviderSelection.InitializeModel(
            ExecutionProvider.Auto,
            NullLogger.Instance,
            provider =>
            {
                createdProviders.Add(provider);
                return provider;
            },
            provider => provider switch
            {
                ExecutionProvider.Cpu => ExecutionProviderPreflightResult.Available,
                ExecutionProvider.Cuda => ExecutionProviderPreflightResult.Failure(
                    new DllNotFoundException("CUDA runtime library 'cudart64_13.dll' was not found on the native library search path."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.Cuda)),
                ExecutionProvider.DirectML => ExecutionProviderPreflightResult.Failure(
                    new InvalidOperationException("DirectML provider is unavailable on this machine."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.DirectML)),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
            });

        Assert.Equal(ExecutionProvider.Cpu, result.ActiveProvider);
        Assert.Equal([ExecutionProvider.Cpu], createdProviders);
        Assert.Contains("Cuda", result.ProviderSelectionDetails);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("DirectML", result.ProviderSelectionDetails);
        }
    }

    [Fact]
    public void InitializeModel_ExplicitCudaUnavailable_ThrowsExecutionProviderExceptionWithoutInvokingFactory()
    {
        var factoryCalled = false;

        var ex = Assert.Throws<ExecutionProviderException>(() =>
            ExecutionProviderSelection.InitializeModel(
                ExecutionProvider.Cuda,
                NullLogger.Instance,
                provider =>
                {
                    factoryCalled = true;
                    return provider;
                },
                _ => ExecutionProviderPreflightResult.Failure(
                    new DllNotFoundException("CUDA runtime library 'cublasLt64_13.dll' was not found on the native library search path."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.Cuda))));

        Assert.False(factoryCalled);
        Assert.Equal(ExecutionProvider.Cuda, ex.Provider);
        Assert.Contains("cublasLt64_13.dll", ex.Message);
        Assert.Equal(
            ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.Cuda),
            ex.Suggestion);
    }

    [Fact]
    public void InitializeModel_AutoAttemptsUnknownDirectMLThenFallsBackToCpu()
    {
        var createdProviders = new List<ExecutionProvider>();

        var result = ExecutionProviderSelection.InitializeModel(
            ExecutionProvider.Auto,
            NullLogger.Instance,
            provider =>
            {
                createdProviders.Add(provider);

                if (provider == ExecutionProvider.DirectML)
                {
                    throw new InvalidOperationException("Specified provider is not supported.");
                }

                return provider;
            },
            provider => provider switch
            {
                ExecutionProvider.Cpu => ExecutionProviderPreflightResult.Available,
                ExecutionProvider.Cuda => ExecutionProviderPreflightResult.Failure(
                    new DllNotFoundException("CUDA runtime library 'cudart64_13.dll' was not found on the native library search path."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.Cuda)),
                ExecutionProvider.DirectML => ExecutionProviderPreflightResult.Unknown(
                    new InvalidOperationException("DirectML availability cannot be confirmed without initializing a model that references Microsoft.ML.OnnxRuntimeGenAI.DirectML."),
                    ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.DirectML)),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
            });

        Assert.Equal(ExecutionProvider.Cpu, result.ActiveProvider);
        Assert.Contains("Cuda", result.ProviderSelectionDetails);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal([ExecutionProvider.DirectML, ExecutionProvider.Cpu], createdProviders);
            Assert.Contains("DirectML", result.ProviderSelectionDetails);
        }
        else
        {
            Assert.Equal([ExecutionProvider.Cpu], createdProviders);
        }
    }

    [Fact]
    public void InitializeModel_ExplicitDirectMLUnknown_StillInvokesFactory()
    {
        var factoryCalled = false;

        var result = ExecutionProviderSelection.InitializeModel(
            ExecutionProvider.DirectML,
            NullLogger.Instance,
            provider =>
            {
                factoryCalled = true;
                return provider;
            },
            _ => ExecutionProviderPreflightResult.Unknown(
                new InvalidOperationException("DirectML availability cannot be confirmed without initializing a model that references Microsoft.ML.OnnxRuntimeGenAI.DirectML."),
                ExecutionProviderSelection.GetUnavailableSuggestion(ExecutionProvider.DirectML)));

        Assert.True(factoryCalled);
        Assert.Equal(ExecutionProvider.DirectML, result.ActiveProvider);
    }
}
