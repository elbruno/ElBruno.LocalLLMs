using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Diagnostics;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="EnvironmentDiagnostics"/> record and
/// <see cref="LocalChatClient.DiagnoseEnvironment"/>.
/// </summary>
public class EnvironmentDiagnosticsTests
{
    // ──────────────────────────────────────────────
    // DiagnoseEnvironment returns valid data
    // ──────────────────────────────────────────────

    [Fact]
    public void DiagnoseEnvironment_ReturnsNonNullResult()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.NotNull(diags);
    }

    [Fact]
    public void DiagnoseEnvironment_CpuAvailable_IsAlwaysTrue()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.True(diags.CpuAvailable);
    }

    [Fact]
    public void DiagnoseEnvironment_DotNetVersion_IsNotEmpty()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.False(string.IsNullOrWhiteSpace(diags.DotNetVersion));
    }

    [Fact]
    public void DiagnoseEnvironment_ProcessorCount_IsPositive()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.True(diags.ProcessorCount > 0);
    }

    [Fact]
    public void DiagnoseEnvironment_OSDescription_IsNotEmpty()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.False(string.IsNullOrWhiteSpace(diags.OSDescription));
    }

    [Fact]
    public void DiagnoseEnvironment_CacheSizeBytes_IsNonNegative()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();

        Assert.True(diags.CacheSizeBytes >= 0);
    }

    [Fact]
    public void DiagnoseEnvironment_UsesModelDownloaderDefaultCacheDirectory()
    {
        var diags = LocalChatClient.DiagnoseEnvironment();
        var downloader = new ModelDownloader();

        Assert.Equal(downloader.GetCacheDirectory(), diags.CacheDirectory);
    }

    [Fact]
    public void DiagnoseEnvironment_CustomCacheDirectory_ReportsConfiguredPathAndSize()
    {
        var cacheDirectory = CreateTestDirectory(nameof(DiagnoseEnvironment_CustomCacheDirectory_ReportsConfiguredPathAndSize));
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllText(Path.Combine(cacheDirectory, "weights.onnx"), "123456789");

        try
        {
            var diags = LocalChatClient.DiagnoseEnvironment(cacheDirectory);

            Assert.Equal(cacheDirectory, diags.CacheDirectory);
            Assert.True(diags.CacheSizeBytes >= 9);
        }
        finally
        {
            DeleteDirectory(cacheDirectory);
        }
    }

    [Fact]
    public void EnvironmentDiagnosticsBuilder_PopulatesProviderDiagnosticsAndAutoResolutionDetails()
    {
        var diags = EnvironmentDiagnosticsBuilder.Create(
            cacheDirectory: @"C:\cache",
            preflight: provider => provider switch
            {
                ExecutionProvider.Cpu => ExecutionProviderPreflightResult.Available,
                ExecutionProvider.Cuda => ExecutionProviderPreflightResult.Failure(
                    new DllNotFoundException("Cuda unavailable"),
                    "Install CUDA"),
                ExecutionProvider.DirectML => OperatingSystem.IsWindows()
                    ? ExecutionProviderPreflightResult.Failure(
                        new InvalidOperationException("DirectML unavailable"),
                        "Install DirectML runtime")
                    : ExecutionProviderPreflightResult.Failure(
                        new PlatformNotSupportedException("DirectML is only supported on Windows."),
                        "Windows only"),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
            });

        Assert.Equal(3, diags.ProviderDiagnostics.Count);
        Assert.Equal(ExecutionProvider.Cpu, diags.AutoResolvedExecutionProvider);
        Assert.True(diags.AutoResolvedExecutionProviderKnown);
        Assert.Contains(diags.ProviderDiagnostics, diagnostic =>
            diagnostic.Provider == ExecutionProvider.Cuda &&
            !diagnostic.IsAvailable &&
            diagnostic.Status == ExecutionProviderDiagnosticStatus.Unavailable &&
            diagnostic.Reason == "Cuda unavailable");

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("DirectML", diags.AutoResolvedExecutionDetails);
        }

        Assert.Contains("Cuda", diags.AutoResolvedExecutionDetails);
    }

    [Fact]
    public void ToString_UsesProviderDiagnosticStatusesWhenAvailable()
    {
        var diags = new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = false,
            DotNetVersion = ".NET 8.0.0",
            ProcessorCount = 8,
            OSDescription = "Windows 11",
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
            ]
        };

        var result = diags.ToString();

        Assert.Contains("CPU: Available", result);
        Assert.Contains("CUDA: Unavailable", result);
        Assert.Contains("DirectML: Unknown", result);
    }

    [Fact]
    public void DiagnoseEnvironment_OnWindowsCpuOnlyRuntime_DoesNotClaimDirectMLOrAutoResolution()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var diags = LocalChatClient.DiagnoseEnvironment();
        var directML = Assert.Single(
            diags.ProviderDiagnostics.Where(diagnostic => diagnostic.Provider == ExecutionProvider.DirectML));

        Assert.False(diags.DirectMLAvailable);
        Assert.False(diags.AutoResolvedExecutionProviderKnown);
        Assert.Equal(ExecutionProvider.Auto, diags.AutoResolvedExecutionProvider);
        Assert.Equal(ExecutionProviderDiagnosticStatus.Unknown, directML.Status);
        Assert.Contains("DirectML", directML.Reason);
        Assert.Contains("Auto resolution is unknown", diags.AutoResolvedExecutionDetails);
    }

    // ──────────────────────────────────────────────
    // Record — ToString
    // ──────────────────────────────────────────────

    [Fact]
    public void ToString_ContainsExpectedLabels()
    {
        var diags = new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = true,
            DotNetVersion = ".NET 8.0.0",
            ProcessorCount = 8,
            OSDescription = "Windows 11"
        };

        var result = diags.ToString();

        Assert.Contains("CPU: True", result);
        Assert.Contains("CUDA: False", result);
        Assert.Contains("DirectML: True", result);
        Assert.Contains(".NET: .NET 8.0.0", result);
        Assert.Contains("Cores: 8", result);
        Assert.Contains("OS: Windows 11", result);
    }

    // ──────────────────────────────────────────────
    // Record — equality
    // ──────────────────────────────────────────────

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = false,
            DotNetVersion = ".NET 8",
            ProcessorCount = 4,
            OSDescription = "Linux"
        };

        var b = new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = false,
            DirectMLAvailable = false,
            DotNetVersion = ".NET 8",
            ProcessorCount = 4,
            OSDescription = "Linux"
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new EnvironmentDiagnostics { ProcessorCount = 4 };
        var b = new EnvironmentDiagnostics { ProcessorCount = 8 };

        Assert.NotEqual(a, b);
    }

    // ──────────────────────────────────────────────
    // Record — default values
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultRecord_HasExpectedDefaults()
    {
        var diags = new EnvironmentDiagnostics();

        Assert.False(diags.CpuAvailable);
        Assert.False(diags.CudaAvailable);
        Assert.False(diags.DirectMLAvailable);
        Assert.Equal(string.Empty, diags.DotNetVersion);
        Assert.Equal(0, diags.ProcessorCount);
        Assert.Equal(string.Empty, diags.OSDescription);
        Assert.Null(diags.CacheDirectory);
        Assert.Equal(0L, diags.CacheSizeBytes);
        Assert.Empty(diags.ProviderDiagnostics);
        Assert.Equal(ExecutionProvider.Auto, diags.AutoResolvedExecutionProvider);
        Assert.False(diags.AutoResolvedExecutionProviderKnown);
        Assert.Null(diags.AutoResolvedExecutionDetails);
    }

    private static string CreateTestDirectory(string name)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "environment-diagnostics-tests",
            name,
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
