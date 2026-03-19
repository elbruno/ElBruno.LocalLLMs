using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var startTime = DateTime.Now;

// ╔════════════════════════════════════════════════════════════════════╗
// ║          ElBruno.LocalLLMs — Console Demo Application            ║
// ╚════════════════════════════════════════════════════════════════════╝
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          ElBruno.LocalLLMs — Console Demo Application             ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Local LLM inference with ONNX Runtime GenAI                      ║");
Console.WriteLine("║  Microsoft.Extensions.AI compatible (IChatClient)                  ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Example 1: Model Download with Progress Tracking
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("  Example 1: Model Download with Progress Tracking");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct,
    CacheDirectory = null,
    EnsureModelDownloaded = true,
};

var defaultCachePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ElBruno", "LocalLLMs", "models");

Console.WriteLine($"  📁 Default cache directory: {defaultCachePath}");
Console.WriteLine($"  🤖 Model: {options.Model.DisplayName}");
Console.WriteLine();

var loadStart = DateTime.Now;

var progressLock = new object();
var progress = new Progress<ModelDownloadProgress>(p =>
{
    lock (progressLock)
    {
        try
        {
            var filled = Math.Clamp((int)(p.PercentComplete / 100.0 * 30), 0, 30);
            var bar = new string('█', filled);
            var empty = new string('░', 30 - filled);
            var fileName = Path.GetFileName(p.FileName);
            if (fileName.Length > 30) fileName = fileName[..27] + "...";
            var width = Math.Max(80, Console.WindowWidth);
            var line = $"  ⬇️ [{bar}{empty}] {p.PercentComplete:F0}% {fileName}";
            Console.CursorLeft = 0;
            Console.Write(line.PadRight(width - 1)[..(width - 1)]);
        }
        catch (IOException)
        {
            // Redirected console — skip progress display
        }
    }
});

using var client = await LocalChatClient.CreateAsync(options, progress);

var loadTime = DateTime.Now - loadStart;
Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"  ✓ Model loaded in {loadTime.TotalSeconds:F1}s");
Console.WriteLine();
Console.WriteLine($"  Provider:  {client.Metadata.ProviderName}");
Console.WriteLine($"  Model ID:  {client.Metadata.DefaultModelId}");
Console.WriteLine($"  URI:       {client.Metadata.ProviderUri}");
Console.WriteLine();

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Example 2: Simple Q&A (Non-Streaming)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("  Example 2: Simple Q&A (Non-Streaming)");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var question = "What is the capital of France?";
Console.WriteLine($"  🗣️ Question: {question}");
Console.WriteLine();

var qaStart = DateTime.Now;
var response = await client.GetResponseAsync([
    new ChatMessage(ChatRole.User, question)
]);
var qaTime = DateTime.Now - qaStart;

Console.WriteLine($"  🤖 Response: {response.Text}");
Console.WriteLine();
Console.WriteLine($"  ✓ Response generated in {qaTime.TotalSeconds:F1}s");
Console.WriteLine();

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Example 3: Streaming Q&A
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("  Example 3: Streaming Q&A");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var streamQuestion = "Explain what machine learning is in 2-3 sentences.";
Console.WriteLine($"  🗣️ Question: {streamQuestion}");
Console.WriteLine();
Console.Write("  🤖 Response: ");

var streamStart = DateTime.Now;
await foreach (var update in client.GetStreamingResponseAsync([
    new ChatMessage(ChatRole.System, "You are a helpful assistant. Be concise."),
    new ChatMessage(ChatRole.User, streamQuestion)
]))
{
    Console.Write(update.Text);
}
var streamTime = DateTime.Now - streamStart;

Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"  ✓ Streamed in {streamTime.TotalSeconds:F1}s");
Console.WriteLine();

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Example 4: Multi-Turn Conversation
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("  Example 4: Multi-Turn Conversation");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var conversation = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant. Be concise.")
};

// Turn 1
var turn1Question = "What is C#?";
Console.WriteLine($"  🗣️ Turn 1: {turn1Question}");
conversation.Add(new ChatMessage(ChatRole.User, turn1Question));

var turn1Start = DateTime.Now;
var turn1Response = await client.GetResponseAsync(conversation);
var turn1Time = DateTime.Now - turn1Start;

Console.WriteLine($"  🤖 Response: {turn1Response.Text}");
Console.WriteLine($"  ✓ Generated in {turn1Time.TotalSeconds:F1}s");
Console.WriteLine();

// Add assistant response to conversation history
conversation.Add(new ChatMessage(ChatRole.Assistant, turn1Response.Text));

// Turn 2 — follow-up that relies on context
var turn2Question = "What are its main features?";
Console.WriteLine($"  🗣️ Turn 2: {turn2Question}");
conversation.Add(new ChatMessage(ChatRole.User, turn2Question));

var turn2Start = DateTime.Now;
var turn2Response = await client.GetResponseAsync(conversation);
var turn2Time = DateTime.Now - turn2Start;

Console.WriteLine($"  🤖 Response: {turn2Response.Text}");
Console.WriteLine($"  ✓ Generated in {turn2Time.TotalSeconds:F1}s");
Console.WriteLine();
Console.WriteLine($"  💡 The model understood \"its\" refers to C# from the previous turn!");
Console.WriteLine();

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Summary
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
var totalTime = DateTime.Now - startTime;

Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                          Summary                                  ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  ✓ Local LLM inference (no API calls needed)                      ║");
Console.WriteLine("║  ✓ Microsoft.Extensions.AI compatible (IChatClient)                ║");
Console.WriteLine("║  ✓ Automatic model downloading and caching                        ║");
Console.WriteLine("║  ✓ Streaming support for real-time responses                      ║");
Console.WriteLine("║  ✓ 20+ models from tiny (0.5B) to large (70B)                     ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Total time: {totalTime.TotalSeconds:F1}s{new string(' ', 53 - totalTime.TotalSeconds.ToString("F1").Length)}║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
