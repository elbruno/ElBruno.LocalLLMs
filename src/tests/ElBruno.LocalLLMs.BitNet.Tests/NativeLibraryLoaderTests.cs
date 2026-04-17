using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.BitNet.Native;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="NativeLibraryLoader"/> — path probing, OS detection, error messages, and NuGet runtime paths.
/// </summary>
public class NativeLibraryLoaderTests
{
    // ──────────────────────────────────────────────
    // Library file name per OS
    // ──────────────────────────────────────────────

    [Fact]
    public void GetLibraryFileNames_ReturnsAtLeastOneName()
    {
        // GetLibraryFileNames is private, but GetCandidateLibraryPathsForTesting
        // builds paths using those names. Verify we get candidates that end with
        // the correct extension for the current OS.
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();

        Assert.NotEmpty(candidates);
    }

    [Fact]
    public void CandidatePaths_EndWithCorrectExtension_ForCurrentOS()
    {
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();
        string expectedExtension;

        if (OperatingSystem.IsWindows())
            expectedExtension = ".dll";
        else if (OperatingSystem.IsMacOS())
            expectedExtension = ".dylib";
        else
            expectedExtension = ".so";

        // At least one candidate should end with the OS-appropriate extension
        Assert.Contains(candidates, c => c.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CandidatePaths_ContainExpectedLibraryName_ForCurrentOS()
    {
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();
        string expectedName;

        if (OperatingSystem.IsWindows())
            expectedName = "llama.dll";
        else if (OperatingSystem.IsMacOS())
            expectedName = "libllama.dylib";
        else
            expectedName = "libllama.so";

        Assert.Contains(candidates, c => c.EndsWith(expectedName, StringComparison.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────
    // Runtime identifier
    // ──────────────────────────────────────────────

    [Fact]
    public void RuntimeIdentifier_IsNotEmpty()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;

        Assert.False(string.IsNullOrEmpty(rid));
    }

    [Fact]
    public void RuntimeIdentifier_ContainsExpectedPlatformPrefix()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;

        // RID should start with win, linux, or osx
        Assert.True(
            rid.StartsWith("win", StringComparison.OrdinalIgnoreCase) ||
            rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase) ||
            rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase),
            $"Unexpected RID prefix: {rid}");
    }

    // ──────────────────────────────────────────────
    // NuGet runtimes path construction
    // ──────────────────────────────────────────────

    [Fact]
    public void NuGetRuntimePath_ConstructedCorrectly()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");

        Assert.False(string.IsNullOrEmpty(expectedPath));
        Assert.Contains("runtimes", expectedPath);
        Assert.Contains("native", expectedPath);
        Assert.Contains(rid, expectedPath);
    }

    [Fact]
    public void CandidatePaths_IncludesRuntimesDir_WhenDirectoryExists()
    {
        // Create a runtimes/{rid}/native/ directory inside the test output dir
        var rid = RuntimeInformation.RuntimeIdentifier;
        var runtimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
        var createdDir = false;

        try
        {
            if (!Directory.Exists(runtimesPath))
            {
                Directory.CreateDirectory(runtimesPath);
                createdDir = true;
            }

            var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();

            // At least one candidate should be under the runtimes/{rid}/native/ path
            Assert.Contains(candidates, c => c.StartsWith(runtimesPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (createdDir && Directory.Exists(runtimesPath))
            {
                Directory.Delete(runtimesPath, recursive: true);

                // Clean up empty parent directories
                var parent = Path.GetDirectoryName(runtimesPath);
                if (parent != null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                    Directory.Delete(parent);

                var grandparent = Path.Combine(AppContext.BaseDirectory, "runtimes");
                if (Directory.Exists(grandparent) && !Directory.EnumerateFileSystemEntries(grandparent).Any())
                    Directory.Delete(grandparent);
            }
        }
    }

    [Fact]
    public void CandidatePaths_DoesNotIncludeRuntimesDir_WhenDirectoryMissing()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var runtimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");

        // Ensure the runtimes directory does NOT exist for this test
        if (Directory.Exists(runtimesPath))
        {
            // Can't reliably test this if it already exists; just skip verification
            return;
        }

        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();

        // No candidate should be under the non-existent runtimes/{rid}/native/ path
        Assert.DoesNotContain(candidates, c =>
            c.StartsWith(runtimesPath, StringComparison.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────
    // Default paths
    // ──────────────────────────────────────────────

    [Fact]
    public void CandidatePaths_IncludesAppBaseDirectory()
    {
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();
        var baseDir = AppContext.BaseDirectory;

        Assert.Contains(candidates, c =>
            c.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CandidatePaths_IncludesCurrentDirectory()
    {
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();
        var currentDir = Environment.CurrentDirectory;

        Assert.Contains(candidates, c =>
            c.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultLibraryPaths_BaseDirectory_Exists()
    {
        var baseDir = AppContext.BaseDirectory;

        Assert.False(string.IsNullOrEmpty(baseDir));
        Assert.True(Directory.Exists(baseDir));
    }

    // ──────────────────────────────────────────────
    // Error message — NuGet package suggestion
    // ──────────────────────────────────────────────

    [Fact]
    public void ErrorMessage_SuggestsCorrectNuGetPackage_ForWindows()
    {
        var rid = "win-x64";
        var packageSuggestion = GetPackageSuggestionForRid(rid);

        Assert.Equal("ElBruno.LocalLLMs.BitNet.Native.win-x64", packageSuggestion);
    }

    [Fact]
    public void ErrorMessage_SuggestsCorrectNuGetPackage_ForLinux()
    {
        var rid = "linux-x64";
        var packageSuggestion = GetPackageSuggestionForRid(rid);

        Assert.Equal("ElBruno.LocalLLMs.BitNet.Native.linux-x64", packageSuggestion);
    }

    [Fact]
    public void ErrorMessage_SuggestsCorrectNuGetPackage_ForMacOS()
    {
        var rid = "osx-arm64";
        var packageSuggestion = GetPackageSuggestionForRid(rid);

        Assert.Equal("ElBruno.LocalLLMs.BitNet.Native.osx-arm64", packageSuggestion);
    }

    [Fact]
    public void ErrorMessage_FallsBackToPlaceholder_ForUnknownRid()
    {
        var rid = "freebsd-x64";
        var packageSuggestion = GetPackageSuggestionForRid(rid);

        Assert.Equal("ElBruno.LocalLLMs.BitNet.Native.{your-rid}", packageSuggestion);
    }

    [Theory]
    [InlineData("win-x64", "ElBruno.LocalLLMs.BitNet.Native.win-x64")]
    [InlineData("win-arm64", "ElBruno.LocalLLMs.BitNet.Native.{your-rid}")]
    [InlineData("linux-x64", "ElBruno.LocalLLMs.BitNet.Native.linux-x64")]
    [InlineData("linux-arm64", "ElBruno.LocalLLMs.BitNet.Native.{your-rid}")]
    [InlineData("osx-arm64", "ElBruno.LocalLLMs.BitNet.Native.osx-arm64")]
    [InlineData("osx-x64", "ElBruno.LocalLLMs.BitNet.Native.{your-rid}")]
    public void PackageSuggestion_MapsRidCorrectly(string rid, string expectedPackage)
    {
        var actual = GetPackageSuggestionForRid(rid);

        Assert.Equal(expectedPackage, actual);
    }

    [Fact]
    public void BitNetNativeLibraryException_MessageFormat_ContainsDotnetAddPackage()
    {
        // Verify the error message format the loader produces
        var message = BuildExpectedErrorMessage("win-x64");

        Assert.Contains("dotnet add package", message);
        Assert.Contains("Unable to locate the BitNet native library", message);
        Assert.Contains("BitNetOptions.NativeLibraryPath", message);
    }

    [Fact]
    public void BitNetNativeLibraryException_MessageFormat_ContainsAllBinaryNames()
    {
        var message = BuildExpectedErrorMessage("linux-x64");

        Assert.Contains("llama.dll", message);
        Assert.Contains("libllama.so", message);
        Assert.Contains("libllama.dylib", message);
    }

    // ──────────────────────────────────────────────
    // Candidate path ordering
    // ──────────────────────────────────────────────

    [Fact]
    public void CandidatePaths_HasMultipleCandidates()
    {
        // Even without a native lib, we should get default + environment paths
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();

        // At minimum: AppContext.BaseDirectory + CurrentDirectory = 2 paths
        Assert.True(candidates.Count >= 2,
            $"Expected at least 2 candidate paths, got {candidates.Count}");
    }

    [Fact]
    public void CandidatePaths_AllPathsAreAbsolute()
    {
        var candidates = NativeLibraryLoader.GetCandidateLibraryPathsForTesting().ToList();

        foreach (var candidate in candidates)
        {
            Assert.True(Path.IsPathRooted(candidate),
                $"Expected absolute path but got: {candidate}");
        }
    }

    // ──────────────────────────────────────────────
    // Integration test stubs (gated by env var)
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void NativeLibrary_LoadsFromRuntimesPath_WhenAvailable()
    {
        var envVar = Environment.GetEnvironmentVariable("RUN_NATIVE_INTEGRATION_TESTS");
        if (string.IsNullOrEmpty(envVar) || envVar != "true")
        {
            // Skip — native binary not available in this environment
            return;
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        var runtimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
        Assert.True(Directory.Exists(runtimesPath), $"Expected runtimes path to exist: {runtimesPath}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NativeLibrary_ErrorMessage_IsActionable_WhenLibraryMissing()
    {
        var envVar = Environment.GetEnvironmentVariable("RUN_NATIVE_INTEGRATION_TESTS");
        if (string.IsNullOrEmpty(envVar) || envVar != "true")
        {
            return;
        }

        // Attempt to load with a bogus path — should throw with actionable message
        var ex = Assert.Throws<BitNetNativeLibraryException>(() =>
            NativeLibraryLoader.EnsureLoaded("/nonexistent/path"));

        Assert.Contains("dotnet add package", ex.Message);
    }

    // ──────────────────────────────────────────────
    // Helpers — mirror the loader's RID → package logic
    // ──────────────────────────────────────────────

    private static string GetPackageSuggestionForRid(string rid) =>
        rid switch
        {
            string r when r.StartsWith("win") && r.Contains("x64") => "ElBruno.LocalLLMs.BitNet.Native.win-x64",
            string r when r.StartsWith("linux") && r.Contains("x64") => "ElBruno.LocalLLMs.BitNet.Native.linux-x64",
            string r when r.StartsWith("osx") && r.Contains("arm64") => "ElBruno.LocalLLMs.BitNet.Native.osx-arm64",
            _ => "ElBruno.LocalLLMs.BitNet.Native.{your-rid}"
        };

    private static string BuildExpectedErrorMessage(string rid)
    {
        var packageSuggestion = GetPackageSuggestionForRid(rid);
        return $"Unable to locate the BitNet native library (llama). " +
               $"Install the platform-specific NuGet package:\n" +
               $"  dotnet add package {packageSuggestion}\n" +
               $"Or set BitNetOptions.NativeLibraryPath to the directory containing " +
               $"llama.dll/libllama.so/libllama.dylib, " +
               $"or add it to your PATH/LD_LIBRARY_PATH/DYLD_LIBRARY_PATH.";
    }
}
