using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace ElBruno.LocalLLMs.Decisions.Internal;

internal sealed partial class KevTokenizer
{
    internal const string SplitExpression = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?[\p{L}\p{M}]+|\p{N}| ?[^\s\p{L}\p{M}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
    private readonly BpeTokenizer _bpe;
    private readonly Dictionary<string, int> _added;
    private readonly Regex _addedPattern;

    private KevTokenizer(BpeTokenizer bpe, Dictionary<string, int> added)
    {
        _bpe = bpe;
        _added = added;
        _addedPattern = new Regex(string.Join("|", added.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape)),
            RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        foreach (var token in new[] { "<|fim_prefix|>", "<|fim_middle|>", "<|box_start|>", "<|box_end|>", "<|fim_suffix|>" })
        {
            if (!added.ContainsKey(token))
                throw new DecisionException($"The Kev tokenizer is missing the '{token}' delimiter.");
        }
    }

    internal static KevTokenizer Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var pre = root.GetProperty("pre_tokenizer");
        var split = pre.GetProperty("pretokenizers")[0];
        var bytes = pre.GetProperty("pretokenizers")[1];
        if (root.GetProperty("normalizer").GetProperty("type").GetString() != "NFC" ||
            pre.GetProperty("type").GetString() != "Sequence" ||
            pre.GetProperty("pretokenizers").GetArrayLength() != 2 ||
            split.GetProperty("type").GetString() != "Split" ||
            split.GetProperty("pattern").GetProperty("Regex").GetString() != SplitExpression ||
            split.GetProperty("behavior").GetString() != "Isolated" || split.GetProperty("invert").GetBoolean() ||
            bytes.GetProperty("type").GetString() != "ByteLevel" ||
            bytes.GetProperty("use_regex").GetBoolean() || bytes.GetProperty("add_prefix_space").GetBoolean())
        {
            throw new DecisionException("Unsupported Kev tokenizer preprocessing. Use the tokenizer produced by convert_kev.py.");
        }

        var model = root.GetProperty("model");
        if (model.GetProperty("type").GetString() != "BPE" ||
            model.GetProperty("byte_fallback").GetBoolean() || model.GetProperty("ignore_merges").GetBoolean() ||
            model.GetProperty("dropout").ValueKind != JsonValueKind.Null ||
            model.GetProperty("unk_token").ValueKind != JsonValueKind.Null ||
            model.GetProperty("continuing_subword_prefix").GetString() != "" ||
            model.GetProperty("end_of_word_suffix").GetString() != "")
        {
            throw new DecisionException("Unsupported Kev BPE model. Use the pinned export tokenizer.");
        }
        var vocabulary = model.GetProperty("vocab").EnumerateObject()
            .Select(entry => new KeyValuePair<string, int>(entry.Name, entry.Value.GetInt32())).ToArray();
        var merges = model.GetProperty("merges").EnumerateArray()
            .Select(pair => pair[0].GetString() + " " + pair[1].GetString()).ToArray();
        var added = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in root.GetProperty("added_tokens").EnumerateArray())
        {
            if (token.GetProperty("lstrip").GetBoolean() || token.GetProperty("rstrip").GetBoolean() ||
                token.GetProperty("single_word").GetBoolean() || token.GetProperty("normalized").GetBoolean())
                throw new DecisionException("Unsupported Kev added-token matching flags.");
            added.Add(token.GetProperty("content").GetString()!, token.GetProperty("id").GetInt32());
        }
        return new KevTokenizer(BpeTokenizer.Create(new BpeOptions(vocabulary)
        {
            Merges = merges,
            ByteLevel = true,
            PreTokenizer = new QwenPreTokenizer(),
            UnknownToken = null,
        }), added);
    }

    internal IReadOnlyList<int> EncodeUserText(string text)
    {
        var normalized = ControlTokens().Replace(text, "<\u00a6$1\u00a6>").Normalize(NormalizationForm.FormC);
        var ids = new List<int>();
        var cursor = 0;
        foreach (Match match in _addedPattern.Matches(normalized))
        {
            ids.AddRange(_bpe.EncodeToIds(normalized[cursor..match.Index]));
            ids.Add(_added[match.Value]);
            cursor = match.Index + match.Length;
        }
        ids.AddRange(_bpe.EncodeToIds(normalized[cursor..]));
        return ids;
    }

    internal KevSequence Build(string state, string instructions, IReadOnlyList<string> options)
    {
        var ids = new List<long> { _added["<|fim_prefix|>"] };
        ids.AddRange(EncodeUserText(state).Select(id => (long)id));
        ids.Add(_added["<|fim_middle|>"]);
        ids.AddRange(EncodeUserText(instructions).Select(id => (long)id));
        var positions = new List<long>();
        foreach (var option in options)
        {
            ids.Add(_added["<|box_start|>"]);
            ids.AddRange(EncodeUserText(option).Select(id => (long)id));
            ids.Add(_added["<|box_end|>"]);
            positions.Add(ids.Count - 1);
        }
        ids.Add(_added["<|fim_suffix|>"]);
        if (ids.Count > 8192)
            throw new DecisionException($"The Kev question requires {ids.Count} tokens; this backend supports at most 8192. Shorten the state or options.");
        return new KevSequence(ids.ToArray(), positions.ToArray());
    }

    [GeneratedRegex(@"<\|([A-Za-z0-9_]+)\|>")]
    private static partial Regex ControlTokens();

    private sealed class QwenPreTokenizer : PreTokenizer
    {
        private static readonly Regex s_split = new(SplitExpression,
            RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

        public override IEnumerable<(int Offset, int Length)> PreTokenize(string? text) =>
            string.IsNullOrEmpty(text) ? [] : s_split.Matches(text).Select(match => (match.Index, match.Length));

        public override IEnumerable<(int Offset, int Length)> PreTokenize(ReadOnlySpan<char> text) => PreTokenize(text.ToString());
    }
}

internal sealed record KevSequence(long[] TokenIds, long[] OptionPositions);
