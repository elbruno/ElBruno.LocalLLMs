using System.Net;
using System.Text;
using MagenticUIServer.Agents.Tests.TestHelpers;
using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Tests.Tools;

public sealed class WebFetchToolTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static WebFetchTool CreateTool(HttpResponseMessage response) =>
        new(new HttpClient(new TestHttpMessageHandler(response)));

    private static HttpResponseMessage HtmlResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><body>{body}</body></html>",
                Encoding.UTF8,
                "text/html")
        };

    private static HttpResponseMessage PlainResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };

    // ── Basic fetch ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchUrl_ValidHtml_ReturnsContent()
    {
        var sut = CreateTool(HtmlResponse("Hello World"));

        var result = await sut.FetchUrl("https://example.com");

        Assert.Contains("Hello World", result);
    }

    [Fact]
    public async Task FetchUrl_ValidHtml_WithoutMarkdownConverter_ReturnsStrippedText()
    {
        var sut = CreateTool(HtmlResponse("<strong>bold</strong> text"));

        var result = await sut.FetchUrl("https://example.com");

        // HTML tags are stripped; plain text content remains
        Assert.DoesNotContain("<strong>", result);
        Assert.Contains("bold", result);
        Assert.Contains("text", result);
    }

    [Fact]
    public async Task FetchUrl_NonHtmlContent_ReturnsRaw()
    {
        var sut = CreateTool(PlainResponse("raw text content"));

        var result = await sut.FetchUrl("https://example.com/data.txt");

        Assert.Equal("raw text content", result);
    }

    // ── Truncation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchUrl_ContentTruncatedAt8000Chars()
    {
        var bigBody = new string('A', 9000);
        var sut = CreateTool(PlainResponse(bigBody));

        var result = await sut.FetchUrl("https://example.com");

        Assert.True(result.Length <= 8100, $"Expected truncated output, got {result.Length}");
        Assert.Contains("[truncated]", result);
    }

    // ── URL validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task FetchUrl_InvalidUrl_ReturnsErrorMessage()
    {
        // FetchUrl returns an error string for invalid URLs — it does not throw.
        var sut = new WebFetchTool(new HttpClient());

        var result = await sut.FetchUrl("not-a-url");

        Assert.StartsWith("Error:", result);
        Assert.Contains("not-a-url", result);
    }

    [Fact]
    public async Task FetchUrl_EmptyUrl_ReturnsErrorMessage()
    {
        var sut = new WebFetchTool(new HttpClient());

        var result = await sut.FetchUrl(string.Empty);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task FetchUrl_RelativeUrl_ReturnsErrorMessage()
    {
        var sut = new WebFetchTool(new HttpClient());

        var result = await sut.FetchUrl("/relative/path");

        Assert.StartsWith("Error:", result);
    }

    // ── HTTP errors ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchUrl_HttpError_ReturnsErrorMessage()
    {
        // FetchUrl catches HttpRequestException and returns an error string.
        var errorResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var sut = CreateTool(errorResponse);

        var result = await sut.FetchUrl("https://example.com/missing");

        Assert.StartsWith("Error", result);
    }

    // ── HTML entity decoding ────────────────────────────────────────────────

    [Fact]
    public async Task FetchUrl_HtmlEntities_AreDecoded()
    {
        var sut = CreateTool(HtmlResponse("a &amp; b &lt;tag&gt;"));

        var result = await sut.FetchUrl("https://example.com");

        Assert.Contains("a & b", result);
        Assert.Contains("<tag>", result);
    }
}
