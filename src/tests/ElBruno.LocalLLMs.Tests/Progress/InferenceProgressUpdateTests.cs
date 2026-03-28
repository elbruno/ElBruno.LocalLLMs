using ElBruno.LocalLLMs.Progress;

namespace ElBruno.LocalLLMs.Tests.Progress;

/// <summary>
/// Tests for <see cref="InferenceProgressUpdate"/> — computed TokensPerSecond and record semantics.
/// </summary>
public class InferenceProgressUpdateTests
{
    // ──────────────────────────────────────────────
    // TokensPerSecond computation
    // ──────────────────────────────────────────────

    [Fact]
    public void TokensPerSecond_ValidValues_ComputesCorrectly()
    {
        var update = new InferenceProgressUpdate
        {
            TotalTokens = 100,
            Elapsed = TimeSpan.FromSeconds(2)
        };

        Assert.Equal(50.0, update.TokensPerSecond);
    }

    [Fact]
    public void TokensPerSecond_ZeroElapsed_ReturnsZero()
    {
        var update = new InferenceProgressUpdate
        {
            TotalTokens = 100,
            Elapsed = TimeSpan.Zero
        };

        Assert.Equal(0.0, update.TokensPerSecond);
    }

    [Fact]
    public void TokensPerSecond_ZeroTokens_ReturnsZero()
    {
        var update = new InferenceProgressUpdate
        {
            TotalTokens = 0,
            Elapsed = TimeSpan.FromSeconds(5)
        };

        Assert.Equal(0.0, update.TokensPerSecond);
    }

    [Fact]
    public void TokensPerSecond_BothZero_ReturnsZero()
    {
        var update = new InferenceProgressUpdate
        {
            TotalTokens = 0,
            Elapsed = TimeSpan.Zero
        };

        Assert.Equal(0.0, update.TokensPerSecond);
    }

    [Fact]
    public void TokensPerSecond_SubSecondElapsed_ComputesCorrectly()
    {
        var update = new InferenceProgressUpdate
        {
            TotalTokens = 10,
            Elapsed = TimeSpan.FromMilliseconds(500)
        };

        Assert.Equal(20.0, update.TokensPerSecond);
    }

    // ──────────────────────────────────────────────
    // Record equality
    // ──────────────────────────────────────────────

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new InferenceProgressUpdate
        {
            TokenIndex = 5,
            Token = "hello",
            TotalTokens = 10,
            Elapsed = TimeSpan.FromSeconds(1)
        };

        var b = new InferenceProgressUpdate
        {
            TokenIndex = 5,
            Token = "hello",
            TotalTokens = 10,
            Elapsed = TimeSpan.FromSeconds(1)
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new InferenceProgressUpdate { TokenIndex = 1 };
        var b = new InferenceProgressUpdate { TokenIndex = 2 };

        Assert.NotEqual(a, b);
    }

    // ──────────────────────────────────────────────
    // Default values
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultValues_AreZeroOrEmpty()
    {
        var update = new InferenceProgressUpdate();

        Assert.Equal(0, update.TokenIndex);
        Assert.Equal(string.Empty, update.Token);
        Assert.Equal(0, update.TotalTokens);
        Assert.Equal(TimeSpan.Zero, update.Elapsed);
        Assert.Equal(0.0, update.TokensPerSecond);
    }

    // ──────────────────────────────────────────────
    // Property init
    // ──────────────────────────────────────────────

    [Fact]
    public void Properties_CanBeSetViaInitSyntax()
    {
        var update = new InferenceProgressUpdate
        {
            TokenIndex = 7,
            Token = "world",
            TotalTokens = 42,
            Elapsed = TimeSpan.FromSeconds(3)
        };

        Assert.Equal(7, update.TokenIndex);
        Assert.Equal("world", update.Token);
        Assert.Equal(42, update.TotalTokens);
        Assert.Equal(TimeSpan.FromSeconds(3), update.Elapsed);
    }
}
