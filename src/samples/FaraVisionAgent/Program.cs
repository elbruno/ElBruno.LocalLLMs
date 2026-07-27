// FaraVisionAgent — demo of Fara1.5-9B agentic model via LocalChatClient.
//
// Fara1.5-9B uses the qwen3_5 text architecture in ORT-GenAI 0.14.1.
// It loads via the GenAI/text path (LocalChatClient), not the vision path.
// ONNX INT4 is available at elbruno/Fara1.5-9B-onnx — set EnsureModelDownloaded = true
// to auto-download on first run.
//
// Usage:
//   dotnet run                              (auto-download from elbruno/Fara1.5-9B-onnx)
//   dotnet run -- --model-path ./fara-onnx

using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var modelPath = GetArg(args, "--model-path");

var options = new LocalLLMsOptions
{
    Model = KnownModels.Fara15_9B,
    EnsureModelDownloaded = true,
    MaxSequenceLength = 4096,
    Temperature = 0.1f,
};

if (!string.IsNullOrWhiteSpace(modelPath))
{
    options.ModelPath = modelPath;
    options.EnsureModelDownloaded = false;
    Console.WriteLine($"Loading Fara1.5-9B from: {modelPath}");
}
else
{
    Console.WriteLine("Loading Fara1.5-9B (auto-download from elbruno/Fara1.5-9B-onnx)...");
}

await using var client = await LocalChatClient.CreateAsync(options);

// ── Query 1: Agentic action planning ────────────────────────────────────────
Console.WriteLine("\n─── Query 1: Agentic action ────────────────────────────────────────");

var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Go to https://github.com and search for 'ElBruno.LocalLLMs'. List the steps you would take.")
};

Console.Write("Fara: ");
await foreach (var token in client.GetStreamingResponseAsync(messages))
{
    Console.Write(token.Text);
}
Console.WriteLine();

// ── Query 2: Follow-up ────────────────────────────────────────────────────────
Console.WriteLine("\n─── Query 2: Follow-up ─────────────────────────────────────────────");

messages.Add(new ChatMessage(ChatRole.Assistant, "I would navigate to GitHub and use the search bar."));
messages.Add(new ChatMessage(ChatRole.User, "What would you click first?"));

Console.Write("Fara: ");
await foreach (var token in client.GetStreamingResponseAsync(messages))
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
