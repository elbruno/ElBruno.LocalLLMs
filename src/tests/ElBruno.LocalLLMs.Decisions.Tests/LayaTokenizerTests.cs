using ElBruno.LocalLLMs.Decisions.Internal;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class LayaTokenizerTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("laya-tokenizer-tests").FullName;

    private readonly LayaTokenizer _tokenizer;

    public LayaTokenizerTests()
    {
        _tokenizer = SyntheticTokenizer.Create(_directory);
    }

    [Fact]
    public void Load_ExposesTheSpecialTokensTheSequenceFormatNeeds()
    {
        Assert.Equal(SyntheticTokenizer.ClsId, _tokenizer.ClsTokenId);
        Assert.Equal(SyntheticTokenizer.SepId, _tokenizer.SepTokenId);
        Assert.Equal(SyntheticTokenizer.MaskId, _tokenizer.MaskTokenId);
        Assert.Equal(SyntheticTokenizer.PadId, _tokenizer.PadTokenId);
    }

    [Fact]
    public void Encode_ReturnsNothingForEmptyText()
    {
        Assert.Empty(_tokenizer.Encode(string.Empty));
    }

    [Fact]
    public void Encode_IsStableForTheSameInput()
    {
        Assert.Equal(_tokenizer.Encode("hello world"), _tokenizer.Encode("hello world"));
    }

    [Fact]
    public void Encode_DistinguishesALeadingSpace()
    {
        // ByteLevel pre-tokenization keeps the space as part of the following token, which is the
        // single most common way a hand-rolled port diverges from the Python reference.
        Assert.NotEqual(_tokenizer.Encode("world"), _tokenizer.Encode(" world"));
    }

    [Fact]
    public void Encode_RecognisesAddedTokensAsSingleIds()
    {
        IReadOnlyList<int> ids = _tokenizer.Encode("[MASK]");

        Assert.Equal([SyntheticTokenizer.MaskId], ids);
    }

    [Fact]
    public void Encode_AbsorbsTheSpaceBeforeAnLStripAddedToken()
    {
        // [MASK] ships with lstrip=true, so the preceding space belongs to the token.
        IReadOnlyList<int> withSpace = _tokenizer.Encode("a [MASK]");
        IReadOnlyList<int> withoutSpace = _tokenizer.Encode("a[MASK]");

        Assert.Equal(withoutSpace, withSpace);
    }

    [Fact]
    public void Encode_NormalizesToCompositeForm()
    {
        // "é" as a single code point and as "e" plus a combining accent must encode identically.
        Assert.Equal(_tokenizer.Encode("caf\u00e9"), _tokenizer.Encode("cafe\u0301"));
    }

    [Fact]
    public void Encode_HandlesTextOutsideTheBasicLatinRange()
    {
        Assert.NotEmpty(_tokenizer.Encode("наши данные 日本語 🎉"));
    }

    [Fact]
    public void EncodeTruncated_KeepsAtMostTheRequestedTokens()
    {
        IReadOnlyList<int> ids = _tokenizer.EncodeTruncated("a fairly long piece of option text", 5);

        Assert.Equal(5, ids.Count);
        Assert.Equal(_tokenizer.Encode("a fairly long piece of option text").Take(5), ids);
    }

    [Fact]
    public void EncodeTruncated_LeavesShortTextUntouched()
    {
        Assert.Equal(_tokenizer.Encode("short"), _tokenizer.EncodeTruncated("short", 64));
    }

    [Fact]
    public void Load_RejectsATokenizerMissingLayasSpecialTokens()
    {
        var path = Path.Combine(_directory, "incomplete.json");
        File.WriteAllText(path,
            """
            {"added_tokens":[],"model":{"type":"BPE","vocab":{"a":0},"merges":[]}}
            """);

        var exception = Assert.Throws<DecisionException>(() => LayaTokenizer.Load(path));
        Assert.Contains("[CLS]", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}
