using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ElBruno.LocalLLMs.Decisions.Internal;

namespace ElBruno.LocalLLMs.Decisions.Tests;

/// <summary>
/// Builds a minimal but genuine byte-level <c>tokenizer.json</c> so the sequence builder can be
/// tested without downloading a real 3 MB checkpoint tokenizer.
/// </summary>
/// <remarks>
/// The vocabulary covers every byte-level character and defines no merges, so each byte becomes
/// one token. That makes token counts predictable while still exercising the real
/// <see cref="LayaTokenizer"/> code path, including byte-level mapping and the added vocabulary.
/// </remarks>
internal static class SyntheticTokenizer
{
    public const int UnknownId = 256;
    public const int ClsId = 257;
    public const int SepId = 258;
    public const int PadId = 259;
    public const int MaskId = 260;

    /// <summary>
    /// Writes a synthetic tokenizer definition and loads it.
    /// </summary>
    /// <param name="directory">The directory to write the definition into.</param>
    /// <returns>A loaded tokenizer.</returns>
    public static LayaTokenizer Create(string directory)
    {
        var path = Path.Combine(directory, $"tokenizer-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, BuildJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return LayaTokenizer.Load(path);
    }

    private static string BuildJson()
    {
        var vocabulary = new JsonObject();
        var alphabet = ByteLevelAlphabet();
        for (var i = 0; i < alphabet.Length; i++)
        {
            vocabulary[alphabet[i].ToString()] = i;
        }

        vocabulary["[UNK]"] = UnknownId;
        vocabulary["[CLS]"] = ClsId;
        vocabulary["[SEP]"] = SepId;
        vocabulary["[PAD]"] = PadId;
        vocabulary["[MASK]"] = MaskId;

        var added = new JsonArray();
        foreach (var (content, id, lstrip) in new[]
                 {
                     ("[UNK]", UnknownId, false),
                     ("[CLS]", ClsId, false),
                     ("[SEP]", SepId, false),
                     ("[PAD]", PadId, false),
                     // The real checkpoint marks [MASK] lstrip, so a preceding space is absorbed.
                     ("[MASK]", MaskId, true),
                 })
        {
            added.Add(new JsonObject
            {
                ["id"] = id,
                ["content"] = content,
                ["lstrip"] = lstrip,
                ["rstrip"] = false,
                ["single_word"] = false,
                ["normalized"] = false,
                ["special"] = true,
            });
        }

        var root = new JsonObject
        {
            ["version"] = "1.0",
            ["added_tokens"] = added,
            ["model"] = new JsonObject
            {
                ["type"] = "BPE",
                ["vocab"] = vocabulary,
                ["merges"] = new JsonArray(),
            },
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static char[] ByteLevelAlphabet()
    {
        var bytes = new List<int>();
        for (var c = '!'; c <= '~'; c++) bytes.Add(c);
        for (var c = '\u00a1'; c <= '\u00ac'; c++) bytes.Add(c);
        for (var c = '\u00ae'; c <= '\u00ff'; c++) bytes.Add(c);

        var characters = new List<int>(bytes);
        var next = 0;
        for (var b = 0; b < 256; b++)
        {
            if (!bytes.Contains(b))
            {
                bytes.Add(b);
                characters.Add(256 + next);
                next++;
            }
        }

        // Order by byte value so the vocabulary ids line up with the bytes they encode.
        return bytes
            .Select((value, index) => (Byte: value, Character: characters[index]))
            .OrderBy(pair => pair.Byte)
            .Select(pair => (char)pair.Character)
            .ToArray();
    }
}
