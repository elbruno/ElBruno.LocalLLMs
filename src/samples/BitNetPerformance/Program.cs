using System.Diagnostics;
using System.Text.Json;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.BitNet;
using Microsoft.Extensions.AI;

const string prompt = "Explain what quantum computing is in 3 sentences.";
const int maxTokens = 100;

var results = new List<BenchmarkResult>();

var bitnetNativePath = Environment.GetEnvironmentVariable("BITNET_NATIVE_PATH");
var bitnetModelPath = Environment.GetEnvironmentVariable("BITNET_MODEL_PATH");

var bitnetResult = await RunBitNetAsync(bitnetNativePath, bitnetModelPath);
if (bitnetResult is not null)
{
    results.Add(bitnetResult);
}

var qwenResult = await RunOnnxAsync(
    "Qwen2.5-0.5B ONNX INT4",
    "825 MB",
    KnownModels.Qwen25_05BInstruct);
if (qwenResult is not null)
{
    results.Add(qwenResult);
}

var phiResult = await RunOnnxAsync(
    "Phi-3.5-mini ONNX",
    "2.7 GB",
    KnownModels.Phi35MiniInstruct);
if (phiResult is not null)
{
    results.Add(phiResult);
}

if (results.Count == 0)
{
    Console.WriteLine("No benchmarks were executed. Check model availability and configuration.");
}
else
{
    PrintTable(results);
}

var outputPath = Path.Combine(Environment.CurrentDirectory, "benchmark-results.json");
var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine($"Benchmark results written to {outputPath}");

static async Task<BenchmarkResult?> RunBitNetAsync(string? nativePath, string? modelPath)
{
    if (string.IsNullOrWhiteSpace(modelPath) || string.IsNullOrWhiteSpace(nativePath))
    {
        Console.WriteLine("Skipping BitNet benchmark: set BITNET_NATIVE_PATH and BITNET_MODEL_PATH.");
        return null;
    }

    if (!Directory.Exists(nativePath))
    {
        Console.WriteLine($"Skipping BitNet benchmark: native library path not found ({nativePath}).");
        return null;
    }

    if (!File.Exists(modelPath))
    {
        Console.WriteLine($"Skipping BitNet benchmark: model file not found ({modelPath}).");
        return null;
    }

    try
    {
        var beforeLoad = GetWorkingSet();
        var loadTimer = Stopwatch.StartNew();
        using var client = new BitNetChatClient(new BitNetOptions
        {
            Model = BitNetKnownModels.BitNet2B4T,
            NativeLibraryPath = nativePath,
            ModelPath = modelPath
        });
        loadTimer.Stop();
        var afterLoad = GetWorkingSet();

        var metrics = await MeasureStreamingAsync(client);
        var afterInference = GetWorkingSet();

        return new BenchmarkResult
        {
            ModelName = "BitNet b1.58 2B-4T",
            SizeLabel = "400 MB",
            LoadSeconds = loadTimer.Elapsed.TotalSeconds,
            TimeToFirstTokenSeconds = metrics.TimeToFirstToken.TotalSeconds,
            TotalSeconds = metrics.TotalTime.TotalSeconds,
            TokensPerSecond = metrics.TotalTime.TotalSeconds > 0 ? metrics.Tokens / metrics.TotalTime.TotalSeconds : 0,
            MemoryBeforeLoadBytes = beforeLoad,
            MemoryAfterLoadBytes = afterLoad,
            MemoryAfterInferenceBytes = afterInference,
            TokensGenerated = metrics.Tokens
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Skipping BitNet benchmark: {ex.Message}");
        return null;
    }
}

static async Task<BenchmarkResult?> RunOnnxAsync(string modelName, string sizeLabel, ModelDefinition model)
{
    try
    {
        var beforeLoad = GetWorkingSet();
        var loadTimer = Stopwatch.StartNew();
        await using var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
        {
            Model = model
        });
        loadTimer.Stop();
        var afterLoad = GetWorkingSet();

        var metrics = await MeasureStreamingAsync(client);
        var afterInference = GetWorkingSet();

        return new BenchmarkResult
        {
            ModelName = modelName,
            SizeLabel = sizeLabel,
            LoadSeconds = loadTimer.Elapsed.TotalSeconds,
            TimeToFirstTokenSeconds = metrics.TimeToFirstToken.TotalSeconds,
            TotalSeconds = metrics.TotalTime.TotalSeconds,
            TokensPerSecond = metrics.TotalTime.TotalSeconds > 0 ? metrics.Tokens / metrics.TotalTime.TotalSeconds : 0,
            MemoryBeforeLoadBytes = beforeLoad,
            MemoryAfterLoadBytes = afterLoad,
            MemoryAfterInferenceBytes = afterInference,
            TokensGenerated = metrics.Tokens
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Skipping {modelName} benchmark: {ex.Message}");
        return null;
    }
}

static async Task<StreamingMetrics> MeasureStreamingAsync(IChatClient client)
{
    var options = new ChatOptions { MaxOutputTokens = maxTokens };
    var messages = new[] { new ChatMessage(ChatRole.User, prompt) };

    var firstTokenTimer = Stopwatch.StartNew();
    var totalTimer = Stopwatch.StartNew();
    var gotFirstToken = false;
    var tokenCount = 0;

    await foreach (var update in client.GetStreamingResponseAsync(messages, options))
    {
        if (!gotFirstToken)
        {
            gotFirstToken = true;
            firstTokenTimer.Stop();
        }

        tokenCount++;
    }

    totalTimer.Stop();
    if (!gotFirstToken)
    {
        firstTokenTimer.Stop();
    }

    return new StreamingMetrics(tokenCount, firstTokenTimer.Elapsed, totalTimer.Elapsed);
}

static long GetWorkingSet()
{
    var process = Process.GetCurrentProcess();
    process.Refresh();
    return process.WorkingSet64;
}

static void PrintTable(IReadOnlyList<BenchmarkResult> results)
{
    var modelWidth = Math.Max("Model".Length, results.Max(r => r.ModelName.Length));
    var sizeWidth = Math.Max("Size".Length, results.Max(r => r.SizeLabel.Length));
    var loadWidth = Math.Max("Load(s)".Length, results.Max(r => FormatSeconds(r.LoadSeconds).Length));
    var tpsWidth = Math.Max("TPS".Length, results.Max(r => FormatTps(r.TokensPerSecond).Length));
    var ramWidth = Math.Max("RAM".Length, results.Max(r => FormatBytes(r.MemoryAfterInferenceBytes).Length));

    string BuildRow(string model, string size, string load, string tps, string ram) =>
        $"║ {model.PadRight(modelWidth)} │ {size.PadRight(sizeWidth)} │ {load.PadLeft(loadWidth)} │ {tps.PadLeft(tpsWidth)} │ {ram.PadLeft(ramWidth)} ║";

    string BuildSeparator(char left, char mid, char right) =>
        left +
        new string('═', modelWidth + 2) +
        mid + new string('═', sizeWidth + 2) +
        mid + new string('═', loadWidth + 2) +
        mid + new string('═', tpsWidth + 2) +
        mid + new string('═', ramWidth + 2) +
        right;

    var sampleRow = BuildRow("Model", "Size", "Load(s)", "TPS", "RAM");
    var tableWidth = sampleRow.Length;
    var title = "BitNet vs ONNX Performance Comparison";

    Console.WriteLine("╔" + new string('═', tableWidth - 2) + "╗");
    Console.WriteLine("║" + CenterText(title, tableWidth - 2) + "║");
    Console.WriteLine(BuildSeparator('╠', '╦', '╣'));
    Console.WriteLine(BuildRow("Model", "Size", "Load(s)", "TPS", "RAM"));
    Console.WriteLine(BuildSeparator('╠', '╪', '╣'));

    foreach (var result in results)
    {
        Console.WriteLine(BuildRow(
            result.ModelName,
            result.SizeLabel,
            FormatSeconds(result.LoadSeconds),
            FormatTps(result.TokensPerSecond),
            FormatBytes(result.MemoryAfterInferenceBytes)));
    }

    Console.WriteLine(BuildSeparator('╚', '╩', '╝'));
}

static string CenterText(string text, int width)
{
    if (text.Length >= width)
    {
        return text;
    }

    var padding = width - text.Length;
    var left = padding / 2;
    var right = padding - left;
    return new string(' ', left) + text + new string(' ', right);
}

static string FormatSeconds(double seconds) => $"{seconds:0.0}s";

static string FormatTps(double tps) => $"{tps:0.0}";

static string FormatBytes(long bytes)
{
    const double kb = 1024;
    const double mb = 1024 * kb;
    const double gb = 1024 * mb;

    return bytes switch
    {
        >= (long)gb => $"{bytes / gb:0.0}GB",
        >= (long)mb => $"{bytes / mb:0.0}MB",
        _ => $"{bytes / kb:0.0}KB"
    };
}

sealed record StreamingMetrics(int Tokens, TimeSpan TimeToFirstToken, TimeSpan TotalTime);

sealed class BenchmarkResult
{
    public string ModelName { get; init; } = string.Empty;
    public string SizeLabel { get; init; } = string.Empty;
    public double LoadSeconds { get; init; }
    public double TimeToFirstTokenSeconds { get; init; }
    public double TotalSeconds { get; init; }
    public double TokensPerSecond { get; init; }
    public long MemoryBeforeLoadBytes { get; init; }
    public long MemoryAfterLoadBytes { get; init; }
    public long MemoryAfterInferenceBytes { get; init; }
    public int TokensGenerated { get; init; }
}
