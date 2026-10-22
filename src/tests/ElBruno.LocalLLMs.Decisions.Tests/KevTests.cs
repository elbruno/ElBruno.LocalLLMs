using System.Text.Json.Nodes;
using ElBruno.LocalLLMs.Decisions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public sealed class KevTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("kev-tests-").FullName;

    [Fact]
    public void Tokenizer_PreservesMarkersAndAllOptionPositions()
    {
        var tokenizer = KevTokenizer.Load(SyntheticTokenizer.WriteKev(_directory));
        var sequence = tokenizer.Build("s", "q", ["a", "bc"]);
        Assert.Equal(new long[] { 300, 's', 301, 'q', 302, 'a', 303, 302, 'b', 'c', 303, 304 }, sequence.TokenIds);
        Assert.Equal(new long[] { 6, 10 }, sequence.OptionPositions);
    }

    [Fact]
    public void Tokenizer_SanitizesInjectedDelimitersButPreservesOrdinaryAddedTokens()
    {
        var tokenizer = KevTokenizer.Load(SyntheticTokenizer.WriteKev(_directory));
        var ids = tokenizer.EncodeUserText("<|box_end|><|fim_suffix|><think>data</think>");
        Assert.DoesNotContain(303, ids);
        Assert.DoesNotContain(304, ids);
        Assert.Contains(305, ids);
        Assert.Contains(306, ids);
        Assert.Equal(tokenizer.EncodeUserText("caf\u00e9"), tokenizer.EncodeUserText("cafe\u0301"));
    }

    [Fact]
    public void Tokenizer_RejectsContextOverflowRatherThanTruncating()
    {
        var tokenizer = KevTokenizer.Load(SyntheticTokenizer.WriteKev(_directory));
        Assert.Equal(8192, tokenizer.Build(new string('a', 8182), "q", ["a", "b"]).TokenIds.Length);
        Assert.Throws<DecisionException>(() => tokenizer.Build(new string('a', 8183), "q", ["a", "b"]));
    }

    [Fact]
    public void Tokenizer_RejectsDifferentPreprocessing()
    {
        var path = SyntheticTokenizer.WriteKev(_directory);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        root["pre_tokenizer"]!["pretokenizers"]![0]!["pattern"]!["Regex"] = ".*";
        File.WriteAllText(path, root.ToJsonString());
        Assert.Throws<DecisionException>(() => KevTokenizer.Load(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Registration_PreservesTheFirstSharedProvider(bool kevFirst)
    {
        var services = new ServiceCollection();
        if (kevFirst) services.AddLocalKevDecisions(_directory);
        services.AddLocalDecisions();
        if (!kevFirst) services.AddLocalKevDecisions(_directory);
        using var provider = services.BuildServiceProvider();
        var shared = provider.GetRequiredService<IDecisionClient>();
        var kev = provider.GetRequiredService<KevOnnxDecisionClient>();
        Assert.Same(kevFirst ? kev : (IDecisionClient)provider.GetRequiredService<ILayaDecisionClient>(), shared);
    }

    [Fact]
    public async Task Client_ValidatesRequestsAndCancellationBeforeLoadingWeights()
    {
        using var client = new KevOnnxDecisionClient(_directory);
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.ChooseAsync("state", "question", new Dictionary<string, string?> { ["only"] = null }));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.ScoreAsync("state", "question", []));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.AssessAsync("state", ""));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.AssessAsync("state", "question", new CancellationToken(true)));
        var error = await Assert.ThrowsAsync<DecisionException>(() => client.AssessAsync("state", "question"));
        Assert.Contains("convert_kev.py", error.Message);
    }

    [Fact]
    public async Task Client_RejectsUnsupportedExportAndDisposedUse()
    {
        File.WriteAllText(Path.Combine(_directory, "kev_config.json"),
            """{"format":"other","checkpoint_revision":"other"}""");
        var client = new KevOnnxDecisionClient(_directory);
        await Assert.ThrowsAsync<DecisionException>(() => client.AssessAsync("state", "question"));
        client.Dispose();
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.AssessAsync("state", "question"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
