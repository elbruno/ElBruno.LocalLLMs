using System.Diagnostics;
using ElBruno.LocalLLMs;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using McpToolRouting;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

// ────────────────────────────────────────────────────────
// McpToolRouting — demonstrates the full pipeline:
//   User prompt → local LLM distillation → embedding →
//   MCPToolRouter filtering → show results
// ────────────────────────────────────────────────────────

Console.WriteLine("🔀 MCP Tool Routing with Local LLM Distillation");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Pipeline: User prompt → LLM distillation → embedding → tool routing\n");

// ── Step 1: Load the local LLM for prompt distillation ──
var llmOptions = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct
};

Console.WriteLine($"📦 Loading LLM: {llmOptions.Model.DisplayName}");
Console.WriteLine("   (First run downloads ~1 GB from HuggingFace)\n");

var downloadComplete = false;
var isInteractive = Environment.UserInteractive && !Console.IsOutputRedirected;
var progressRenderer = new ConsoleDownloadProgressRenderer(isInteractive);
var progressLock = new object();
var progress = new Progress<ModelDownloadProgress>(p =>
{
    if (Volatile.Read(ref downloadComplete)) return;
    lock (progressLock)
    {
        if (Volatile.Read(ref downloadComplete)) return;
        try
        {
            var update = progressRenderer.BuildUpdate(p, DateTimeOffset.UtcNow);
            if (!update.HasValue) return;
            if (update.Value.InPlace)
                Console.Write("\r" + update.Value.Text.PadRight(100));
            else
                Console.WriteLine(update.Value.Text);
        }
        catch (IOException) { }
    }
});

using var chatClient = await LocalChatClient.CreateAsync(llmOptions, progress);
Volatile.Write(ref downloadComplete, true);
if (progressRenderer.NeedsFinalNewLine)
    Console.WriteLine();

var modelCachePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ElBruno", "LocalLLMs", "models",
    llmOptions.Model.HuggingFaceRepoId.Replace('/', Path.DirectorySeparatorChar));
Console.WriteLine($"📁 Model path: {modelCachePath}");

// ── Step 2: Build the MCPToolRouter index ──
Console.WriteLine("\n🔧 Building tool index from 40 MCP tool definitions...");

var allTools = ToolDefinitions.GetAllTools();
var indexSw = Stopwatch.StartNew();

await using var toolIndex = await ToolIndex.CreateAsync(
    allTools,
    new ToolIndexOptions { QueryCacheSize = 10 });

indexSw.Stop();
Console.WriteLine($"   ✅ Indexed {allTools.Length} tools in {indexSw.ElapsedMilliseconds}ms");
Console.WriteLine($"   Embedding model downloads automatically on first use\n");

// ════════════════════════════════════════════════════════
// Scenario 1: Complex multi-part prompt → distillation → filtered tools
// Shows the benefit of distilling a verbose prompt before routing.
// ════════════════════════════════════════════════════════
Console.WriteLine("═══ Scenario 1: Complex Prompt with Distillation ═══\n");

var complexPrompt =
    "Hey, so I was thinking about my trip next week and I need to know if it's going to rain " +
    "in Tokyo. Also, my boss keeps asking about the quarterly report — can you help me figure " +
    "out the budget numbers? Oh and remind me to call the dentist on Friday.";

Console.WriteLine($"👤 User: {complexPrompt}\n");

var distillSw = Stopwatch.StartNew();
var distilled = await PromptDistiller.DistillIntentAsync(chatClient, complexPrompt);
distillSw.Stop();

Console.WriteLine($"🧠 Distilled intent: \"{distilled}\"");
Console.WriteLine($"   ⏱️  Distillation: {distillSw.ElapsedMilliseconds}ms\n");

var routeSw = Stopwatch.StartNew();
var results = await toolIndex.SearchAsync(distilled, topK: 5);
routeSw.Stop();

Console.WriteLine($"🎯 Top-5 matched tools ({routeSw.ElapsedMilliseconds}ms):");
PrintResults(results);

// ════════════════════════════════════════════════════════
// Scenario 2: Simple single-intent prompt → direct routing (skip distillation)
// When the prompt is already clear, distillation is unnecessary overhead.
// ════════════════════════════════════════════════════════
Console.WriteLine("\n═══ Scenario 2: Simple Prompt — Direct Routing ═══\n");

var simplePrompt = "Send an email to Alice about the project deadline";
Console.WriteLine($"👤 User: {simplePrompt}\n");

routeSw.Restart();
var simpleResults = await toolIndex.SearchAsync(simplePrompt, topK: 3);
routeSw.Stop();

Console.WriteLine($"🎯 Top-3 matched tools ({routeSw.ElapsedMilliseconds}ms) — no distillation needed:");
PrintResults(simpleResults);

// ════════════════════════════════════════════════════════
// Scenario 3: Token savings comparison — all tools vs. top-3 routed
// Demonstrates the LLM context window savings.
// ════════════════════════════════════════════════════════
Console.WriteLine("\n═══ Scenario 3: Token Savings Comparison ═══\n");

var tokenPrompt = "What's the current temperature in Paris?";
Console.WriteLine($"👤 User: {tokenPrompt}\n");

// Estimate token cost: each tool definition ≈ name + description ≈ 25-40 tokens
var allToolTokenEstimate = allTools.Sum(t =>
    EstimateTokens(t.Name) + EstimateTokens(t.Description ?? ""));
var routedResults = await toolIndex.SearchAsync(tokenPrompt, topK: 3);
var routedToolTokenEstimate = routedResults.Sum(r =>
    EstimateTokens(r.Tool.Name) + EstimateTokens(r.Tool.Description ?? ""));

Console.WriteLine("📊 Token Estimation (tool definitions in LLM context):");
Console.WriteLine($"   Standard mode (all {allTools.Length} tools):  ~{allToolTokenEstimate:N0} tokens");
Console.WriteLine($"   Routed mode  (top {routedResults.Count} tools):    ~{routedToolTokenEstimate:N0} tokens");

var savings = allToolTokenEstimate > 0
    ? (1.0 - (double)routedToolTokenEstimate / allToolTokenEstimate) * 100
    : 0;
Console.WriteLine($"   💰 Savings:                      ~{savings:F0}% fewer tokens!\n");

Console.WriteLine("   Matched tools:");
PrintResults(routedResults);

// ════════════════════════════════════════════════════════
// Scenario 4: Multi-tool query — shows multiple relevant tools
// A prompt that spans several tool domains.
// ════════════════════════════════════════════════════════
Console.WriteLine("\n═══ Scenario 4: Multi-Tool Query ═══\n");

var multiPrompt = "Translate this document from French to English and then summarize the key points";
Console.WriteLine($"👤 User: {multiPrompt}\n");

distillSw.Restart();
var multiDistilled = await PromptDistiller.DistillIntentAsync(chatClient, multiPrompt);
distillSw.Stop();

Console.WriteLine($"🧠 Distilled intent: \"{multiDistilled}\"");
Console.WriteLine($"   ⏱️  Distillation: {distillSw.ElapsedMilliseconds}ms\n");

routeSw.Restart();
var multiResults = await toolIndex.SearchAsync(multiDistilled, topK: 5);
routeSw.Stop();

Console.WriteLine($"🎯 Top-5 matched tools ({routeSw.ElapsedMilliseconds}ms):");
PrintResults(multiResults);

// ── Summary ──
Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("✅ MCP Tool Routing demo complete!\n");
Console.WriteLine("💡 Key takeaways:");
Console.WriteLine("   • Prompt distillation extracts clean intent from verbose user input");
Console.WriteLine("   • MCPToolRouter uses local embeddings — no API calls needed");
Console.WriteLine($"   • Routing {allTools.Length} tools → top-3 saves ~{savings:F0}% of token budget");
Console.WriteLine("   • Simple prompts can skip distillation for lower latency");
Console.WriteLine("   • All processing runs locally on CPU — no cloud dependency");

// ────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────

static void PrintResults(IReadOnlyList<ToolSearchResult> results)
{
    for (int i = 0; i < results.Count; i++)
    {
        var r = results[i];
        var bar = new string('█', (int)(r.Score * 20));
        var pad = new string('░', 20 - (int)(r.Score * 20));
        Console.WriteLine($"   {i + 1}. {r.Tool.Name,-28} {r.Score:F3}  {bar}{pad}");
        Console.WriteLine($"      {Truncate(r.Tool.Description ?? "", 80)}");
    }
}

static string Truncate(string text, int maxLen) =>
    text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";

static int EstimateTokens(string text) =>
    // Simple approximation: ~4 characters per token for English text
    (int)Math.Ceiling(text.Length / 4.0);
