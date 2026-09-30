using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// Splits text the way HuggingFace's ByteLevel pre-tokenizer does, which is what Laya's
/// checkpoints were tokenized with.
/// </summary>
/// <remarks>
/// The pattern keeps a leading space attached to the word that follows it. That detail is not
/// cosmetic: "world" and " world" are different tokens in a byte-level vocabulary, so dropping
/// the space shifts every id after it and produces a different answer from the model.
/// </remarks>
internal sealed partial class ByteLevelPreTokenizer : PreTokenizer
{
    internal static ByteLevelPreTokenizer Instance { get; } = new();

    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+")]
    private static partial Regex SplitPattern();

    public override IEnumerable<(int Offset, int Length)> PreTokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match match in SplitPattern().Matches(text))
        {
            if (match.Length > 0)
            {
                yield return (match.Index, match.Length);
            }
        }
    }

    public override IEnumerable<(int Offset, int Length)> PreTokenize(ReadOnlySpan<char> text)
        => PreTokenize(text.ToString());
}
