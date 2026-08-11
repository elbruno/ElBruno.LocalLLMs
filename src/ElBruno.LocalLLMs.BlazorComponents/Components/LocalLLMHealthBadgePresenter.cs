using ElBruno.LocalLLMs.Diagnostics;

namespace ElBruno.LocalLLMs.BlazorComponents.Components;

internal static class LocalLLMHealthBadgePresenter
{
    internal static LocalLLMHealthBadgeViewModel CreateNotConfigured()
        => new("unknown", "◌", "LLM not configured", "No LocalChatClient provided.");

    internal static LocalLLMHealthBadgeViewModel Create(
        EnvironmentDiagnostics diagnostics,
        LocalChatClient? client)
        => Create(diagnostics, LocalLLMClientExecutionState.FromClient(client));

    internal static LocalLLMHealthBadgeViewModel Create(
        EnvironmentDiagnostics diagnostics,
        LocalLLMClientExecutionState clientState)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(clientState);

        if (!clientState.IsConfigured)
        {
            return CreateNotConfigured();
        }

        var selectedProvider = LocalLLMExecutionProviderPresenter.ResolveSelectedProvider(diagnostics, clientState);

        if (clientState.IsInitialized)
        {
            var modelId = clientState.ModelId ?? "unknown";
            return new LocalLLMHealthBadgeViewModel(
                "ok",
                "●",
                $"LLM Ready ({modelId} · {selectedProvider.Label})",
                BuildTooltip(diagnostics, clientState, selectedProvider));
        }

        var label = selectedProvider.Status switch
        {
            ExecutionProviderDiagnosticStatus.Unavailable => $"LLM unavailable ({selectedProvider.Label})",
            _ when string.Equals(selectedProvider.Label, "Unknown", StringComparison.Ordinal) => "LLM provider unknown",
            _ => $"LLM pending ({selectedProvider.Label})"
        };

        return new LocalLLMHealthBadgeViewModel(
            selectedProvider.Status == ExecutionProviderDiagnosticStatus.Unavailable ? "error" : "unknown",
            selectedProvider.Status == ExecutionProviderDiagnosticStatus.Unavailable ? "●" : "◌",
            label,
            BuildTooltip(diagnostics, clientState, selectedProvider));
    }

    private static string BuildTooltip(
        EnvironmentDiagnostics diagnostics,
        LocalLLMClientExecutionState clientState,
        LocalLLMExecutionProviderSummary selectedProvider)
    {
        var lines = new List<string>
        {
            $"{selectedProvider.Caption}: {selectedProvider.Label}"
        };

        if (!string.IsNullOrWhiteSpace(selectedProvider.Details))
        {
            lines.Add(selectedProvider.Details);
        }

        lines.Add($"CPU: {GetProviderStatus(diagnostics, clientState, ExecutionProvider.Cpu, diagnostics.CpuAvailable)}");
        lines.Add($"CUDA: {GetProviderStatus(diagnostics, clientState, ExecutionProvider.Cuda, diagnostics.CudaAvailable)}");
        lines.Add($"DirectML: {GetProviderStatus(diagnostics, clientState, ExecutionProvider.DirectML, diagnostics.DirectMLAvailable)}");
        lines.Add($".NET: {diagnostics.DotNetVersion}  Cores: {diagnostics.ProcessorCount}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetProviderStatus(
        EnvironmentDiagnostics diagnostics,
        LocalLLMClientExecutionState clientState,
        ExecutionProvider provider,
        bool fallbackAvailability)
    {
        if (LocalLLMExecutionProviderPresenter.IsRuntimeSelectedProvider(clientState, provider))
        {
            return "Available";
        }

        var diagnostic = LocalLLMExecutionProviderPresenter.FindProviderDiagnostic(diagnostics, provider);

        if (diagnostic is null)
        {
            return fallbackAvailability ? "Available" : "Unavailable";
        }

        return LocalLLMExecutionProviderPresenter.GetStatus(diagnostic) switch
        {
            ExecutionProviderDiagnosticStatus.Available => "Available",
            ExecutionProviderDiagnosticStatus.Unknown => "Unknown",
            _ => "Unavailable"
        };
    }
}

internal sealed record LocalLLMHealthBadgeViewModel(
    string State,
    string Icon,
    string Label,
    string Tooltip);
