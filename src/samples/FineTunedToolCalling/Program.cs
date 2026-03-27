using System.ComponentModel;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// ────────────────────────────────────────────────────────
// FineTunedToolCalling — demonstrates using a fine-tuned
// Qwen2.5-0.5B model for improved tool calling accuracy.
//
// Fine-tuned models produce more reliable JSON tool calls,
// especially for small (0.5B–1.5B) parameter counts where
// the base model often generates malformed output.
// ────────────────────────────────────────────────────────

Console.WriteLine("🎯 Fine-Tuned Tool Calling Demo");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Comparing base vs fine-tuned Qwen2.5-0.5B for tool calling\n");

// ── Load the fine-tuned model ──
// This model was trained specifically for tool calling on
// ElBruno.LocalLLMs' QwenFormatter chat template format.
var fineTunedOptions = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05B_ToolCalling
};

Console.WriteLine($"Model: {fineTunedOptions.Model.DisplayName}");
Console.WriteLine("Loading fine-tuned model (first run downloads from HuggingFace)...\n");

using var fineTunedClient = await LocalChatClient.CreateAsync(fineTunedOptions);

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

var singleResponse = await fineTunedClient.GetResponseAsync(
    singleTurnMessages,
    new ChatOptions { Tools = tools });

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
    Console.WriteLine($"🤖 Model: {singleResponse.Text}");
}

// ══════════════════════════════════════════
// Demo 2: Multi-turn agent loop
// Fine-tuned models handle multi-tool scenarios
// more reliably, calling the right tools in sequence.
// ══════════════════════════════════════════
Console.WriteLine("\n═══ Demo 2: Multi-Turn Agent Loop ═══\n");

await RunAgentLoop(fineTunedClient, tools,
    "What's the weather like in Paris and what is 25 * 4 + 10?");

// ══════════════════════════════════════════
// Demo 3: Complex multi-tool scenario
// ══════════════════════════════════════════
Console.WriteLine("\n═══ Demo 3: Complex Multi-Tool Query ═══\n");

await RunAgentLoop(fineTunedClient, tools,
    "Get the current UTC time, then calculate 100 / 5, and check the weather in Tokyo.");

Console.WriteLine("\n✅ Fine-tuned model demonstrates improved tool calling accuracy!");
Console.WriteLine("\n💡 Tips:");
Console.WriteLine("   • Fine-tuned 0.5B models produce cleaner JSON than base 0.5B");
Console.WriteLine("   • For even better results, try KnownModels.Qwen25_05B_Instruct_FineTuned");
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
            new ChatOptions { Tools = agentTools });

        // Collect any tool calls from the response
        var calls = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .ToList();

        if (calls.Count == 0)
        {
            // No tool calls — model produced a final text answer
            Console.WriteLine($"🤖 Assistant: {response.Text}");
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
