namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Validates that native NuGet package project structure is correct.
/// These tests verify the on-disk layout of the native platform-specific packages.
/// </summary>
public class NativePackageValidationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ElBruno.LocalLLMs.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not find repo root");
    }

    // ──────────────────────────────────────────────
    // Project directory structure
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_DirectoryExists(string rid)
    {
        var projectDir = Path.Combine(RepoRoot, "src", $"ElBruno.LocalLLMs.BitNet.Native.{rid}");

        Assert.True(Directory.Exists(projectDir),
            $"Native project directory should exist: {projectDir}");
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_HasRuntimesDirectory(string rid)
    {
        var runtimesDir = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            "runtimes", rid, "native");

        Assert.True(Directory.Exists(runtimesDir),
            $"runtimes/{rid}/native/ directory should exist: {runtimesDir}");
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_RuntimesDirMatchesRid(string rid)
    {
        // Verify there's no mismatch between project RID and directory RID
        var runtimesBase = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            "runtimes");

        if (!Directory.Exists(runtimesBase))
            return;

        var subdirs = Directory.GetDirectories(runtimesBase);
        Assert.Single(subdirs);
        Assert.Equal(rid, Path.GetFileName(subdirs[0]));
    }

    // ──────────────────────────────────────────────
    // Project files (.csproj)
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_HasCsproj(string rid)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        Assert.True(File.Exists(csprojPath),
            $"Project file should exist: {csprojPath}");
    }

    [Theory]
    [InlineData("win-x64", "llama.dll")]
    [InlineData("linux-x64", "libllama.so")]
    [InlineData("osx-arm64", "libllama.dylib")]
    public void NativeProject_CsprojReferencesCorrectBinary(string rid, string expectedBinary)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        if (!File.Exists(csprojPath))
            return; // Skip if project doesn't exist yet

        var content = File.ReadAllText(csprojPath);
        Assert.Contains(expectedBinary, content);
        Assert.Contains("runtimes", content);
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_HasNoBuildTrue(string rid)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        if (!File.Exists(csprojPath))
            return;

        var content = File.ReadAllText(csprojPath);
        Assert.Contains("<NoBuild>true</NoBuild>", content);
        Assert.Contains("<IncludeBuildOutput>false</IncludeBuildOutput>", content);
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_IncludesNugetLogo(string rid)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        if (!File.Exists(csprojPath))
            return;

        var content = File.ReadAllText(csprojPath);
        Assert.Contains("nuget_logo.png", content);
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_CsprojHasPackTrue(string rid)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        if (!File.Exists(csprojPath))
            return;

        var content = File.ReadAllText(csprojPath);
        // Native content items must be marked Pack="true"
        Assert.Contains("Pack=\"true\"", content);
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("osx-arm64")]
    public void NativeProject_TargetsCorrectRid(string rid)
    {
        var csprojPath = Path.Combine(RepoRoot, "src",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
            $"ElBruno.LocalLLMs.BitNet.Native.{rid}.csproj");

        if (!File.Exists(csprojPath))
            return;

        var content = File.ReadAllText(csprojPath);
        Assert.Contains(rid, content);
    }

    // ──────────────────────────────────────────────
    // Solution file includes native projects
    // ──────────────────────────────────────────────

    [Fact]
    public void SlnxFile_ContainsAllNativeProjects()
    {
        var slnxPath = Path.Combine(RepoRoot, "ElBruno.LocalLLMs.slnx");
        var content = File.ReadAllText(slnxPath);

        Assert.Contains("BitNet.Native.win-x64", content);
        Assert.Contains("BitNet.Native.linux-x64", content);
        Assert.Contains("BitNet.Native.osx-arm64", content);
    }

    [Fact]
    public void SlnxFile_NativeProjectsInSrcFolder()
    {
        var slnxPath = Path.Combine(RepoRoot, "ElBruno.LocalLLMs.slnx");
        var content = File.ReadAllText(slnxPath);

        // Native projects should be under the /src/ folder in the solution
        Assert.Contains("src/ElBruno.LocalLLMs.BitNet.Native", content);
    }

    // ──────────────────────────────────────────────
    // Cross-platform consistency
    // ──────────────────────────────────────────────

    [Fact]
    public void AllThreeNativeProjectDirectories_Exist()
    {
        var rids = new[] { "win-x64", "linux-x64", "osx-arm64" };
        var missing = rids.Where(rid =>
        {
            var dir = Path.Combine(RepoRoot, "src", $"ElBruno.LocalLLMs.BitNet.Native.{rid}");
            return !Directory.Exists(dir);
        }).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void AllThreeRuntimesDirectories_HaveConsistentStructure()
    {
        var rids = new[] { "win-x64", "linux-x64", "osx-arm64" };

        foreach (var rid in rids)
        {
            var nativeDir = Path.Combine(RepoRoot, "src",
                $"ElBruno.LocalLLMs.BitNet.Native.{rid}",
                "runtimes", rid, "native");
            Assert.True(Directory.Exists(nativeDir),
                $"runtimes/{rid}/native/ should exist for all platforms");
        }
    }
}
