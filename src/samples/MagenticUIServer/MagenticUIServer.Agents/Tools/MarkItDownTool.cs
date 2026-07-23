using System.ComponentModel;
using ElBruno.MarkItDotNet;

namespace MagenticUIServer.Agents.Tools;

/// <summary>
/// Converts documents (PDF, DOCX, XLSX, PPTX, HTML, etc.) to Markdown
/// using ElBruno.MarkItDotNet v0.9.x. API: await converter.ConvertAsync(filePath).
/// </summary>
public sealed class MarkItDownTool
{
    private readonly IMarkdownConverter _converter;

    public MarkItDownTool(IMarkdownConverter converter)
    {
        _converter = converter;
    }

    [Description("Converts a document file (PDF, DOCX, XLSX, PPTX, HTML, etc.) to Markdown text.")]
    public async Task<string> ConvertToMarkdown(
        [Description("Absolute or relative path to the document file")] string filePath)
    {
        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";

        try
        {
            var markdown = await _converter.ConvertAsync(filePath);
            return markdown ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Fallback: read raw text if conversion fails
            try
            {
                return File.ReadAllText(filePath);
            }
            catch
            {
                return $"Error converting file '{filePath}': {ex.Message}";
            }
        }
    }
}
