using System.ComponentModel;

namespace MagenticUIServer.Agents.Tools;

/// <summary>
/// Sandboxed file-system operations exposed as MEAI AIFunctions.
/// All paths are resolved relative to the working directory; access outside it is denied.
/// </summary>
public sealed class FileSurferTool
{
    private readonly string _workingDirectory;

    public FileSurferTool(string workingDirectory)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    [Description("Reads the text content of a file at the given path relative to the working directory.")]
    public string ReadFile(
        [Description("Relative path to the file inside the working directory")] string relativePath)
    {
        var fullPath = ResolveSandboxed(relativePath);
        if (!File.Exists(fullPath))
            return $"Error: file not found: {relativePath}";

        var text = File.ReadAllText(fullPath);
        return text.Length > 8000 ? text[..8000] + "\n...[truncated]" : text;
    }

    [Description("Writes text content to a file at the given path relative to the working directory. Creates or overwrites.")]
    public void WriteFile(
        [Description("Relative path to the file inside the working directory")] string relativePath,
        [Description("Text content to write")] string content)
    {
        var fullPath = ResolveSandboxed(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Description("Lists files and subdirectories at the given path relative to the working directory. Defaults to the working directory root.")]
    public string ListDirectory(
        [Description("Relative path to list, or null/empty for the working directory root")] string? relativePath = null)
    {
        var dir = string.IsNullOrWhiteSpace(relativePath)
            ? _workingDirectory
            : ResolveSandboxed(relativePath);

        if (!Directory.Exists(dir))
            return $"Error: directory not found: {relativePath}";

        var entries = Directory.GetFileSystemEntries(dir)
            .Select(e => Path.GetFileName(e) + (Directory.Exists(e) ? "/" : ""))
            .OrderBy(e => e);

        return string.Join("\n", entries);
    }

    // ── Sandbox enforcement ───────────────────────────────────────────────────

    private string ResolveSandboxed(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, relativePath));
        if (!fullPath.StartsWith(_workingDirectory, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"Path '{relativePath}' resolves outside the working directory sandbox.");
        return fullPath;
    }
}
