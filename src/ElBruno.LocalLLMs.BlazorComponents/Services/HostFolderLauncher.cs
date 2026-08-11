using System.Diagnostics;
using ElBruno.LocalLLMs.BlazorComponents.Options;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalLLMs.BlazorComponents.Services;

/// <summary>
/// Opens model or cache folders on the current host when explicitly enabled.
/// </summary>
public interface IHostFolderLauncher
{
    /// <summary>Whether folder actions are currently enabled and supported.</summary>
    bool CanOpenFolders { get; }

    /// <summary>Why folder actions are unavailable.</summary>
    string UnavailableReason { get; }

    /// <summary>Attempts to open the specified folder on the host machine.</summary>
    Task<FolderLaunchResult> TryOpenFolderAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from trying to open a host folder.
/// </summary>
public sealed record FolderLaunchResult(bool Opened, string Message);

/// <summary>
/// Default implementation of <see cref="IHostFolderLauncher"/>.
/// </summary>
public sealed class HostFolderLauncher : IHostFolderLauncher
{
    private const string DisabledByDefaultMessage =
        "Folder actions are disabled by default to avoid opening directories on a remote or server host. Enable BlazorComponentsOptions.EnableHostFolderActions to opt in.";

    private readonly BlazorComponentsOptions _options;
    private readonly IHostPlatformInfo _platformInfo;
    private readonly IProcessStarter _processStarter;

    /// <summary>
    /// Creates a launcher using configured <see cref="BlazorComponentsOptions"/>.
    /// </summary>
    public HostFolderLauncher(IOptions<BlazorComponentsOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), new RuntimeHostPlatformInfo(), new DefaultProcessStarter())
    {
    }

    internal HostFolderLauncher(
        BlazorComponentsOptions options,
        IHostPlatformInfo platformInfo,
        IProcessStarter processStarter)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
        _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
    }

    /// <inheritdoc />
    public bool CanOpenFolders => _options.EnableHostFolderActions && SupportsProcessLaunch;

    /// <inheritdoc />
    public string UnavailableReason =>
        !_options.EnableHostFolderActions
            ? DisabledByDefaultMessage
            : SupportsProcessLaunch
                ? string.Empty
                : "Folder actions are unavailable on this platform.";

    /// <inheritdoc />
    public Task<FolderLaunchResult> TryOpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(new FolderLaunchResult(false, "Folder path is required."));
        }

        if (!CanOpenFolders)
        {
            return Task.FromResult(new FolderLaunchResult(false, UnavailableReason));
        }

        if (!Directory.Exists(path))
        {
            return Task.FromResult(new FolderLaunchResult(false, $"Folder '{path}' does not exist."));
        }

        var startInfo = CreateStartInfo(path);
        if (startInfo is null)
        {
            return Task.FromResult(new FolderLaunchResult(false, UnavailableReason));
        }

        try
        {
            _processStarter.Start(startInfo);
            return Task.FromResult(new FolderLaunchResult(true, $"Opened '{path}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FolderLaunchResult(false, ex.Message));
        }
    }

    internal ProcessStartInfo? CreateStartInfo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_platformInfo.IsBrowser)
        {
            return null;
        }

        if (_platformInfo.IsWindows)
        {
            return CreateStartInfo("explorer.exe", path, useShellExecute: true);
        }

        if (_platformInfo.IsMacOS)
        {
            return CreateStartInfo("open", path, useShellExecute: false);
        }

        if (_platformInfo.IsLinux)
        {
            return CreateStartInfo("xdg-open", path, useShellExecute: false);
        }

        return null;
    }

    private bool SupportsProcessLaunch =>
        !_platformInfo.IsBrowser &&
        (_platformInfo.IsWindows || _platformInfo.IsMacOS || _platformInfo.IsLinux);

    private static ProcessStartInfo CreateStartInfo(string fileName, string path, bool useShellExecute)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = useShellExecute
        };
        startInfo.ArgumentList.Add(path);
        return startInfo;
    }
}

internal interface IHostPlatformInfo
{
    bool IsWindows { get; }

    bool IsMacOS { get; }

    bool IsLinux { get; }

    bool IsBrowser { get; }
}

file sealed class RuntimeHostPlatformInfo : IHostPlatformInfo
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsMacOS => OperatingSystem.IsMacOS();

    public bool IsLinux => OperatingSystem.IsLinux();

    public bool IsBrowser => OperatingSystem.IsBrowser();
}

internal interface IProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}

file sealed class DefaultProcessStarter : IProcessStarter
{
    public void Start(ProcessStartInfo startInfo)
    {
        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("The folder-open process could not be started.");
    }
}
