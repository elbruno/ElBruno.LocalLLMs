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
