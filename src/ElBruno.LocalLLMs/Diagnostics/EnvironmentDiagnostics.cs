namespace ElBruno.LocalLLMs.Diagnostics;

/// <summary>
/// Results from environment diagnostics check.
/// </summary>
public sealed record EnvironmentDiagnostics
{
    /// <summary>Whether CPU execution is available (always true).</summary>
    public bool CpuAvailable { get; init; }

    /// <summary>
    /// Whether CUDA GPU acceleration has been confirmed available.
    /// Use <see cref="ProviderDiagnostics"/> to distinguish unavailable from unknown.
    /// </summary>
    public bool CudaAvailable { get; init; }

    /// <summary>
    /// Whether DirectML GPU acceleration has been confirmed available.
    /// Use <see cref="ProviderDiagnostics"/> to distinguish unavailable from unknown.
    /// </summary>
    public bool DirectMLAvailable { get; init; }

    /// <summary>The .NET runtime version description.</summary>
    public string DotNetVersion { get; init; } = string.Empty;

    /// <summary>Number of logical processors.</summary>
    public int ProcessorCount { get; init; }

    /// <summary>Operating system description.</summary>
    public string OSDescription { get; init; } = string.Empty;

    /// <summary>Default cache directory path for downloaded models.</summary>
    public string? CacheDirectory { get; init; }

    /// <summary>Total size of cached models in bytes.</summary>
    public long CacheSizeBytes { get; init; }

    /// <summary>
    /// Readiness details for each execution provider that the library can evaluate.
    /// </summary>
    public IReadOnlyList<ExecutionProviderDiagnostic> ProviderDiagnostics { get; init; } = Array.Empty<ExecutionProviderDiagnostic>();

    /// <summary>
    /// The provider that <see cref="ExecutionProvider.Auto"/> would currently resolve to.
    /// When <see cref="AutoResolvedExecutionProviderKnown"/> is <see langword="false"/>,
    /// the value remains <see cref="ExecutionProvider.Auto"/>.
    /// </summary>
    public ExecutionProvider AutoResolvedExecutionProvider { get; init; } = ExecutionProvider.Auto;

    /// <summary>
    /// Whether diagnostics can safely predict which provider <see cref="ExecutionProvider.Auto"/> would choose.
    /// </summary>
    public bool AutoResolvedExecutionProviderKnown { get; init; }

    /// <summary>
    /// Optional details explaining the current <see cref="ExecutionProvider.Auto"/> result,
    /// fallback reasons, or why the resolution is still unknown.
    /// </summary>
    public string? AutoResolvedExecutionDetails { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"CPU: {FormatProviderStatus(ExecutionProvider.Cpu, CpuAvailable)}, " +
               $"CUDA: {FormatProviderStatus(ExecutionProvider.Cuda, CudaAvailable)}, " +
               $"DirectML: {FormatProviderStatus(ExecutionProvider.DirectML, DirectMLAvailable)}, " +
               $".NET: {DotNetVersion}, Cores: {ProcessorCount}, OS: {OSDescription}";
    }

    private string FormatProviderStatus(ExecutionProvider provider, bool fallbackAvailability)
    {
        var diagnostic = ProviderDiagnostics.FirstOrDefault(item => item.Provider == provider);
        if (diagnostic is null)
        {
            return fallbackAvailability.ToString();
        }

        var status = diagnostic.IsAvailable
            ? ExecutionProviderDiagnosticStatus.Available
            : diagnostic.Status;

        return status switch
        {
            ExecutionProviderDiagnosticStatus.Available => "Available",
            ExecutionProviderDiagnosticStatus.Unknown => "Unknown",
            _ => "Unavailable"
        };
    }
}
