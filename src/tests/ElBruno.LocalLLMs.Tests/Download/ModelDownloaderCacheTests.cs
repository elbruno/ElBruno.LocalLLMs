using System.Reflection;
using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests.Download;

public class ModelDownloaderCacheTests
{
    [Fact]
    public void IsModelCached_GlobModel_MissingExternalData_ReturnsFalse()
    {
        var model = KnownModels.Phi35MiniInstruct;
        var modelDir = CreateTempModelDir();
        var modelPath = Path.Combine(modelDir, model.ModelSubPath!);
        Directory.CreateDirectory(modelPath);

        try
        {
            File.WriteAllText(Path.Combine(modelPath, "genai_config.json"), "{}");
            File.WriteAllText(Path.Combine(modelPath, "model.onnx"), "stub");

            var cached = InvokeIsModelCached(model, modelDir, modelPath);

            Assert.False(cached);
        }
        finally
        {
            TryDelete(modelDir);
        }
    }

    [Fact]
    public void IsModelCached_GlobModel_WithExternalData_ReturnsTrue()
    {
        var model = KnownModels.Phi35MiniInstruct;
        var modelDir = CreateTempModelDir();
        var modelPath = Path.Combine(modelDir, model.ModelSubPath!);
        Directory.CreateDirectory(modelPath);

        try
        {
            File.WriteAllText(Path.Combine(modelPath, "genai_config.json"), "{}");
            File.WriteAllText(Path.Combine(modelPath, "model.onnx"), "stub");
            File.WriteAllText(Path.Combine(modelPath, "model.onnx.data"), "weights");

            var cached = InvokeIsModelCached(model, modelDir, modelPath);

            Assert.True(cached);
        }
        finally
        {
            TryDelete(modelDir);
        }
    }

    private static bool InvokeIsModelCached(ModelDefinition model, string modelDir, string modelPath)
    {
        var method = typeof(ModelDownloader).GetMethod(
            "IsModelCached",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [model, modelDir, modelPath]);
        Assert.IsType<bool>(result);
        return (bool)result!;
    }

    // ─────────────────────────────────────────────────────────────
    // DeleteModelAsync tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteModelAsync_ExistingModel_DeletesDirectory()
    {
        var cacheRoot = CreateTempModelDir();
        try
        {
            var model = KnownModels.Qwen25_05BInstruct;
            // Replicate the sanitized model ID path the downloader uses
            var sanitizedId = model.Id.Replace('/', '-').Replace('\\', '-');
            var modelDir = Path.Combine(cacheRoot, sanitizedId);
            Directory.CreateDirectory(modelDir);
            File.WriteAllText(Path.Combine(modelDir, "genai_config.json"), "{}");

            Assert.True(Directory.Exists(modelDir));

            var downloader = new ModelDownloader();
            await downloader.DeleteModelAsync(model, cacheRoot);

            Assert.False(Directory.Exists(modelDir));
        }
        finally
        {
            TryDelete(cacheRoot);
        }
    }

    [Fact]
    public async Task DeleteModelAsync_NonExistentModel_DoesNotThrow()
    {
        var cacheRoot = CreateTempModelDir();
        try
        {
            var model = KnownModels.Qwen25_05BInstruct;
            var downloader = new ModelDownloader();

            // Should not throw even if directory doesn't exist
            await downloader.DeleteModelAsync(model, cacheRoot);
        }
        finally
        {
            TryDelete(cacheRoot);
        }
    }

    [Fact]
    public async Task DeleteModelAsync_OnlyDeletesTargetModel_LeavesOthersIntact()
    {
        var cacheRoot = CreateTempModelDir();
        try
        {
            // Create two model directories
            var modelA = KnownModels.Qwen25_05BInstruct;
            var modelB = KnownModels.Qwen25_15BInstruct;

            var sanitizedIdA = modelA.Id.Replace('/', '-').Replace('\\', '-');
            var sanitizedIdB = modelB.Id.Replace('/', '-').Replace('\\', '-');

            var dirA = Path.Combine(cacheRoot, sanitizedIdA);
            var dirB = Path.Combine(cacheRoot, sanitizedIdB);

            Directory.CreateDirectory(dirA);
            Directory.CreateDirectory(dirB);
            File.WriteAllText(Path.Combine(dirA, "genai_config.json"), "{}");
            File.WriteAllText(Path.Combine(dirB, "genai_config.json"), "{}");

            var downloader = new ModelDownloader();
            await downloader.DeleteModelAsync(modelA, cacheRoot);

            Assert.False(Directory.Exists(dirA), "Model A directory should be deleted");
            Assert.True(Directory.Exists(dirB), "Model B directory should be untouched");
        }
        finally
        {
            TryDelete(cacheRoot);
        }
    }

    [Fact]
    public async Task DeleteModelAsync_DefaultCacheDirectory_UsesAppDataPath()
    {
        // Verifies the default cache path is under LocalApplicationData
        var downloader = new ModelDownloader();
        var defaultCache = downloader.GetCacheDirectory();
        Assert.Contains("ElBruno", defaultCache, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalLLMs", defaultCache, StringComparison.OrdinalIgnoreCase);

        // DeleteModelAsync with no model dir present should be a no-op
        var model = KnownModels.Qwen25_05BInstruct;
        await downloader.DeleteModelAsync(model); // no exception
    }

    [Fact]
    public async Task DeleteModelFromCacheAsync_OnLocalChatClient_DeletesDirectory()
    {
        var cacheRoot = CreateTempModelDir();
        try
        {
            var model = KnownModels.Qwen25_05BInstruct;
            var sanitizedId = model.Id.Replace('/', '-').Replace('\\', '-');
            var modelDir = Path.Combine(cacheRoot, sanitizedId);
            Directory.CreateDirectory(modelDir);
            File.WriteAllText(Path.Combine(modelDir, "genai_config.json"), "{}");

            await LocalChatClient.DeleteModelFromCacheAsync(model, cacheRoot);

            Assert.False(Directory.Exists(modelDir));
        }
        finally
        {
            TryDelete(cacheRoot);
        }
    }

    [Fact]
    public async Task DeleteModelFromCacheAsync_OnLocalVisionChatClient_DeletesDirectory()
    {
        var cacheRoot = CreateTempModelDir();
        try
        {
            var model = KnownModels.Fara15_9B;
            var sanitizedId = model.Id.Replace('/', '-').Replace('\\', '-');
            var modelDir = Path.Combine(cacheRoot, sanitizedId);
            Directory.CreateDirectory(modelDir);
            File.WriteAllText(Path.Combine(modelDir, "genai_config.json"), "{}");

            await LocalVisionChatClient.DeleteModelFromCacheAsync(model, cacheRoot);

            Assert.False(Directory.Exists(modelDir));
        }
        finally
        {
            TryDelete(cacheRoot);
        }
    }

    private static string CreateTempModelDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "localllms-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup in tests.
        }
    }
}
