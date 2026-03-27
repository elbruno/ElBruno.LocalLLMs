using System.ComponentModel;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// ────────────────────────────────────────────────────────
// ToolCallingAgent — demonstrates tool/function calling
// with a local LLM using the agent loop pattern.
// ────────────────────────────────────────────────────────

// Use Qwen2.5-0.5B — smallest model with tool calling support (~1 GB).
// For better quality, use KnownModels.Phi35MiniInstruct or KnownModels.Qwen25_7BInstruct.
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct
};

Console.WriteLine("🔧 ToolCallingAgent — Local LLM Function Calling Demo");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"Model: {options.Model.DisplayName}");
Console.WriteLine("Loading model (first run downloads ~1 GB)...\n");

using var client = await LocalChatClient.CreateAsync(options);

// ── Define tools using AIFunctionFactory ──
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetCurrentTime),
    AIFunctionFactory.Create(Calculate),
    AIFunctionFactory.Create(GetWeather)
};

// ══════════════════════════════════════════
// Demo 1: Single-turn tool call
// ══════════════════════════════════════════
Console.WriteLine("═══ Demo 1: Single-Turn Tool Call ═══\n");

var singleTurnMessages = new List<ChatMessage>
{
    new(ChatRole.User, "What time is it in UTC?")
};

Console.WriteLine($"👤 User: {singleTurnMessages[0].Text}");

var singleResponse = await client.GetResponseAsync(
    singleTurnMessages,
    new ChatOptions { Tools = tools });

// Check if the model requested a tool call
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
// ══════════════════════════════════════════
Console.WriteLine("\n═══ Demo 2: Multi-Turn Agent Loop ═══\n");

await RunAgentLoop(client, tools,
    "What's the weather like in Paris and what is 25 * 4 + 10?");

Console.WriteLine("\n═══ Demo 3: Another Agent Loop ═══\n");

await RunAgentLoop(client, tools,
    "What time is it in Tokyo? Also, calculate 15 + 27.");

Console.WriteLine("\n✅ Done!");

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
// Tool definitions
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
