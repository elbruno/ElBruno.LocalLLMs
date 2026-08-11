using ElBruno.LocalLLMs.BlazorComponents.Components;
using ElBruno.LocalLLMs.Diagnostics;
using Xunit;

namespace ElBruno.LocalLLMs.BlazorComponents.Tests;

public sealed class LocalLLMHealthBadgePresenterTests
{
    [Fact]
    public void Create_WithUninitializedConfiguredCudaClient_ShowsUnavailableState()
    {
        using var client = new LocalChatClient(new LocalLLMsOptions
        {
            ExecutionProvider = ExecutionProvider.Cuda
        });

        var diagnostics = CreateDiagnostics(
            cudaStatus: ExecutionProviderDiagnosticStatus.Unavailable,
            cudaReason: "Cuda unavailable",
            directMLStatus: ExecutionProviderDiagnosticStatus.Unavailable,
            directMLReason: "DirectML unavailable");

        var viewModel = LocalLLMHealthBadgePresenter.Create(diagnostics, client);

        Assert.Equal("error", viewModel.State);
        Assert.Equal("LLM unavailable (Cuda)", viewModel.Label);
        Assert.DoesNotContain("LLM Ready", viewModel.Label);
        Assert.Contains("Configured provider: Cuda", viewModel.Tooltip);
        Assert.Contains("Cuda unavailable", viewModel.Tooltip);
    }

    [Fact]
    public void Create_WithUninitializedConfiguredDirectMLClient_ShowsUnknownState()
    {
        using var client = new LocalChatClient(new LocalLLMsOptions
        {
            ExecutionProvider = ExecutionProvider.DirectML
        });

        var diagnostics = CreateDiagnostics(
            cudaStatus: ExecutionProviderDiagnosticStatus.Unavailable,
            cudaReason: "Cuda unavailable",
            directMLStatus: ExecutionProviderDiagnosticStatus.Unknown,
            directMLReason: "DirectML availability cannot be confirmed.");

        var viewModel = LocalLLMHealthBadgePresenter.Create(diagnostics, client);

        Assert.Equal("unknown", viewModel.State);
        Assert.Equal("LLM pending (DirectML)", viewModel.Label);
        Assert.DoesNotContain("LLM Ready", viewModel.Label);
        Assert.Contains("Configured provider: DirectML", viewModel.Tooltip);
        Assert.Contains("DirectML availability cannot be confirmed.", viewModel.Tooltip);
    }

    [Theory]
    [InlineData(ExecutionProvider.Cpu, "phi-4-mini", null)]
    [InlineData(ExecutionProvider.Cuda, "qwen-3", "CUDA initialized successfully.")]
    public void Create_WithInitializedClientState_PreservesReadyLabel(
        ExecutionProvider provider,
        string modelId,
        string? providerSelectionDetails)
    {
        var diagnostics = CreateDiagnostics(
            cudaStatus: ExecutionProviderDiagnosticStatus.Available,
            cudaReason: null,
            directMLStatus: ExecutionProviderDiagnosticStatus.Unknown,
            directMLReason: "DirectML availability cannot be confirmed.");
        var clientState = new LocalLLMClientExecutionState(
            IsConfigured: true,
            IsInitialized: true,
            ModelId: modelId,
            ActiveExecutionProvider: provider,
            ProviderSelectionDetails: providerSelectionDetails);

        var viewModel = LocalLLMHealthBadgePresenter.Create(diagnostics, clientState);

        Assert.Equal("ok", viewModel.State);
        Assert.Equal($"LLM Ready ({modelId} · {provider})", viewModel.Label);
    }

    [Fact]
    public void Create_WithInitializedDirectMLClientState_UsesRuntimeSelectedProviderStatusInTooltip()
    {
        var diagnostics = CreateDiagnostics(
            cudaStatus: ExecutionProviderDiagnosticStatus.Unavailable,
            cudaReason: "Cuda unavailable",
            directMLStatus: ExecutionProviderDiagnosticStatus.Unknown,
            directMLReason: "DirectML availability cannot be confirmed.");
        var clientState = new LocalLLMClientExecutionState(
            IsConfigured: true,
            IsInitialized: true,
            ModelId: "phi-4-mini",
            ActiveExecutionProvider: ExecutionProvider.DirectML,
            ProviderSelectionDetails: "DirectML initialized successfully.");

        var viewModel = LocalLLMHealthBadgePresenter.Create(diagnostics, clientState);

        Assert.Equal("ok", viewModel.State);
        Assert.Contains("Selected provider: DirectML", viewModel.Tooltip);
        Assert.Contains("DirectML initialized successfully.", viewModel.Tooltip);
        Assert.Contains("CPU: Available", viewModel.Tooltip);
        Assert.Contains("CUDA: Unavailable", viewModel.Tooltip);
        Assert.Contains("DirectML: Available", viewModel.Tooltip);
        Assert.DoesNotContain("DirectML: Unknown", viewModel.Tooltip);
    }

    private static EnvironmentDiagnostics CreateDiagnostics(
        ExecutionProviderDiagnosticStatus cudaStatus,
        string? cudaReason,
        ExecutionProviderDiagnosticStatus directMLStatus,
        string? directMLReason)
    {
        return new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = cudaStatus == ExecutionProviderDiagnosticStatus.Available,
            DirectMLAvailable = directMLStatus == ExecutionProviderDiagnosticStatus.Available,
            DotNetVersion = ".NET test",
            ProcessorCount = 8,
            OSDescription = "Test OS",
            ProviderDiagnostics =
            [
                new ExecutionProviderDiagnostic
                {
                    Provider = ExecutionProvider.Cpu,
                    IsAvailable = true
                },
                new ExecutionProviderDiagnostic
                {
                    Provider = ExecutionProvider.Cuda,
                    IsAvailable = cudaStatus == ExecutionProviderDiagnosticStatus.Available,
                    Status = cudaStatus,
                    Reason = cudaReason
                },
                new ExecutionProviderDiagnostic
                {
                    Provider = ExecutionProvider.DirectML,
                    IsAvailable = directMLStatus == ExecutionProviderDiagnosticStatus.Available,
                    Status = directMLStatus,
                    Reason = directMLReason
                }
            ],
            AutoResolvedExecutionProvider = ExecutionProvider.Cpu,
            AutoResolvedExecutionProviderKnown = true,
            AutoResolvedExecutionDetails = "Auto would currently select Cpu based on provider preflight."
        };
    }
}
