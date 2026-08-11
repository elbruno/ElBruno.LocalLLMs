using System.Diagnostics;
using ElBruno.LocalLLMs.BlazorComponents.Options;
using ElBruno.LocalLLMs.BlazorComponents.Services;
using Xunit;

namespace ElBruno.LocalLLMs.BlazorComponents.Tests;

public sealed class HostFolderLauncherTests
{
    [Fact]
    public async Task DisabledByDefault_ReturnsSafeNoOp()
    {
        var folder = CreateTestDirectory(nameof(DisabledByDefault_ReturnsSafeNoOp));
        Directory.CreateDirectory(folder);

        try
        {
            var processStarter = new RecordingProcessStarter();
            var launcher = new HostFolderLauncher(
                new BlazorComponentsOptions(),
                FakeHostPlatformInfo.Windows(),
                processStarter);

            var result = await launcher.TryOpenFolderAsync(folder);

            Assert.False(launcher.CanOpenFolders);
            Assert.False(result.Opened);
            Assert.Contains("disabled by default", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processStarter.StartInfo);
        }
        finally
        {
            DeleteDirectory(folder);
        }
    }

    [Theory]
    [InlineData("windows", "explorer.exe", true)]
    [InlineData("macos", "open", false)]
    [InlineData("linux", "xdg-open", false)]
    public async Task EnabledLauncher_UsesExpectedHostCommand(string platform, string expectedFileName, bool useShellExecute)
    {
        var folder = CreateTestDirectory(nameof(EnabledLauncher_UsesExpectedHostCommand) + "-" + platform);
        Directory.CreateDirectory(folder);

        try
        {
            var processStarter = new RecordingProcessStarter();
            var launcher = new HostFolderLauncher(
                new BlazorComponentsOptions { EnableHostFolderActions = true },
                FakeHostPlatformInfo.Create(platform),
                processStarter);

            var result = await launcher.TryOpenFolderAsync(folder);

            Assert.True(launcher.CanOpenFolders);
            Assert.True(result.Opened);
            Assert.NotNull(processStarter.StartInfo);
            Assert.Equal(expectedFileName, processStarter.StartInfo!.FileName);
            Assert.Equal(useShellExecute, processStarter.StartInfo.UseShellExecute);
            Assert.Single(processStarter.StartInfo.ArgumentList);
            Assert.Equal(folder, processStarter.StartInfo.ArgumentList[0]);
        }
        finally
        {
            DeleteDirectory(folder);
        }
    }

    [Fact]
    public async Task EnabledLauncher_OnUnsupportedPlatform_ReturnsUnavailable()
    {
        var launcher = new HostFolderLauncher(
            new BlazorComponentsOptions { EnableHostFolderActions = true },
            FakeHostPlatformInfo.Browser(),
            new RecordingProcessStarter());

        var result = await launcher.TryOpenFolderAsync(@"C:\not-used");

        Assert.False(launcher.CanOpenFolders);
        Assert.False(result.Opened);
        Assert.Contains("unavailable on this platform", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTestDirectory(string name)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "host-folder-launcher-tests",
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

    private sealed class RecordingProcessStarter : IProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }

        public void Start(ProcessStartInfo startInfo)
        {
            StartInfo = startInfo;
        }
    }

    private sealed class FakeHostPlatformInfo : IHostPlatformInfo
    {
        private FakeHostPlatformInfo(bool isWindows, bool isMacOS, bool isLinux, bool isBrowser)
        {
            IsWindows = isWindows;
            IsMacOS = isMacOS;
            IsLinux = isLinux;
            IsBrowser = isBrowser;
        }

        public bool IsWindows { get; }

        public bool IsMacOS { get; }

        public bool IsLinux { get; }

        public bool IsBrowser { get; }

        public static FakeHostPlatformInfo Windows() => new(true, false, false, false);

        public static FakeHostPlatformInfo Browser() => new(false, false, false, true);

        public static FakeHostPlatformInfo Create(string platform) => platform switch
        {
            "windows" => Windows(),
            "macos" => new FakeHostPlatformInfo(false, true, false, false),
            "linux" => new FakeHostPlatformInfo(false, false, true, false),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
    }
}
