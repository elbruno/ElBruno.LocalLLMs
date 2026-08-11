using ElBruno.LocalLLMs.BlazorComponents.Components;
using ElBruno.LocalLLMs.Diagnostics;
using Xunit;

namespace ElBruno.LocalLLMs.BlazorComponents.Tests;

public sealed class EnvironmentDashboardPresenterTests
{
    [Fact]
    public void Create_WithoutClient_UsesAutoResolutionSummary()
    {
        var diagnostics = CreateKnownAutoDiagnostics();

        var viewModel = EnvironmentDashboardPresenter.Create(
            diagnostics,
            client: null,
            showOpenFolderButton: true,
            canOpenCacheFolder: false,
            cacheFolderButtonTitle: "Disabled");

        Assert.Equal("Auto preflight suggests", viewModel.SelectedProviderCaption);
        Assert.Equal("Cpu", viewModel.SelectedProviderLabel);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Available, viewModel.SelectedProviderStatus);
        Assert.Equal("Auto would currently select Cpu after provider preflight fallbacks: Cuda unavailable", viewModel.SelectedProviderDetails);
        Assert.True(viewModel.ShowOpenFolderButton);
        Assert.False(viewModel.CanOpenCacheFolder);
        Assert.Contains(viewModel.ProviderRows, row =>
            row.Provider == ExecutionProvider.Cuda &&
            row.Status == ExecutionProviderDiagnosticStatus.Unavailable &&
            row.Detail == "Cuda unavailable");
    }

    [Fact]
    public void Create_WithoutClient_ShowsUnknownWhenAutoResolutionCannotBeConfirmed()
    {
        var diagnostics = CreateUnknownAutoDiagnostics();

        var viewModel = EnvironmentDashboardPresenter.Create(
            diagnostics,
            client: null,
            showOpenFolderButton: false,
            canOpenCacheFolder: false,
            cacheFolderButtonTitle: string.Empty);

        Assert.Equal("Auto resolution", viewModel.SelectedProviderCaption);
        Assert.Equal("Unknown", viewModel.SelectedProviderLabel);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Unknown, viewModel.SelectedProviderStatus);
        Assert.Contains("DirectML", viewModel.SelectedProviderDetails);
        Assert.Contains(viewModel.ProviderRows, row =>
            row.Provider == ExecutionProvider.DirectML &&
            row.Status == ExecutionProviderDiagnosticStatus.Unknown &&
            row.Detail == "DirectML availability cannot be confirmed.");
    }

    [Fact]
    public void Create_WithUninitializedExplicitClient_ShowsConfiguredProviderReason()
    {
        using var client = new LocalChatClient(new LocalLLMsOptions
        {
            ExecutionProvider = ExecutionProvider.Cuda
        });

        var diagnostics = CreateKnownAutoDiagnostics();

        var viewModel = EnvironmentDashboardPresenter.Create(
            diagnostics,
            client,
            showOpenFolderButton: false,
            canOpenCacheFolder: false,
            cacheFolderButtonTitle: string.Empty);

        Assert.Equal("Configured provider", viewModel.SelectedProviderCaption);
        Assert.Equal("Cuda", viewModel.SelectedProviderLabel);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Unavailable, viewModel.SelectedProviderStatus);
        Assert.Equal("Cuda unavailable", viewModel.SelectedProviderDetails);
    }

    [Fact]
    public void Create_WithInitializedDirectMLClientState_OverlaysRuntimeSelectedProviderRow()
    {
        var diagnostics = CreateUnknownAutoDiagnostics();
        var clientState = new LocalLLMClientExecutionState(
            IsConfigured: true,
            IsInitialized: true,
            ModelId: "phi-4-mini",
            ActiveExecutionProvider: ExecutionProvider.DirectML,
            ProviderSelectionDetails: "DirectML initialized successfully.");

        var viewModel = EnvironmentDashboardPresenter.Create(
            diagnostics,
            clientState,
            showOpenFolderButton: false,
            canOpenCacheFolder: false,
            cacheFolderButtonTitle: string.Empty);

        Assert.Equal("Selected provider", viewModel.SelectedProviderCaption);
        Assert.Equal("DirectML", viewModel.SelectedProviderLabel);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Available, viewModel.SelectedProviderStatus);
        Assert.Equal("DirectML initialized successfully.", viewModel.SelectedProviderDetails);
        Assert.Contains(viewModel.ProviderRows, row =>
            row.Provider == ExecutionProvider.DirectML &&
            row.Status == ExecutionProviderDiagnosticStatus.Available &&
            row.Detail == "Confirmed by runtime initialization.");
        Assert.Contains(viewModel.ProviderRows, row =>
            row.Provider == ExecutionProvider.Cuda &&
            row.Status == ExecutionProviderDiagnosticStatus.Unavailable &&
            row.Detail == "Cuda unavailable");
    }

    private static EnvironmentDiagnostics CreateKnownAutoDiagnostics()
    {
        return new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = false,
            DotNetVersion = ".NET test",
            ProcessorCount = 8,
            OSDescription = "Test OS",
            CacheDirectory = @"C:\cache",
            CacheSizeBytes = 42,
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
                    IsAvailable = false,
                    Status = ExecutionProviderDiagnosticStatus.Unavailable,
                    Reason = "Cuda unavailable"
                },
                new ExecutionProviderDiagnostic
                {
                    Provider = ExecutionProvider.DirectML,
                    IsAvailable = false,
                    Status = ExecutionProviderDiagnosticStatus.Unavailable,
                    Reason = "DirectML unavailable"
                }
            ],
            AutoResolvedExecutionProvider = ExecutionProvider.Cpu,
            AutoResolvedExecutionProviderKnown = true,
            AutoResolvedExecutionDetails = "Auto would currently select Cpu after provider preflight fallbacks: Cuda unavailable"
        };
    }

    private static EnvironmentDiagnostics CreateUnknownAutoDiagnostics()
    {
        return new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = false,
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
                    IsAvailable = false,
                    Status = ExecutionProviderDiagnosticStatus.Unavailable,
                    Reason = "Cuda unavailable"
                },
                new ExecutionProviderDiagnostic
                {
                    Provider = ExecutionProvider.DirectML,
                    IsAvailable = false,
                    Status = ExecutionProviderDiagnosticStatus.Unknown,
                    Reason = "DirectML availability cannot be confirmed."
                }
            ],
            AutoResolvedExecutionProvider = ExecutionProvider.Auto,
            AutoResolvedExecutionDetails = "Auto resolution is unknown because DirectML availability cannot be confirmed.",
            AutoResolvedExecutionProviderKnown = false
        };
    }
}
