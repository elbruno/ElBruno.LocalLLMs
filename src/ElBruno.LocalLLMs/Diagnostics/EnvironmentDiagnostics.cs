namespace ElBruno.LocalLLMs.Diagnostics;

/// <summary>
/// Results from environment diagnostics check.
/// </summary>
public sealed record EnvironmentDiagnostics
{
    /// <summary>Whether CPU execution is available (always true).</summary>
    public bool CpuAvailable { get; init; }

    /// <summary>Whether CUDA GPU acceleration may be available.</summary>
    public bool CudaAvailable { get; init; }

    /// <summary>Whether DirectML GPU acceleration may be available.</summary>
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

    /// <inheritdoc />
    public override string ToString()
    {
        return $"CPU: {CpuAvailable}, CUDA: {CudaAvailable}, DirectML: {DirectMLAvailable}, " +
               $".NET: {DotNetVersion}, Cores: {ProcessorCount}, OS: {OSDescription}";
    }
}
