using System.Diagnostics;
using System.IO.Compression;

namespace ElBruno.LocalLLMs.IntegrationTests;

public class PlaceholderIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Pack_ShouldIncludeBuildTransitiveAssets()
    {
        var repoRoot = FindRepositoryRoot();
        var csprojPath = Path.Combine(repoRoot, "src", "ElBruno.LocalLLMs", "ElBruno.LocalLLMs.csproj");
        var workDir = Path.Combine(Path.GetTempPath(), "ElBruno.LocalLLMs.Tests", Guid.NewGuid().ToString("N"));
        var packageOutDir = Path.Combine(workDir, "nupkg");
        Directory.CreateDirectory(packageOutDir);

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{csprojPath}\" -c Release -p:TargetFrameworks=net8.0 -o \"{packageOutDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            Assert.NotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"dotnet pack failed.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");

            var nupkgPath = Directory.GetFiles(packageOutDir, "ElBruno.LocalLLMs.*.nupkg")
                .Single(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));

            using var archive = ZipFile.OpenRead(nupkgPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "buildTransitive/ElBruno.LocalLLMs.props");

            var targetsEntry = Assert.Single(archive.Entries.Where(e => e.FullName == "buildTransitive/ElBruno.LocalLLMs.targets"));
            using var reader = new StreamReader(targetsEntry.Open());
            var targetsContent = reader.ReadToEnd();
            Assert.Contains("onnxruntime-genai.dll", targetsContent, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "ElBruno.LocalLLMs.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output path.");
    }
}
