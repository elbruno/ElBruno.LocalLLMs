// FaraVisionAgent — demo of Fara1.5-9B vision-language model via LocalVisionChatClient.
//
// Fara1.5-9B ONNX is available at elbruno/Fara1.5-9B-onnx — set EnsureModelDownloaded = true
// to auto-download on first run, or pass --model-path to use a local directory.
//
// Usage:
//   dotnet run                              (auto-download from elbruno/Fara1.5-9B-onnx)
//   dotnet run -- --model-path ./fara-onnx --image ./screenshot.png

using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var modelPath = GetArg(args, "--model-path");
var imagePath = GetArg(args, "--image");

if (string.IsNullOrWhiteSpace(modelPath))
{
    Console.Error.WriteLine("Usage: FaraVisionAgent --model-path <path> [--image <path>]");
    Console.Error.WriteLine("  --model-path  Path to the ONNX-converted Fara1.5-9B directory.");
    Console.Error.WriteLine("  --image       Optional: path to a screenshot PNG to analyze.");
    return 1;
}

Console.WriteLine($"Loading Fara1.5-9B from: {modelPath}");
if (!string.IsNullOrWhiteSpace(imagePath))
    Console.WriteLine($"Image: {imagePath}");

var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    ModelPath = modelPath,
    MaxSequenceLength = 4096,
    Temperature = 0.1f,
};

await using var client = new LocalVisionChatClient(options);

// ── Query 1: Analyze screenshot ─────────────────────────────────────────────
Console.WriteLine("\n─── Query 1: Screenshot analysis ───────────────────────────────");

var analyzeMessages = new List<ChatMessage>
{
    new(ChatRole.User, "What elements do you see in this screenshot? List any interactive elements.")
};

var visionOptions = new VisionChatOptions
{
    ImagePaths = string.IsNullOrWhiteSpace(imagePath) ? [] : [imagePath]
};

Console.Write("Fara: ");
await foreach (var token in client.GetStreamingResponseAsync(analyzeMessages, visionOptions))
{
    Console.Write(token.Text);
}
Console.WriteLine();

// ── Query 2: Action request ──────────────────────────────────────────────────
Console.WriteLine("\n─── Query 2: Action request ─────────────────────────────────────");
Console.WriteLine("(Fara may emit <action>click(x,y)</action> tags for UI actions)");

var actionMessages = new List<ChatMessage>
{
    new(ChatRole.User, "Click on the search button.")
};

// Text-only request — no image paths needed for follow-up
Console.Write("Fara: ");
await foreach (var token in client.GetStreamingResponseAsync(actionMessages))
{
    Console.Write(token.Text);
}
Console.WriteLine();

return 0;

static string? GetArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
