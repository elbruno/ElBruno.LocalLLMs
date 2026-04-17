using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetChatClient"/> — constructor validation and metadata.
/// NOTE: Most BitNetChatClient functionality requires the native bitnet.cpp library.
/// These tests focus on what's verifiable without the native library.
/// Integration tests requiring the native lib should use [Trait("Category", "Integration")].
/// </summary>
public class BitNetChatClientTests
{
    // ──────────────────────────────────────────────
    // Constructor null checks
    // ──────────────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BitNetChatClient(null!));
    }

    [Fact]
    public void Constructor_NullOptionsWithLoggerFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BitNetChatClient(null!, null));
    }

    // ──────────────────────────────────────────────
    // CreateAsync null checks
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            BitNetChatClient.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_WithProgress_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            BitNetChatClient.CreateAsync(null!, progress: null));
    }

    // ──────────────────────────────────────────────
    // Type checks
    // ──────────────────────────────────────────────

    [Fact]
    public void BitNetChatClient_ImplementsIChatClient()
    {
        Assert.True(typeof(Microsoft.Extensions.AI.IChatClient).IsAssignableFrom(typeof(BitNetChatClient)));
    }

    [Fact]
    public void BitNetChatClient_ImplementsIAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(BitNetChatClient)));
    }

    [Fact]
    public void BitNetChatClient_IsSealed()
    {
        Assert.True(typeof(BitNetChatClient).IsSealed);
    }
}
