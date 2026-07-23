using System.ComponentModel;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// ────────────────────────────────────────────────────────
// MagenticBrainAgent — demonstrates the OmniAgent round-based
// agentic loop using the Qwen3 chat template format.
//
// Models supported:
//   KnownModels.Qwen3_14BInstruct  — native ONNX, CPU INT4 (~8 GB)
//   KnownModels.MagenticBrain      — requires ONNX conversion first
//                                    (see docs/onnx-conversion.md)
//
// Key Qwen3 settings for reliable tool calling:
//   Temperature = 0.7, TopK = 20, MaxSequenceLength = 32768
//   Non-thinking mode is enforced by Qwen3Formatter (empty <think> block)
// ────────────────────────────────────────────────────────

var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen3_14BInstruct,
    Temperature = 0.7f,
    MaxSequenceLength = 32768,
};

Console.WriteLine("🧠 MagenticBrainAgent — Qwen3 Agentic Tool-Calling Demo");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"Model: {options.Model.DisplayName}");
Console.WriteLine($"Template: {options.Model.ChatTemplate}");
Console.WriteLine("Loading model (first run downloads from HuggingFace)...\n");

using var client = await LocalChatClient.CreateAsync(options);

// ── Define tools ──────────────────────────────────────────
// "submit" is the stop-signal: when the model calls it, the loop ends.
var tools = new List<AITool>
{
    AIFunctionFactory.Create(ReadFile),
    AIFunctionFactory.Create(ListDirectory),
    AIFunctionFactory.Create(Submit),
};

// ══════════════════════════════════════════════════════════
// OmniAgent round-based loop
// build → call LLM → parse tool calls → execute → feed back → repeat
// Loop ends when the model calls the "submit" tool.
// ══════════════════════════════════════════════════════════

var task = "List the files in the current directory, then read the README.md file and submit a one-sentence summary of the project.";

Console.WriteLine($"📋 Task: {task}\n");

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful agentic assistant. Use the available tools to complete the task. When you have gathered all necessary information and are ready to deliver your final answer, call the submit tool with your result."),
    new(ChatRole.User, task)
};

const int maxRounds = 10;
var submitted = false;
string? submitResult = null;

for (int round = 1; round <= maxRounds && !submitted; round++)
{
    Console.WriteLine($"── Round {round} ──────────────────────────────────────────");

    var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });

    // Collect tool calls from the response
    var calls = response.Messages
        .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
        .ToList();

    if (calls.Count == 0)
    {
        // No tool calls — model gave a direct text answer
        Console.WriteLine($"🤖 Assistant: {response.Text}");
        break;
    }

    // Add the assistant turn (with tool calls) to conversation history
    messages.AddRange(response.Messages);

    Console.WriteLine($"   ⚙️  Model requested {calls.Count} tool call(s):");

    var resultContents = new List<AIContent>();
    foreach (var call in calls)
    {
        Console.WriteLine($"      → {call.Name}({FormatArgs(call.Arguments)})");

        // Check for stop signal
        if (call.Name == nameof(Submit))
        {
            submitted = true;
            submitResult = call.Arguments?.TryGetValue("result", out var r) == true
                ? r?.ToString()
                : "(no result provided)";
            resultContents.Add(new FunctionResultContent(call.CallId, "Submitted successfully."));
            Console.WriteLine($"      ← Submitted.");
            continue;
        }

        var tool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == call.Name);
        object? result = tool is not null
            ? await tool.InvokeAsync(call.Arguments is null ? null : new AIFunctionArguments(call.Arguments))
            : $"Unknown tool: {call.Name}";

        Console.WriteLine($"      ← {TruncateForDisplay(result?.ToString())}");
        resultContents.Add(new FunctionResultContent(call.CallId, result?.ToString() ?? ""));
    }

    // Feed tool results back as a tool role message
    messages.Add(new ChatMessage(ChatRole.Tool, resultContents));
}

Console.WriteLine();
if (submitted)
{
    Console.WriteLine("✅ Agent submitted final answer:");
    Console.WriteLine($"   {submitResult}");
}
else
{
    Console.WriteLine("⚠️  Agent reached max rounds without submitting.");
}

// ────────────────────────────────────────────────────────
// Tool definitions
// ────────────────────────────────────────────────────────

[Description("Reads the contents of a file at the given path.")]
static string ReadFile(
    [Description("Relative or absolute path to the file")] string path)
{
    try
    {
        if (!File.Exists(path))
            return $"Error: file not found: {path}";

        var text = File.ReadAllText(path);
        // Truncate very large files
        return text.Length > 4000 ? text[..4000] + "\n...[truncated]" : text;
    }
    catch (Exception ex)
    {
        return $"Error reading file: {ex.Message}";
    }
}

[Description("Lists files and directories at the given path. Defaults to the current directory.")]
static string ListDirectory(
    [Description("Directory path to list. Use '.' for the current directory.")] string path = ".")
{
    try
    {
        if (!Directory.Exists(path))
            return $"Error: directory not found: {path}";

        var entries = Directory.GetFileSystemEntries(path)
            .Select(e => Path.GetFileName(e) + (Directory.Exists(e) ? "/" : ""))
            .OrderBy(e => e);

        return string.Join("\n", entries);
    }
    catch (Exception ex)
    {
        return $"Error listing directory: {ex.Message}";
    }
}

[Description("Submits the final result. Call this when you have completed the task and are ready to deliver your answer.")]
static string Submit(
    [Description("The final answer or summary to deliver")] string result)
{
    // This tool acts as a stop signal — the agent loop detects calls to it.
    // Return value is sent back to the model but the loop will terminate.
    return $"Result received: {result}";
}

static string FormatArgs(IDictionary<string, object?>? args) =>
    args is null ? "" : string.Join(", ", args.Select(kv => $"{kv.Key}: \"{kv.Value}\""));

static string TruncateForDisplay(string? text, int maxLength = 120) =>
    text is null ? "(null)"
    : text.Length <= maxLength ? text
    : text[..maxLength] + "...";
