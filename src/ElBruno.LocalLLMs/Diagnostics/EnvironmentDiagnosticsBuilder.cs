using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Diagnostics;

internal static class EnvironmentDiagnosticsBuilder
{
    private static readonly ExecutionProvider[] ProviderOrder =
    [
        ExecutionProvider.Cpu,
        ExecutionProvider.Cuda,
        ExecutionProvider.DirectML
    ];

    internal static EnvironmentDiagnostics Create(
        string? cacheDirectory = null,
        Func<ExecutionProvider, ExecutionProviderPreflightResult>? preflight = null)
    {
        preflight ??= ExecutionProviderPreflight.Validate;
        cacheDirectory ??= new ModelDownloader().GetCacheDirectory();

        var providerDiagnostics = BuildProviderDiagnostics(preflight);
        var autoResolution = ResolveAutoExecutionProvider(providerDiagnostics);

        return new EnvironmentDiagnostics
        {
            CpuAvailable = providerDiagnostics.First(d => d.Provider == ExecutionProvider.Cpu).IsAvailable,
            CudaAvailable = providerDiagnostics.First(d => d.Provider == ExecutionProvider.Cuda).IsAvailable,
            DirectMLAvailable = providerDiagnostics.First(d => d.Provider == ExecutionProvider.DirectML).IsAvailable,
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            OSDescription = RuntimeInformation.OSDescription,
            CacheDirectory = cacheDirectory,
            CacheSizeBytes = GetCacheSize(cacheDirectory),
            ProviderDiagnostics = providerDiagnostics,
            AutoResolvedExecutionProvider = autoResolution.ActiveProvider,
            AutoResolvedExecutionProviderKnown = autoResolution.IsKnown,
            AutoResolvedExecutionDetails = autoResolution.ProviderSelectionDetails
        };
    }

    private static IReadOnlyList<ExecutionProviderDiagnostic> BuildProviderDiagnostics(
        Func<ExecutionProvider, ExecutionProviderPreflightResult> preflight)
    {
        var diagnostics = new List<ExecutionProviderDiagnostic>(ProviderOrder.Length);

        foreach (var provider in ProviderOrder)
        {
            var result = preflight(provider);
            diagnostics.Add(new ExecutionProviderDiagnostic
            {
                Provider = provider,
                IsAvailable = result.IsAvailable,
                Status = result.IsAvailable
                    ? ExecutionProviderDiagnosticStatus.Available
                    : result.Status,
                Reason = result.IsAvailable ? null : result.Exception?.Message,
                Suggestion = result.IsAvailable ? null : result.Suggestion
            });
        }

        return diagnostics;
    }

    private static AutoResolutionDiagnostic ResolveAutoExecutionProvider(
        IReadOnlyList<ExecutionProviderDiagnostic> providerDiagnostics)
    {
        var diagnosticsByProvider = providerDiagnostics.ToDictionary(diagnostic => diagnostic.Provider);
        var failures = new List<string>();

        foreach (var candidate in ExecutionProviderSelection.GetProviderFallbackOrder(ExecutionProvider.Auto))
        {
            var diagnostic = diagnosticsByProvider[candidate];
            var status = diagnostic.IsAvailable
                ? ExecutionProviderDiagnosticStatus.Available
                : diagnostic.Status;

            if (status == ExecutionProviderDiagnosticStatus.Available)
            {
                return new AutoResolutionDiagnostic(
                    candidate,
                    true,
                    failures.Count > 0
                        ? $"Auto would currently select {candidate} after provider preflight fallbacks: {string.Join(" | ", failures)}"
                        : $"Auto would currently select {candidate} based on provider preflight.");
            }

            if (status == ExecutionProviderDiagnosticStatus.Unknown)
            {
                var reason = string.IsNullOrWhiteSpace(diagnostic.Reason)
                    ? $"{candidate} availability could not be confirmed from provider preflight alone."
                    : diagnostic.Reason;

                var details = failures.Count > 0
                    ? $"Auto resolution is unknown because {reason} Earlier unavailable providers: {string.Join(" | ", failures)}"
                    : $"Auto resolution is unknown because {reason}";

                return new AutoResolutionDiagnostic(ExecutionProvider.Auto, false, details);
            }

            failures.Add(BuildProviderFailureReason(diagnostic));
        }

        return new AutoResolutionDiagnostic(
            ExecutionProvider.Auto,
            false,
            failures.Count > 0
                ? $"Auto resolution is unknown. Unavailable providers: {string.Join(" | ", failures)}"
                : "Auto resolution is unknown because no provider readiness information was available.");
    }

    private static string BuildProviderFailureReason(ExecutionProviderDiagnostic diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic.Reason)
            ? $"{diagnostic.Provider}: unavailable"
            : $"{diagnostic.Provider}: {diagnostic.Reason}";
    }

    private static long GetCacheSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return 0;
        }

        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    private sealed record AutoResolutionDiagnostic(
        ExecutionProvider ActiveProvider,
        bool IsKnown,
        string? ProviderSelectionDetails);
}
