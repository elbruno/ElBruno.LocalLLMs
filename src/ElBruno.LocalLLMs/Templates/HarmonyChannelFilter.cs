using System.Text;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Extracts user-facing text from a Harmony (GPT-OSS) response stream.
///
/// GPT-OSS emits its reasoning on the <c>analysis</c> channel and its answer on the
/// <c>final</c> channel:
/// <code>
/// &lt;|channel|&gt;analysis&lt;|message|&gt;The user is asking...&lt;|end|&gt;
/// &lt;|start|&gt;assistant&lt;|channel|&gt;final&lt;|message|&gt;Paris.&lt;|return|&gt;
/// </code>
///
/// The model card is explicit that chain-of-thought "is not intended to be shown to
/// end users", so only the <c>final</c> channel is surfaced. The <c>commentary</c>
/// channel carries tool calls and is handled separately by
/// <see cref="ToolCalling.HarmonyToolCallParser"/>.
///
/// The filter is stateful and byte-stream safe: markers may be split across arbitrary
/// token boundaries, so partial markers are held back until they can be resolved.
/// </summary>
internal sealed class HarmonyChannelFilter
{
    private const string StartMarker = "<|start|>";
    private const string ChannelMarker = "<|channel|>";
    private const string MessageMarker = "<|message|>";
    private const string EndMarker = "<|end|>";
    private const string ReturnMarker = "<|return|>";
    private const string CallMarker = "<|call|>";

    /// <summary>Longest control marker; bounds how much text must be buffered.</summary>
    private static readonly int MaxMarkerLength =
        new[] { StartMarker, ChannelMarker, MessageMarker, EndMarker, ReturnMarker, CallMarker }
            .Max(m => m.Length);

    private readonly StringBuilder _pending = new();
    private bool _inFinalMessage;
    private bool _sawAnyChannel;

    /// <summary>
    /// Feeds the next chunk of raw model output and returns any user-facing text
    /// that can be emitted now. Returns an empty string when nothing is ready.
    /// </summary>
    public string Push(string? chunk)
    {
        if (!string.IsNullOrEmpty(chunk))
        {
            _pending.Append(chunk);
        }

        return Drain(flush: false);
    }

    /// <summary>
    /// Signals end of stream and returns any remaining user-facing text.
    /// </summary>
    public string Flush() => Drain(flush: true);

    private string Drain(bool flush)
    {
        var output = new StringBuilder();

        while (_pending.Length > 0)
        {
            var buffer = _pending.ToString();

            if (_inFinalMessage)
            {
                // Inside the final channel: emit text until a terminator appears.
                var terminator = IndexOfAny(buffer, out var matchedLength, EndMarker, ReturnMarker, StartMarker);
                if (terminator >= 0)
                {
                    output.Append(buffer, 0, terminator);
                    _pending.Remove(0, terminator + matchedLength);
                    _inFinalMessage = false;
                    continue;
                }

                // No terminator yet. Emit everything except a possible partial marker tail.
                var safe = SafeEmitLength(buffer, flush);
                if (safe <= 0)
                {
                    break;
                }

                output.Append(buffer, 0, safe);
                _pending.Remove(0, safe);
                continue;
            }

            // Outside the final channel: look for the next channel declaration.
            var channelIndex = buffer.IndexOf(ChannelMarker, StringComparison.Ordinal);
            if (channelIndex < 0)
            {
                if (flush)
                {
                    // No channel markers at all — treat the whole output as plain text.
                    // This keeps the filter safe if the model or a stub emits raw text.
                    if (!_sawAnyChannel)
                    {
                        output.Append(StripControlMarkers(buffer));
                    }

                    _pending.Clear();
                }

                // Otherwise retain the buffer: a channel marker may still be arriving,
                // and text before the first channel may yet turn out to be plain output.
                break;
            }

            var afterChannel = channelIndex + ChannelMarker.Length;
            var messageIndex = buffer.IndexOf(MessageMarker, afterChannel, StringComparison.Ordinal);
            if (messageIndex < 0)
            {
                if (flush)
                {
                    _pending.Clear();
                }
                else
                {
                    // Channel name is still arriving; drop everything before it and wait.
                    _pending.Remove(0, channelIndex);
                }

                break;
            }

            _sawAnyChannel = true;

            // The channel name may carry a suffix (e.g. "commentary json").
            var channelName = buffer[afterChannel..messageIndex].Trim();
            var isFinal = channelName.StartsWith("final", StringComparison.OrdinalIgnoreCase);

            _pending.Remove(0, messageIndex + MessageMarker.Length);
            _inFinalMessage = isFinal;
        }

        return output.ToString();
    }

    /// <summary>
    /// How many characters can be emitted without risking splitting a control marker.
    /// </summary>
    private static int SafeEmitLength(string buffer, bool flush)
    {
        if (flush)
        {
            return buffer.Length;
        }

        // Hold back a tail that could be the beginning of a marker.
        var maxTail = Math.Min(buffer.Length, MaxMarkerLength - 1);
        for (var tail = maxTail; tail > 0; tail--)
        {
            if (buffer[^tail] == '<' && CouldStartMarker(buffer[^tail..]))
            {
                return buffer.Length - tail;
            }
        }

        return buffer.Length;
    }

    private static bool CouldStartMarker(string candidate)
    {
        foreach (var marker in new[] { StartMarker, ChannelMarker, MessageMarker, EndMarker, ReturnMarker, CallMarker })
        {
            if (marker.StartsWith(candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfAny(string buffer, out int matchedLength, params string[] markers)
    {
        var best = -1;
        matchedLength = 0;

        foreach (var marker in markers)
        {
            var index = buffer.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
                matchedLength = marker.Length;
            }
        }

        return best;
    }

    private static string StripControlMarkers(string text)
    {
        foreach (var marker in new[] { StartMarker, ChannelMarker, MessageMarker, EndMarker, ReturnMarker, CallMarker })
        {
            text = text.Replace(marker, string.Empty, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// Extracts the user-facing text from a complete Harmony response in one pass.
    /// </summary>
    public static string ExtractFinal(string? responseText)
    {
        if (string.IsNullOrEmpty(responseText))
        {
            return string.Empty;
        }

        var filter = new HarmonyChannelFilter();
        var result = filter.Push(responseText) + filter.Flush();
        return result;
    }
}
