using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetModelDownloader"/> — cache detection, path resolution, and null handling.
/// NOTE: Actual download tests are integration tests requiring network access.
/// </summary>
public class BitNetModelDownloaderTests
{
    // ──────────────────────────────────────────────
    // Default cache directory
    // ──────────────────────────────────────────────

    [Fact]
    public void GetCacheDirectory_ReturnsLocalAppDataPath()
    {
        var downloader = new BitNetModelDownloader();

        var cacheDir = downloader.GetCacheDirectory();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalLLMs", "models");
        Assert.Equal(expected, cacheDir);
    }

    // ──────────────────────────────────────────────
    // Null argument checks
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnsureModelAsync_NullModel_ThrowsArgumentNullException()
    {
        var downloader = new BitNetModelDownloader();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            downloader.EnsureModelAsync(null!));
    }

    // ──────────────────────────────────────────────
    // Cache hit — returns immediately when file exists
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnsureModelAsync_ReturnsExistingPath_WhenFileAlreadyCached()
    {
        var downloader = new BitNetModelDownloader();
        var model = BitNetKnownModels.BitNet2B4T;
        var tempDir = Path.Combine(Path.GetTempPath(), "bitnet-test-cache-" + Guid.NewGuid().ToString("N"));

        try
        {
            // Pre-create the cache directory and GGUF file
            var modelDir = Path.Combine(tempDir, model.Id.Replace('/', '-').Replace('\\', '-'));
            Directory.CreateDirectory(modelDir);
            var ggufPath = Path.Combine(modelDir, model.GgufFileName);
            await File.WriteAllTextAsync(ggufPath, "fake-gguf-content");

            var result = await downloader.EnsureModelAsync(model, cacheDirectory: tempDir);

            Assert.Equal(ggufPath, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_UsesCustomCacheDirectory()
    {
        var downloader = new BitNetModelDownloader();
        var model = BitNetKnownModels.Falcon3_1B;
        var tempDir = Path.Combine(Path.GetTempPath(), "bitnet-test-custom-" + Guid.NewGuid().ToString("N"));

        try
        {
            // Pre-create to simulate cached model
            var modelDir = Path.Combine(tempDir, model.Id.Replace('/', '-').Replace('\\', '-'));
            Directory.CreateDirectory(modelDir);
            var ggufPath = Path.Combine(modelDir, model.GgufFileName);
            await File.WriteAllTextAsync(ggufPath, "fake-gguf");

            var result = await downloader.EnsureModelAsync(model, cacheDirectory: tempDir);

            Assert.StartsWith(tempDir, result);
            Assert.EndsWith(model.GgufFileName, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ──────────────────────────────────────────────
    // All known models resolve valid paths
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllKnownModels))]
    public async Task EnsureModelAsync_AllKnownModels_ReturnCorrectFileName(BitNetModelDefinition model)
    {
        var downloader = new BitNetModelDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), "bitnet-test-all-" + Guid.NewGuid().ToString("N"));

        try
        {
            var modelDir = Path.Combine(tempDir, model.Id.Replace('/', '-').Replace('\\', '-'));
            Directory.CreateDirectory(modelDir);
            var ggufPath = Path.Combine(modelDir, model.GgufFileName);
            await File.WriteAllTextAsync(ggufPath, "fake-gguf");

            var result = await downloader.EnsureModelAsync(model, cacheDirectory: tempDir);

            Assert.EndsWith(model.GgufFileName, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    public static TheoryData<BitNetModelDefinition> AllKnownModels => new()
    {
        BitNetKnownModels.BitNet2B4T,
        BitNetKnownModels.BitNet07B,
        BitNetKnownModels.BitNet3B,
        BitNetKnownModels.Falcon3_1B,
        BitNetKnownModels.Falcon3_3B,
    };
}
