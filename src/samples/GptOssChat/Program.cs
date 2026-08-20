using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// ─────────────────────────────────────────────────────────────────────────────
// GPT-OSS 20B — OpenAI's open-weight MoE model (Apache-2.0) running locally
// through ONNX Runtime GenAI.
//
// Heads-up before you run this:
//   • First run downloads ~12 GB of INT4 weights.
//   • GPT-OSS is a mixture-of-experts model; CPU inference is slow. If you have
//     a CUDA GPU, switch to KnownModels.GptOss20BCuda and swap the package
//     reference in GptOssChat.csproj.
//   • GPT-OSS "thinks" before it answers. That chain-of-thought is emitted on a
//     separate Harmony channel and is deliberately filtered out — the model card
//     states it is not intended to be shown to end users. You only ever see the
//     final answer.
//
// Set GPTOSS_MODEL_PATH to reuse an already-downloaded model directory.
// ─────────────────────────────────────────────────────────────────────────────

var options = new LocalLLMsOptions
{
    Model = KnownModels.GptOss20B,

    // GPT-OSS-specific: trade latency against depth of reasoning.
    // Ignored by every other model in the library.
    ReasoningEffort = ReasoningEffort.Low,

    MaxSequenceLength = 4096,
};

var localPath = Environment.GetEnvironmentVariable("GPTOSS_MODEL_PATH");
if (!string.IsNullOrWhiteSpace(localPath))
{
    options.ModelPath = localPath;
    options.EnsureModelDownloaded = false;
}

Console.WriteLine("Loading GPT-OSS 20B (this can take a while on first run)...");
await using var client = await LocalChatClient.CreateAsync(options);
Console.WriteLine("Ready.\n");

// ── 1. Simple chat ───────────────────────────────────────────────────────────

Console.WriteLine("── Chat ──");
var response = await client.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "What is the capital of France? Answer in one sentence.")
],
new ChatOptions { MaxOutputTokens = 128 });

Console.WriteLine(response.Text);
Console.WriteLine();

// ── 2. Streaming ─────────────────────────────────────────────────────────────

Console.WriteLine("── Streaming ──");
await foreach (var update in client.GetStreamingResponseAsync(
[
    new ChatMessage(ChatRole.User, "List three primary colours, one per line.")
],
new ChatOptions { MaxOutputTokens = 128 }))
{
    Console.Write(update.Text);
}

Console.WriteLine();
Console.WriteLine();

// ── 3. Tool calling ──────────────────────────────────────────────────────────

Console.WriteLine("── Tool calling ──");

var getWeather = AIFunctionFactory.Create(
    (string city) => $"It is 18°C and sunny in {city}.",
    "get_weather",
    "Get the current weather for a city.");

var toolResponse = await client.GetResponseAsync(
[
    new ChatMessage(ChatRole.System, "You are a helpful assistant. Use the provided tools when relevant."),
    new ChatMessage(ChatRole.User, "What is the weather in Paris?")
],
new ChatOptions { Tools = [getWeather], MaxOutputTokens = 256 });

var calls = toolResponse.Messages
    .SelectMany(m => m.Contents)
    .OfType<FunctionCallContent>()
    .ToList();

if (calls.Count > 0)
{
    foreach (var call in calls)
    {
        var argText = string.Join(", ", call.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? []);
        Console.WriteLine($"Model requested: {call.Name}({argText})");
    }
}
else
{
    Console.WriteLine(toolResponse.Text);
}
