namespace ElBruno.LocalLLMs.BlazorComponents.Options;

/// <summary>
/// Options for the Blazor components package.
/// </summary>
public sealed class BlazorComponentsOptions
{
    /// <summary>
    /// Enables host-side folder open actions for model/cache directories.
    /// Disabled by default because Blazor Server actions run on the host machine,
    /// not in the remote browser.
    /// </summary>
    public bool EnableHostFolderActions { get; set; }
}
