using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Tests.Tools;

public sealed class FileSurferToolTests : IDisposable
{
    private readonly string _sandbox;
    private readonly FileSurferTool _sut;

    public FileSurferToolTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_sandbox);
        _sut = new FileSurferTool(_sandbox);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
            Directory.Delete(_sandbox, recursive: true);
    }

    // ── ListDirectory ──────────────────────────────────────────────────────

    [Fact]
    public void ListDirectory_ReturnsRootFiles()
    {
        File.WriteAllText(Path.Combine(_sandbox, "alpha.txt"), "a");
        File.WriteAllText(Path.Combine(_sandbox, "beta.txt"), "b");

        var result = _sut.ListDirectory();

        Assert.Contains("alpha.txt", result);
        Assert.Contains("beta.txt", result);
    }

    [Fact]
    public void ListDirectory_EmptyDirectory_ReturnsEmptyString()
    {
        var result = _sut.ListDirectory();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ListDirectory_SubDirectory_ReturnsCorrectFiles()
    {
        var sub = Path.Combine(_sandbox, "subdir");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "child.txt"), "c");

        var result = _sut.ListDirectory("subdir");

        Assert.Contains("child.txt", result);
    }

    [Fact]
    public void ListDirectory_DirectoriesHaveTrailingSlash()
    {
        Directory.CreateDirectory(Path.Combine(_sandbox, "mydir"));

        var result = _sut.ListDirectory();

        Assert.Contains("mydir/", result);
    }

    [Fact]
    public void ListDirectory_NonExistentSubDir_ReturnsErrorMessage()
    {
        var result = _sut.ListDirectory("doesnotexist");

        Assert.StartsWith("Error:", result);
    }

    // ── ReadFile ────────────────────────────────────────────────────────────

    [Fact]
    public void ReadFile_ReturnsContent()
    {
        File.WriteAllText(Path.Combine(_sandbox, "hello.txt"), "world");

        var result = _sut.ReadFile("hello.txt");

        Assert.Equal("world", result);
    }

    [Fact]
    public void ReadFile_EmptyFile_ReturnsEmptyString()
    {
        File.WriteAllText(Path.Combine(_sandbox, "empty.txt"), string.Empty);

        var result = _sut.ReadFile("empty.txt");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReadFile_NonExistentFile_ReturnsErrorMessage()
    {
        var result = _sut.ReadFile("nosuchfile.txt");

        Assert.StartsWith("Error:", result);
        Assert.Contains("nosuchfile.txt", result);
    }

    [Fact]
    public void ReadFile_LargeFile_TruncatesAt8000Chars()
    {
        var bigContent = new string('X', 9000);
        File.WriteAllText(Path.Combine(_sandbox, "big.txt"), bigContent);

        var result = _sut.ReadFile("big.txt");

        Assert.True(result.Length <= 8100, $"Expected truncated output, got length {result.Length}");
        Assert.Contains("[truncated]", result);
    }

    [Fact]
    public void ReadFile_OutsideSandbox_ThrowsUnauthorizedAccess()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _sut.ReadFile("../../etc/passwd"));
    }

    // ── WriteFile ───────────────────────────────────────────────────────────

    [Fact]
    public void WriteFile_CreatesFile()
    {
        _sut.WriteFile("newfile.txt", "content");

        Assert.True(File.Exists(Path.Combine(_sandbox, "newfile.txt")));
    }

    [Fact]
    public void WriteFile_ThenRead_RoundTrip()
    {
        _sut.WriteFile("roundtrip.txt", "round-trip value");

        var result = _sut.ReadFile("roundtrip.txt");

        Assert.Equal("round-trip value", result);
    }

    [Fact]
    public void WriteFile_CreatesSubdirectoryIfNeeded()
    {
        _sut.WriteFile("subdir/nested.txt", "nested content");

        Assert.True(File.Exists(Path.Combine(_sandbox, "subdir", "nested.txt")));
    }

    // ── Sandbox enforcement ─────────────────────────────────────────────────

    [Fact]
    public void FileSurferTool_SandboxPath_CannotEscapeWithDotDot()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _sut.ReadFile("../outside.txt"));
    }

    [Fact]
    public void FileSurferTool_SandboxPath_CannotEscapeWithAbsolutePath()
    {
        // An absolute path that resolves outside the sandbox must be rejected.
        var outsidePath = Path.GetTempPath(); // parent of the sandbox
        Assert.Throws<UnauthorizedAccessException>(() =>
            _sut.ReadFile(outsidePath));
    }
}
