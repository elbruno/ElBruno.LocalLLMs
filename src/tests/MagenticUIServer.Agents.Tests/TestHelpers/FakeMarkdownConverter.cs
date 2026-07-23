using ElBruno.MarkItDotNet;

namespace MagenticUIServer.Agents.Tests.TestHelpers;

/// <summary>
/// Stub IMarkdownConverter for unit tests. Returns a configurable markdown string.
/// </summary>
internal sealed class FakeMarkdownConverter : IMarkdownConverter
{
    private readonly string _result;

    public FakeMarkdownConverter(string result = "## Converted Markdown") => _result = result;

    public bool CanHandle(string fileExtension) => true;

    public Task<string> ConvertAsync(Stream fileStream, string fileExtension,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}
