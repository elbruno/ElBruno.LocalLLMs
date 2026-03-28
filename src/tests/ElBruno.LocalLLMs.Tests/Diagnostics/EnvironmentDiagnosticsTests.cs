using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Diagnostics;

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
    }
}
