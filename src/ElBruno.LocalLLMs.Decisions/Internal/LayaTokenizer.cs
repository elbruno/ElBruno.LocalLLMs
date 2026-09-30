using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// A token that HuggingFace keeps out of the BPE merge process and matches against the raw text.
/// </summary>
/// <param name="Content">The literal text that identifies the token.</param>
/// <param name="Id">The vocabulary id emitted when the content matches.</param>
/// <param name="LStrip">Whether whitespace immediately before the content is absorbed into the token.</param>
/// <param name="RStrip">Whether whitespace immediately after the content is absorbed into the token.</param>
internal sealed record LayaAddedToken(string Content, int Id, bool LStrip, bool RStrip);

/// <summary>
/// Reproduces the HuggingFace fast tokenizer that Laya's English checkpoint ships, reading the
/// checkpoint's own <c>tokenizer.json</c> so the vocabulary and merges are never duplicated here.
/// </summary>
/// <remarks>
/// <para>
/// Token ids must match the Python reference exactly. The model scores the hidden state at each
/// <c>[MASK]</c> marker position, so a single extra or missing token moves every marker after it
/// and the answer silently changes rather than failing.
/// </para>
/// <para>
/// Three pieces have to line up: NFC normalization, ByteLevel pre-tokenization, and the added
/// vocabulary, which is matched against the text before BPE runs.
/// </para>
/// </remarks>
internal sealed class LayaTokenizer
{
    private readonly BpeTokenizer _bpe;
    private readonly Regex _addedPattern;
    private readonly Dictionary<string, int> _idsByContent;

    private LayaTokenizer(BpeTokenizer bpe, IReadOnlyList<LayaAddedToken> addedTokens)
    {
        _bpe = bpe;
        _idsByContent = addedTokens
            .GroupBy(token => token.Content, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        _addedPattern = BuildAddedPattern(addedTokens);

        ClsTokenId = RequireToken("[CLS]");
        SepTokenId = RequireToken("[SEP]");
        MaskTokenId = RequireToken("[MASK]");
        PadTokenId = RequireToken("[PAD]");
    }

    /// <summary>Gets the id of the <c>[CLS]</c> token that opens every sequence.</summary>
    public int ClsTokenId { get; }

    /// <summary>Gets the id of the <c>[SEP]</c> token that separates the sequence sections.</summary>
    public int SepTokenId { get; }

    /// <summary>Gets the id of the <c>[MASK]</c> token that marks a scorable option.</summary>
    public int MaskTokenId { get; }

    /// <summary>Gets the id of the <c>[PAD]</c> token used to square off a batch.</summary>
    public int PadTokenId { get; }

    /// <summary>Gets the literal <c>[MASK]</c> text, which is stripped from caller-supplied text.</summary>
    public const string MaskToken = "[MASK]";

    /// <summary>
    /// Loads a tokenizer from a checkpoint's <c>tokenizer.json</c>.
    /// </summary>
    /// <param name="tokenizerJsonPath">Full path to the tokenizer definition.</param>
    /// <returns>A tokenizer that reproduces the checkpoint's Python behaviour.</returns>
    public static LayaTokenizer Load(string tokenizerJsonPath)
    {
        // Read as text rather than bytes so a byte-order mark, which some tooling adds when
        // re-saving a tokenizer, does not fail the parse.
        using var document = JsonDocument.Parse(File.ReadAllText(tokenizerJsonPath));
        var root = document.RootElement;
        var model = root.GetProperty("model");

        var vocabulary = model.GetProperty("vocab")
            .EnumerateObject()
            .Select(entry => new KeyValuePair<string, int>(entry.Name, entry.Value.GetInt32()))
            .ToList();

        var merges = model.GetProperty("merges")
            .EnumerateArray()
            .Select(pair => pair[0].GetString() + " " + pair[1].GetString())
            .ToList();

        var addedTokens = root.GetProperty("added_tokens")
            .EnumerateArray()
            .Select(token => new LayaAddedToken(
                token.GetProperty("content").GetString()!,
                token.GetProperty("id").GetInt32(),
                token.GetProperty("lstrip").GetBoolean(),
                token.GetProperty("rstrip").GetBoolean()))
            .ToList();

        var bpe = BpeTokenizer.Create(new BpeOptions(vocabulary)
        {
            Merges = merges,
            ByteLevel = true,
            PreTokenizer = ByteLevelPreTokenizer.Instance,
            UnknownToken = null,
        });

        return new LayaTokenizer(bpe, addedTokens);
    }

    /// <summary>
    /// Encodes text to token ids, without adding the surrounding special tokens.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>The token ids, in order.</returns>
    public IReadOnlyList<int> Encode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var normalized = text.Normalize(NormalizationForm.FormC);
        var ids = new List<int>();
        var cursor = 0;

        foreach (Match match in _addedPattern.Matches(normalized))
        {
            if (match.Index > cursor)
            {
                ids.AddRange(_bpe.EncodeToIds(normalized[cursor..match.Index]));
            }

            ids.Add(_idsByContent[match.Groups["content"].Value]);
            cursor = match.Index + match.Length;
        }

        if (cursor < normalized.Length)
        {
            ids.AddRange(_bpe.EncodeToIds(normalized[cursor..]));
        }

        return ids;
    }

    /// <summary>
    /// Encodes text and keeps at most <paramref name="maxTokens"/> tokens from the start.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="maxTokens">The inclusive token ceiling.</param>
    /// <returns>The truncated token ids.</returns>
    public IReadOnlyList<int> EncodeTruncated(string text, int maxTokens)
    {
        var ids = Encode(text);
        return ids.Count <= maxTokens ? ids : ids.Take(maxTokens).ToArray();
    }

    private int RequireToken(string content) =>
        _idsByContent.TryGetValue(content, out var id)
            ? id
            : throw new DecisionException(
                $"The tokenizer is missing the '{content}' token, so it cannot be a Laya checkpoint. " +
                "Check that the model directory holds the tokenizer that shipped with the model.");

    private static Regex BuildAddedPattern(IReadOnlyList<LayaAddedToken> addedTokens)
    {
        // Longest content first: the added vocabulary contains overlapping whitespace runs, and
        // regex alternation is first-match-wins rather than longest-match-wins.
        var alternatives = addedTokens
            .OrderByDescending(token => token.Content.Length)
            .Select(token =>
            {
                var pattern = "(?<content>" + Regex.Escape(token.Content) + ")";
                if (token.LStrip)
                {
                    pattern = @"\s*" + pattern;
                }

                if (token.RStrip)
                {
                    pattern += @"\s*";
                }

                return pattern;
            });

        return new Regex(string.Join("|", alternatives), RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}
