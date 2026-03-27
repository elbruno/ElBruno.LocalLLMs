using System.ComponentModel;
using System.Text.RegularExpressions;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// ────────────────────────────────────────────────────────
// FineTunedToolCalling — demonstrates using a fine-tuned
// Qwen2.5-0.5B model that has learned the tool-calling
// format. This is a DEMONSTRATION of the fine-tuning
// pipeline; the 0.5B model learns the shape of tool calls
// but may produce imperfect JSON. Larger models (1.5B+)
// are recommended for production tool calling.
// ────────────────────────────────────────────────────────

Console.WriteLine("🎯 Fine-Tuned Tool Calling Demo");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Demonstrating fine-tuned Qwen2.5-0.5B for tool calling\n");

// ── Load the fine-tuned model (with fallback to base model) ──
// This model was trained specifically for tool calling on
// ElBruno.LocalLLMs' QwenFormatter chat template format.
var fineTunedOptions = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05B_ToolCalling
};

Console.WriteLine("Loading fine-tuned model (first run downloads from HuggingFace)...\n");

LocalChatClient? fineTunedClient = null;
bool usingFallback = false;

// ── Progress bar for model download ──
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

try
{
    fineTunedClient = await LocalChatClient.CreateAsync(fineTunedOptions, progress);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("not found on HuggingFace"))
{
    Console.WriteLine($"\n⚠️  Fine-tuned model '{KnownModels.Qwen25_05B_ToolCalling.DisplayName}' is not available yet.");
    Console.WriteLine("    This model will be published after fine-tuning is complete.");
    Console.WriteLine($"    Falling back to base model: {KnownModels.Qwen25_05BInstruct.DisplayName}\n");

    fineTunedOptions = new LocalLLMsOptions
    {
        Model = KnownModels.Qwen25_05BInstruct
    };
    fineTunedClient = await LocalChatClient.CreateAsync(fineTunedOptions, progress);
    usingFallback = true;
}

Volatile.Write(ref downloadComplete, true);
if (progressRenderer.NeedsFinalNewLine)
{
    Console.WriteLine();
}

using var client = fineTunedClient;
Console.WriteLine($"Model: {fineTunedOptions.Model.DisplayName}{(usingFallback ? " (fallback)" : "")}");

var modelCachePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ElBruno", "LocalLLMs", "models",
    fineTunedOptions.Model.HuggingFaceRepoId.Replace('/', Path.DirectorySeparatorChar));
Console.WriteLine($"📁 Model path: {modelCachePath}");
Console.WriteLine();

// ── Define tools using AIFunctionFactory ──
// Same tools as the ToolCallingAgent sample — the fine-tuned model
// produces more accurate JSON for these function schemas.
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetCurrentTime),
    AIFunctionFactory.Create(Calculate),
    AIFunctionFactory.Create(GetWeather)
};

// ══════════════════════════════════════════
// Demo 1: Single-turn tool call
// Fine-tuned models are better at producing valid
// <tool_call> JSON on the first attempt.
// ══════════════════════════════════════════
Console.WriteLine("═══ Demo 1: Single-Turn Tool Call ═══\n");

var singleTurnMessages = new List<ChatMessage>
{
    new(ChatRole.User, "What time is it in UTC?")
};

Console.WriteLine($"👤 User: {singleTurnMessages[0].Text}");

var singleResponse = await client!.GetResponseAsync(
    singleTurnMessages,
    new ChatOptions { Tools = tools, MaxOutputTokens = 512 });

var toolCalls = singleResponse.Messages
    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
    .ToList();

if (toolCalls.Count > 0)
{
    Console.WriteLine($"🔧 Model requested {toolCalls.Count} tool call(s):");
    foreach (var tc in toolCalls)
    {
        Console.WriteLine($"   → {tc.Name}({FormatArgs(tc.Arguments)})");
    }
}
else
{
    var text = singleResponse.Text ?? "";
    Console.WriteLine($"🤖 Model: {TruncateResponse(text)}");
    if (LooksLikeRawToolCalls(text))
        Console.WriteLine("   ⚠️ Model attempted tool calls but output wasn't parseable (expected for 0.5B models)");
}

// ══════════════════════════════════════════
// Demo 2: Multi-turn agent loop
// Fine-tuned models handle multi-tool scenarios
// more reliably, calling the right tools in sequence.
// ══════════════════════════════════════════
Console.WriteLine("\n═══ Demo 2: Multi-Turn Agent Loop ═══\n");

await RunAgentLoop(client, tools,
    "What's the weather like in Paris and what is 25 * 4 + 10?");

// ══════════════════════════════════════════
// Demo 3: Complex multi-tool scenario
// ══════════════════════════════════════════
Console.WriteLine("\n═══ Demo 3: Complex Multi-Tool Query ═══\n");

await RunAgentLoop(client, tools,
    "Get the current UTC time, then calculate 100 / 5, and check the weather in Tokyo.");

Console.WriteLine("\n✅ Fine-tuned tool calling demo complete!");
Console.WriteLine("\n💡 Tips:");
Console.WriteLine("   • The 0.5B fine-tuned model learns the tool-call FORMAT but may not produce perfect JSON");
Console.WriteLine("   • This demo showcases the fine-tuning pipeline — for production use, try 1.5B+ models");
Console.WriteLine("   • See docs/fine-tuning-guide.md for training your own models");

// ────────────────────────────────────────────────────────
// Agent loop: send message → execute tool calls → repeat
// until the model gives a final text answer.
// ────────────────────────────────────────────────────────
async Task RunAgentLoop(IChatClient chatClient, List<AITool> agentTools, string userMessage)
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, userMessage)
    };

    Console.WriteLine($"👤 User: {userMessage}");

    const int maxIterations = 10;
    for (int i = 0; i < maxIterations; i++)
    {
        var response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions { Tools = agentTools, MaxOutputTokens = 512 });

        // Collect any tool calls from the response
        var calls = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .ToList();

        if (calls.Count == 0)
        {
            // No tool calls — model produced a final text answer
            var text = response.Text ?? "";
            Console.WriteLine($"🤖 Assistant: {TruncateResponse(text)}");
            if (LooksLikeRawToolCalls(text))
                Console.WriteLine("   ⚠️ Model attempted tool calls but output wasn't parseable (expected for 0.5B models)");
            return;
        }

        // Add the assistant's response (with tool calls) to conversation history
        messages.AddRange(response.Messages);

        // Execute each tool call and send results back
        Console.WriteLine($"   ⚙️  Round {i + 1}: model requested {calls.Count} tool call(s)");

        var resultContents = new List<AIContent>();
        foreach (var call in calls)
        {
            Console.WriteLine($"      → Calling {call.Name}({FormatArgs(call.Arguments)})");

            // Find and invoke the matching tool
            var tool = agentTools.OfType<AIFunction>().FirstOrDefault(t => t.Name == call.Name);
            object? result = tool is not null
                ? await tool.InvokeAsync(call.Arguments is null ? null : new AIFunctionArguments(call.Arguments))
                : $"Unknown tool: {call.Name}";

            Console.WriteLine($"      ← Result: {result}");

            resultContents.Add(new FunctionResultContent(call.CallId, result?.ToString() ?? ""));
        }

        // Add tool results as a user message so the model can continue
        messages.Add(new ChatMessage(ChatRole.Tool, resultContents));
    }

    Console.WriteLine("⚠️  Agent loop hit max iterations without a final answer.");
}

static string FormatArgs(IDictionary<string, object?>? args) =>
    args is null ? "" : string.Join(", ", args.Select(kv => $"{kv.Key}: {kv.Value}"));

static string TruncateResponse(string text, int maxLength = 500)
{
    if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
    return text[..maxLength] + "\n   ... [truncated — model generated " + text.Length + " chars]";
}

static bool LooksLikeRawToolCalls(string text) =>
    text.Length > 100 && Regex.IsMatch(text, @"\{""name"":\s*""");

// ────────────────────────────────────────────────────────
// Tool definitions — identical to ToolCallingAgent sample.
// The fine-tuned model handles these schemas more reliably.
// ────────────────────────────────────────────────────────

[Description("Gets the current date and time in the specified timezone.")]
static string GetCurrentTime(
    [Description("Timezone name, e.g. 'UTC', 'Eastern Standard Time', 'Tokyo Standard Time'")] string timezone = "UTC")
{
    try
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return $"{now:yyyy-MM-dd HH:mm:ss} ({tz.StandardName})";
    }
    catch (TimeZoneNotFoundException)
    {
        return $"Unknown timezone: {timezone}. Use standard timezone names like 'UTC', 'Eastern Standard Time', 'Tokyo Standard Time'.";
    }
}

[Description("Evaluates a simple math expression. Supports +, -, *, / with two numbers.")]
static string Calculate(
    [Description("First number")] double a,
    [Description("Mathematical operator: +, -, *, /")] string op,
    [Description("Second number")] double b)
{
    var result = op switch
    {
        "+" => a + b,
        "-" => a - b,
        "*" => a * b,
        "/" when b != 0 => a / b,
        "/" => double.NaN,
        _ => double.NaN
    };

    return double.IsNaN(result)
        ? $"Error: invalid operation '{a} {op} {b}'"
        : $"{a} {op} {b} = {result}";
}

[Description("Gets the current weather for a city. Returns temperature and conditions.")]
static string GetWeather(
    [Description("City name, e.g. 'Paris', 'Tokyo', 'New York'")] string city)
{
    // Mock weather data for demonstration
    var weather = city.ToLowerInvariant() switch
    {
        "paris" => "☁️ 18°C, partly cloudy",
        "tokyo" => "☀️ 24°C, sunny",
        "new york" => "🌧️ 15°C, light rain",
        "london" => "🌫️ 12°C, foggy",
        "sydney" => "☀️ 22°C, clear skies",
        _ => $"🌡️ 20°C, mild (no specific data for {city})"
    };

    return $"Weather in {city}: {weather}";
}
