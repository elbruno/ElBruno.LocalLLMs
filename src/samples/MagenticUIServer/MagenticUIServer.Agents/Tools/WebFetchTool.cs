using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ElBruno.MarkItDotNet;

namespace MagenticUIServer.Agents.Tools;

/// <summary>
/// Fetches web pages and optionally converts HTML to Markdown.
/// Pass an IMarkdownConverter to enable Markdown conversion; otherwise raw text is returned.
/// </summary>
public sealed class WebFetchTool
{
    private readonly HttpClient _httpClient;
    private readonly IMarkdownConverter? _markdownConverter;

    public WebFetchTool(HttpClient httpClient, IMarkdownConverter? markdownConverter = null)
    {
        _httpClient = httpClient;
        _markdownConverter = markdownConverter;
    }

    [Description("Fetches the content of a URL. Returns Markdown if the page is HTML and a converter is configured, otherwise plain text (truncated to 8000 characters).")]
    public async Task<string> FetchUrl(
        [Description("The fully-qualified URL to fetch (must start with http:// or https://)")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return $"Error: invalid URL '{url}'. Must start with http:// or https://.";

        try
        {
            using var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var content = await response.Content.ReadAsStringAsync();

            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                _markdownConverter is not null)
            {
                // TODO: IMarkdownConverter.ConvertAsync expects a file path; for URL content
                // we write to a temp file and convert. Phase 3B: use MarkdownService.ConvertUrlAsync.
                var markdown = await _markdownConverter.ConvertAsync(url);
                return markdown.Length > 8000 ? markdown[..8000] + "\n...[truncated]" : markdown;
            }

            // Plain text or no converter — strip HTML tags and truncate
            var text = IsHtml(contentType)
                ? StripHtmlTags(content)
                : content;

            return text.Length > 8000 ? text[..8000] + "\n...[truncated]" : text;
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching '{url}': {ex.Message}";
        }
    }

    private static bool IsHtml(string contentType) =>
        contentType.Contains("html", StringComparison.OrdinalIgnoreCase);

    private static string StripHtmlTags(string html) =>
        Regex.Replace(html, "<[^>]*>", " ", RegexOptions.Compiled)
             .Replace("&amp;", "&")
             .Replace("&lt;", "<")
             .Replace("&gt;", ">")
             .Replace("&nbsp;", " ")
             .Replace("&quot;", "\"");
}
