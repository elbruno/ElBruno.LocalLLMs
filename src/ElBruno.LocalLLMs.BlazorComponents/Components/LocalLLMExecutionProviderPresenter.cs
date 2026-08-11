using ElBruno.LocalLLMs.Diagnostics;

namespace ElBruno.LocalLLMs.BlazorComponents.Components;

internal static class LocalLLMExecutionProviderPresenter
{
    internal static LocalLLMExecutionProviderSummary ResolveSelectedProvider(
        EnvironmentDiagnostics diagnostics,
        LocalChatClient? client)
        => ResolveSelectedProvider(diagnostics, LocalLLMClientExecutionState.FromClient(client));

    internal static LocalLLMExecutionProviderSummary ResolveSelectedProvider(
        EnvironmentDiagnostics diagnostics,
        LocalLLMClientExecutionState clientState)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(clientState);

        if (!clientState.IsConfigured)
        {
            return BuildAutoSelection(diagnostics);
        }

        if (!clientState.IsInitialized)
        {
            if (clientState.ActiveExecutionProvider == ExecutionProvider.Auto)
            {
                return BuildAutoSelection(diagnostics);
            }

            var configuredProvider = clientState.ActiveExecutionProvider;
            var configuredDiagnostic = FindProviderDiagnostic(diagnostics, configuredProvider);
            return new LocalLLMExecutionProviderSummary(
                "Configured provider",
                configuredProvider.ToString(),
                configuredDiagnostic is null ? ExecutionProviderDiagnosticStatus.Available : GetStatus(configuredDiagnostic),
                configuredDiagnostic?.Reason);
        }

        return new LocalLLMExecutionProviderSummary(
            "Selected provider",
            clientState.ActiveExecutionProvider.ToString(),
            ExecutionProviderDiagnosticStatus.Available,
            string.IsNullOrWhiteSpace(clientState.ProviderSelectionDetails)
                ? null
                : clientState.ProviderSelectionDetails);
    }

    internal static ExecutionProviderDiagnostic? FindProviderDiagnostic(
        EnvironmentDiagnostics diagnostics,
        ExecutionProvider provider)
    {
        return diagnostics.ProviderDiagnostics
            .FirstOrDefault(diagnostic => diagnostic.Provider == provider);
    }

    internal static ExecutionProviderDiagnosticStatus GetStatus(ExecutionProviderDiagnostic diagnostic)
    {
        return diagnostic.IsAvailable
            ? ExecutionProviderDiagnosticStatus.Available
            : diagnostic.Status;
    }

    internal static bool IsRuntimeSelectedProvider(
        LocalLLMClientExecutionState clientState,
        ExecutionProvider provider)
        => clientState.IsConfigured &&
           clientState.IsInitialized &&
           clientState.ActiveExecutionProvider == provider;

    private static LocalLLMExecutionProviderSummary BuildAutoSelection(EnvironmentDiagnostics diagnostics)
    {
        if (!IsAutoResolutionKnown(diagnostics))
        {
            return new LocalLLMExecutionProviderSummary(
                "Auto resolution",
                "Unknown",
                ExecutionProviderDiagnosticStatus.Unknown,
                diagnostics.AutoResolvedExecutionDetails);
        }

        var selectedDiagnostic = FindProviderDiagnostic(diagnostics, diagnostics.AutoResolvedExecutionProvider);
        return new LocalLLMExecutionProviderSummary(
            "Auto preflight suggests",
            diagnostics.AutoResolvedExecutionProvider.ToString(),
            selectedDiagnostic is null ? ExecutionProviderDiagnosticStatus.Available : GetStatus(selectedDiagnostic),
            diagnostics.AutoResolvedExecutionDetails);
    }

    private static bool IsAutoResolutionKnown(EnvironmentDiagnostics diagnostics)
    {
        return diagnostics.AutoResolvedExecutionProviderKnown ||
               diagnostics.AutoResolvedExecutionProvider != ExecutionProvider.Auto;
    }
}

internal sealed record LocalLLMExecutionProviderSummary(
    string Caption,
    string Label,
    ExecutionProviderDiagnosticStatus Status,
    string? Details);

internal sealed record LocalLLMClientExecutionState(
    bool IsConfigured,
    bool IsInitialized,
    string? ModelId,
    ExecutionProvider ActiveExecutionProvider,
    string? ProviderSelectionDetails)
{
    internal static LocalLLMClientExecutionState FromClient(LocalChatClient? client)
    {
        if (client is null)
        {
            return new LocalLLMClientExecutionState(
                IsConfigured: false,
                IsInitialized: false,
                ModelId: null,
                ActiveExecutionProvider: ExecutionProvider.Auto,
                ProviderSelectionDetails: null);
        }

        return new LocalLLMClientExecutionState(
            IsConfigured: true,
            IsInitialized: client.ModelInfo is not null || !string.IsNullOrWhiteSpace(client.ProviderSelectionDetails),
            ModelId: client.Metadata.DefaultModelId,
            ActiveExecutionProvider: client.ActiveExecutionProvider,
            ProviderSelectionDetails: client.ProviderSelectionDetails);
    }
}
