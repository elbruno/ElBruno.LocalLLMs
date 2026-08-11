using ElBruno.LocalLLMs.Diagnostics;

namespace ElBruno.LocalLLMs.BlazorComponents.Components;

internal static class EnvironmentDashboardPresenter
{
    internal static EnvironmentDashboardViewModel Create(
        EnvironmentDiagnostics diagnostics,
        LocalChatClient? client,
        bool showOpenFolderButton,
        bool canOpenCacheFolder,
        string cacheFolderButtonTitle)
        => Create(
            diagnostics,
            LocalLLMClientExecutionState.FromClient(client),
            showOpenFolderButton,
            canOpenCacheFolder,
            cacheFolderButtonTitle);

    internal static EnvironmentDashboardViewModel Create(
        EnvironmentDiagnostics diagnostics,
        LocalLLMClientExecutionState clientState,
        bool showOpenFolderButton,
        bool canOpenCacheFolder,
        string cacheFolderButtonTitle)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(clientState);

        var selectedProvider = LocalLLMExecutionProviderPresenter.ResolveSelectedProvider(diagnostics, clientState);
        var providerRows = diagnostics.ProviderDiagnostics
            .Select(diagnostic => BuildProviderRow(diagnostic, clientState))
            .ToArray();

        return new EnvironmentDashboardViewModel(
            selectedProvider.Caption,
            selectedProvider.Label,
            selectedProvider.Status,
            selectedProvider.Details,
            providerRows,
            showOpenFolderButton,
            canOpenCacheFolder,
            cacheFolderButtonTitle);
    }

    private static EnvironmentDashboardProviderRow BuildProviderRow(
        ExecutionProviderDiagnostic diagnostic,
        LocalLLMClientExecutionState clientState)
    {
        var status = LocalLLMExecutionProviderPresenter.IsRuntimeSelectedProvider(clientState, diagnostic.Provider)
            ? ExecutionProviderDiagnosticStatus.Available
            : LocalLLMExecutionProviderPresenter.GetStatus(diagnostic);

        return new EnvironmentDashboardProviderRow(
            diagnostic.Provider,
            status,
            BuildProviderDetail(diagnostic, clientState));
    }

    private static string? BuildProviderDetail(
        ExecutionProviderDiagnostic diagnostic,
        LocalLLMClientExecutionState clientState)
    {
        if (LocalLLMExecutionProviderPresenter.IsRuntimeSelectedProvider(clientState, diagnostic.Provider) &&
            LocalLLMExecutionProviderPresenter.GetStatus(diagnostic) != ExecutionProviderDiagnosticStatus.Available)
        {
            return "Confirmed by runtime initialization.";
        }

        var status = LocalLLMExecutionProviderPresenter.GetStatus(diagnostic);
        if (status != ExecutionProviderDiagnosticStatus.Available)
        {
            return string.IsNullOrWhiteSpace(diagnostic.Reason)
                ? status == ExecutionProviderDiagnosticStatus.Unknown
                    ? $"{diagnostic.Provider} readiness could not be confirmed without initializing a model."
                    : $"{diagnostic.Provider} is unavailable."
                : diagnostic.Reason;
        }

        return diagnostic.Provider switch
        {
            ExecutionProvider.Cpu => "Always available.",
            ExecutionProvider.Cuda => "Native CUDA provider libraries loaded successfully.",
            ExecutionProvider.DirectML => "DirectML availability was confirmed.",
            _ => null
        };
    }
}

internal sealed record EnvironmentDashboardViewModel(
    string SelectedProviderCaption,
    string SelectedProviderLabel,
    ExecutionProviderDiagnosticStatus SelectedProviderStatus,
    string? SelectedProviderDetails,
    IReadOnlyList<EnvironmentDashboardProviderRow> ProviderRows,
    bool ShowOpenFolderButton,
    bool CanOpenCacheFolder,
    string CacheFolderButtonTitle);

internal sealed record EnvironmentDashboardProviderRow(
    ExecutionProvider Provider,
    ExecutionProviderDiagnosticStatus Status,
    string? Detail);
