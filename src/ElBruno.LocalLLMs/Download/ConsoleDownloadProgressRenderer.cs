namespace ElBruno.LocalLLMs;

/// <summary>
/// Renders model download progress with throttling for interactive and redirected console output.
/// </summary>
public sealed class ConsoleDownloadProgressRenderer
{
    private readonly bool _isInteractive;
    private readonly TimeSpan _minimumInterval;

    private DateTimeOffset _lastRenderAt = DateTimeOffset.MinValue;
    private int _lastInteractiveBucket = -1;
    private int _lastNonInteractiveBucket = -1;
    private string _lastFileName = string.Empty;
    private bool _hasInPlaceOutput;

    /// <summary>
    /// Creates a renderer for console download progress output.
    /// </summary>
    /// <param name="isInteractive">True when output supports in-place updates.</param>
    /// <param name="minimumInterval">Minimum time between rendered updates.</param>
    public ConsoleDownloadProgressRenderer(bool isInteractive, TimeSpan? minimumInterval = null)
    {
        _isInteractive = isInteractive;
        _minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(125);
    }

    /// <summary>
    /// True when an interactive in-place line was rendered and a trailing newline should be printed after completion.
    /// </summary>
    public bool NeedsFinalNewLine => _isInteractive && _hasInPlaceOutput;

    /// <summary>
    /// Returns a progress line when the update should be rendered, otherwise null.
    /// </summary>
    public ConsoleDownloadProgressUpdate? BuildUpdate(ModelDownloadProgress progress, DateTimeOffset now)
    {
        var percent = NormalizePercent(progress.PercentComplete);
        var isComplete = percent >= 100.0;
        var fileName = ShortenFileName(progress.FileName, maxLength: 30);
        var fileChanged = !string.Equals(_lastFileName, fileName, StringComparison.Ordinal);

        if (_isInteractive)
        {
            var interactiveBucket = (int)Math.Floor(percent * 2.0); // 0.5% increments
            var hasIntervalElapsed = now - _lastRenderAt >= _minimumInterval;

            if (!isComplete && !fileChanged && (!hasIntervalElapsed || interactiveBucket == _lastInteractiveBucket))
            {
                return null;
            }

            var filled = Math.Clamp((int)Math.Round(percent / 100.0 * 30), 0, 30);
            var bar = new string('#', filled);
            var empty = new string('-', 30 - filled);
            var line = $"  Downloading [{bar}{empty}] {percent,6:F1}% {fileName}";

            _lastRenderAt = now;
            _lastInteractiveBucket = interactiveBucket;
            _lastFileName = fileName;
            _hasInPlaceOutput = true;

            return new ConsoleDownloadProgressUpdate(line, InPlace: true);
        }

        var nonInteractiveBucket = (int)Math.Floor(percent / 10.0);
        if (!isComplete && !fileChanged && nonInteractiveBucket == _lastNonInteractiveBucket)
        {
            return null;
        }

        var conciseLine = $"  Downloading {percent,6:F1}% {fileName}";
        _lastRenderAt = now;
        _lastNonInteractiveBucket = nonInteractiveBucket;
        _lastFileName = fileName;

        return new ConsoleDownloadProgressUpdate(conciseLine, InPlace: false);
    }

    private static double NormalizePercent(double rawPercent)
    {
        var value = rawPercent <= 1.0 ? rawPercent * 100.0 : rawPercent;
        return Math.Clamp(value, 0.0, 100.0);
    }

    private static string ShortenFileName(string? fileName, int maxLength)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        if (safeName.Length <= maxLength) return safeName;
        return safeName[..(maxLength - 3)] + "...";
    }
}

/// <summary>
/// A rendered progress update line and how it should be written to the console.
/// </summary>
public readonly record struct ConsoleDownloadProgressUpdate(string Text, bool InPlace);