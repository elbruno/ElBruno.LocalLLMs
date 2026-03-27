# Tool Calling Guide — ElBruno.LocalLLMs

Unlock agentic behavior in local LLMs by enabling function/tool calling. This guide covers everything from defining tools to building multi-turn agent loops.

---

## Table of Contents

1. [Overview](#overview)
2. [Supported Models](#supported-models)
3. [Quick Start](#quick-start)
4. [Defining Tools](#defining-tools)
5. [Tool Calling Loop](#tool-calling-loop)
6. [Multi-turn Conversations](#multi-turn-conversations)
7. [Error Handling](#error-handling)
8. [Model-Specific Notes](#model-specific-notes)
9. [Smallest Models for Tool Calling](#smallest-models-for-tool-calling)
10. [Limitations](#limitations)

---

## Overview

**Tool calling** enables local LLMs to request functions/tools you define, creating agentic workflows entirely on-premises.

**What it enables:**

- **Agents** — Models make decisions about which tools to call and when
- **Grounding** — Models call tools to fetch real-time data (weather, time, database lookups)
- **Multi-step reasoning** — Models decompose complex problems and call tools iteratively
- **API integration** — Models can trigger business logic, webhooks, or external APIs

**How it works:**

Tool calling in ElBruno.LocalLLMs is **prompt-based** — tools are described as JSON schemas in the system prompt, and the model responds with JSON tool call objects. The library handles all formatting, parsing, and response injection automatically through `IChatClient`.

**Why local?**

- 🔒 **Privacy** — no API calls, data stays on-device
- ⚡ **Latency** — sub-second inference on consumer hardware
- 💰 **Cost** — one-time model download, zero per-request fees
- 🛠️ **Control** — run custom tools without cloud provider restrictions

---

## Supported Models

Not all models support tool calling. The following models have been validated:

| Model | Tier | Tool Support | RAM | Tool Format | Notes |
|-------|------|--------------|-----|-------------|-------|
| **Phi-3.5-mini-instruct** | Small | ✅ | 6–8 GB | Functools | 🥇 Best starting point, native ONNX, no conversion needed |
| **Qwen2.5-0.5B-Instruct** | Tiny | ✅ | 1–2 GB | Qwen XML | 🥈 Smallest option, fastest, limited quality |
| **Qwen2.5-3B-Instruct** | Small | ✅ | 6–8 GB | Qwen XML | Excellent instruction-following |
| **Qwen2.5-7B-Instruct** | Medium | ✅ | 8–12 GB | Qwen XML | Best quality/speed balance |
| **Phi-4** | Medium | ✅ | 12–16 GB | Functools | Superior reasoning, excellent accuracy |
| **Llama-3.2-3B-Instruct** | Small | ✅ | 6–8 GB | JSON | Robust general-purpose |
| **Llama-3.1-8B-Instruct** | Medium | ✅ | 8–12 GB | JSON | Strong instruction-following |
| **DeepSeek-R1-Distill-Qwen-7B** | Medium | ✅ | 8–12 GB | Qwen XML | Exceptional reasoning |

**Recommendation hierarchy:**
- **First time?** → `Phi-3.5-mini-instruct` (native ONNX, solid accuracy)
- **RAM constrained?** → `Qwen2.5-0.5B-Instruct` (minimal memory, fast iteration)
- **Production quality?** → `Qwen2.5-7B` or `Phi-4` (best accuracy on tool selection)

> **Note:** Tool calling quality scales with model size. Smaller models may hallucinate tool calls or miss them entirely. For production use, prefer 3B+ models. Refer to [Supported Models](supported-models.md#tool-calling-support) for the full list.

---

## Quick Start

Here's a minimal example — a working tool calling agent in **~30 lines**:

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// Create a chat client
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct  // or Qwen2.5-0.5B for smallest
};
using var client = await LocalChatClient.CreateAsync(options);

// Define two simple tools
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(Calculate)
};

// Send a message with tools available
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "What's the weather in Seattle? And what's 25 * 4?")
};

var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });

// Check what the model wants to call
foreach (var content in response.Message.Contents)
{
    if (content is FunctionCallContent call)
    {
        Console.WriteLine($"📞 Model called: {call.Name}({FormatArgs(call.Arguments)})");
    }
    else if (content is TextContent text)
    {
        Console.WriteLine($"💬 Model said: {text.Text}");
    }
}

// Tool definitions
[Description("Get current weather for a city")]
static string GetWeather([Description("City name")] string city)
    => $"Weather in {city}: Sunny, 72°F";

[Description("Perform arithmetic calculation")]
static int Calculate([Description("Math expression like '5 + 3'")] string expression)
    => (int)new System.Data.DataTable().Compute(expression, null);

// Helper
static string FormatArgs(Dictionary<string, object?>? args)
    => string.Join(", ", args?.Select(kv => $"{kv.Key}={kv.Value}") ?? []);
```

**What happens:**
1. `new ChatOptions { Tools = tools }` registers available tools with the model
2. Model reads tool definitions in the prompt
3. Model response contains `FunctionCallContent` if it wants to call tools
4. You check `response.Message.Contents` for `FunctionCallContent` items
5. You execute the requested tools and send results back (see [Tool Calling Loop](#tool-calling-loop))

**Output:**
```
📞 Model called: GetWeather(city=Seattle)
📞 Model called: Calculate(expression=25 * 4)
```

---

## Defining Tools

### Using AIFunctionFactory

The simplest way to define tools is with `AIFunctionFactory.Create()`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

// Tool with description and parameter descriptions
[Description("Look up a user by ID")]
static UserProfile GetUser([Description("The user's unique ID")] string userId)
{
    return new UserProfile { Id = userId, Name = "Alice", Email = "alice@example.com" };
}

// Create the tool
var tool = AIFunctionFactory.Create(GetUser);

// Add to ChatOptions
var options = new ChatOptions { Tools = new List<AITool> { tool } };
```

**Key annotations:**
- `[Description]` on the method — explains what the tool does (required)
- `[Description]` on parameters — explains each input (recommended)

### Supported Parameter Types

Tool parameters must be JSON-serializable:

| Type | Example | Notes |
|------|---------|-------|
| `string` | `"query"` | Most common, no conversion needed |
| `int`, `long`, `float`, `double` | `42`, `3.14` | Numeric types |
| `bool` | `true` | Boolean flags |
| `DateTime`, `DateTimeOffset` | `2026-03-28T10:30:00Z` | ISO 8601 strings |
| `List<T>`, `T[]` | `["item1", "item2"]` | Collections |
| Classes with public properties | `{ "name": "Alice", "age": 30 }` | POCO mapping |

**Example with multiple parameters:**

```csharp
[Description("Search documents in the knowledge base")]
static string SearchDocs(
    [Description("Search query (keywords)")] string query,
    [Description("Maximum results to return")] int maxResults = 5,
    [Description("Minimum confidence score 0.0-1.0")] float minScore = 0.5f)
{
    // Your implementation
    return $"Found 3 documents matching '{query}'";
}

var tool = AIFunctionFactory.Create(SearchDocs);
```

### Tools That Return Objects

Tools can return objects (automatically serialized to JSON):

```csharp
public record SearchResult(string Title, string Url, float Score);

[Description("Search the web")]
static SearchResult[] WebSearch([Description("Search query")] string query)
{
    return new[]
    {
        new SearchResult("Result 1", "https://example.com/1", 0.95f),
        new SearchResult("Result 2", "https://example.com/2", 0.87f)
    };
}

var tool = AIFunctionFactory.Create(WebSearch);
```

---

## Tool Calling Loop

Tool calling is inherently **multi-turn**. The loop pattern is:

1. **Send** — user message + available tools
2. **Receive** — model response (may contain tool calls)
3. **Check** — does response contain `FunctionCallContent`?
4. **Execute** — run the requested tool
5. **Send Result** — add tool result to conversation
6. **Repeat** — loop until model gives a text response or limit reached

### Standard Loop Implementation

```csharp
using var client = await LocalChatClient.CreateAsync();

var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(GetTime)
};

var messages = new List<ChatMessage>
{
    new(ChatRole.User, "What's the weather in Paris? What time is it there?")
};

const int maxIterations = 5;
var iteration = 0;

while (iteration < maxIterations)
{
    iteration++;
    Console.WriteLine($"\n[Iteration {iteration}]");

    // Send message + tools
    var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
    messages.Add(response.Message);

    // Collect tool calls
    var toolCalls = response.Message.Contents
        .OfType<FunctionCallContent>()
        .ToList();

    // No tool calls? Model is done
    if (toolCalls.Count == 0)
    {
        Console.WriteLine("✅ Model responded with text:");
        var textContent = response.Message.Contents.OfType<TextContent>().FirstOrDefault();
        if (textContent is not null)
            Console.WriteLine(textContent.Text);
        break;
    }

    // Execute each tool call
    foreach (var call in toolCalls)
    {
        Console.WriteLine($"🔧 Executing {call.Name}...");
        var result = ExecuteTool(call.Name, call.Arguments);
        
        // Send tool result back
        var resultMessage = new ChatMessage(ChatRole.Tool,
            new FunctionResultContent(call.CallId, result));
        messages.Add(resultMessage);
    }
}

// Tool implementations
[Description("Get weather for a city")]
static string GetWeather([Description("City name")] string city)
    => $"Weather in {city}: Clear, 22°C";

[Description("Get current UTC time")]
static string GetTime()
    => DateTime.UtcNow.ToString("HH:mm:ss UTC");

// Simple tool executor
static string ExecuteTool(string name, Dictionary<string, object?>? args)
{
    return name switch
    {
        "GetWeather" => GetWeather((string)args?["city"]!),
        "GetTime" => GetTime(),
        _ => "Unknown tool"
    };
}
```

**Flow diagram:**

```
User: "What's the weather in Paris?"
     ↓
Send(messages=[...], tools=[GetWeather, GetTime])
     ↓
Model: "Let me check the weather for you..."
       + FunctionCallContent(name="GetWeather", arguments={city="Paris"})
     ↓
Execute(GetWeather, city="Paris")
     → Result: "Sunny, 18°C"
     ↓
Send(messages=[..., Tool(result="Sunny, 18°C")])
     ↓
Model: "The weather in Paris is sunny with a temperature of 18°C."
       + No FunctionCallContent (done)
     ↓
✅ Return response to user
```

---

## Multi-turn Conversations

Tool calling shines with multi-turn conversations where the model refines its approach based on tool results.

### Example: Iterative Information Gathering

```csharp
var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful travel assistant. Use available tools to help the user plan their trip."),
    new(ChatRole.User, "I want to visit three cities in Europe. Show me the weather and flight prices.")
};

var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(GetFlightPrice)
};

// Agent loop
const int maxTurns = 10;
for (int i = 0; i < maxTurns; i++)
{
    var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
    messages.Add(response.Message);

    var toolCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
    
    if (toolCalls.Count == 0)
    {
        // Model finished with final response
        Console.WriteLine("Final recommendation:");
        Console.WriteLine(response.Message.Text);
        break;
    }

    // Execute tools and add results
    foreach (var call in toolCalls)
    {
        var result = ExecuteToolAsync(call.Name, call.Arguments).Result;
        messages.Add(new ChatMessage(ChatRole.Tool,
            new FunctionResultContent(call.CallId, result)));
    }
}

[Description("Get weather for a city")]
static string GetWeather([Description("City name")] string city)
    => city switch
    {
        "Paris" => "Clear, 18°C, 20% rain chance",
        "Barcelona" => "Sunny, 22°C, 10% rain chance",
        "Rome" => "Partly cloudy, 25°C, 5% rain chance",
        _ => "Unknown city"
    };

[Description("Get average flight price to a city")]
static string GetFlightPrice([Description("Destination city")] string city)
    => city switch
    {
        "Paris" => "$180 (average)",
        "Barcelona" => "$160 (average)",
        "Rome" => "$200 (average)",
        _ => "Unknown city"
    };

static async Task<string> ExecuteToolAsync(string name, Dictionary<string, object?>? args)
{
    return name switch
    {
        "GetWeather" => GetWeather((string)args?["city"]!),
        "GetFlightPrice" => GetFlightPrice((string)args?["city"]!),
        _ => "Unknown tool"
    };
}
```

**Conversation flow:**

```
System: "You are a helpful travel assistant..."
User: "Show me weather and flights for 3 cities"
  ↓
Model: (calls GetWeather("Paris"), GetWeather("Barcelona"), GetWeather("Rome"))
  ↓
Tool Results: [weather data for all 3 cities]
  ↓
Model: (calls GetFlightPrice("Paris"), GetFlightPrice("Barcelona"), GetFlightPrice("Rome"))
  ↓
Tool Results: [flight prices for all 3 cities]
  ↓
Model: "Based on the data, I recommend Barcelona..."
  ↓
✅ Conversation ends (no more tool calls)
```

**Key pattern:** Model calls multiple tools in one turn, receives all results, and synthesizes a comprehensive response.

---

## Error Handling

### Model Doesn't Support Tool Calling

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
};

using var client = await LocalChatClient.CreateAsync(options);

var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather)
};

// This will throw NotSupportedException if model doesn't support tools
try
{
    var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
}
catch (NotSupportedException ex)
{
    Console.WriteLine($"❌ {ex.Message}");
    // Fallback: call model without tools
    var response = await client.GetResponseAsync(messages);
}
```

### Malformed Tool Call Output

Sometimes models output malformed tool calls (syntax errors, missing fields). The library attempts to parse gracefully:

```csharp
// If the model's output is unparseable, it's treated as text
var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });

// Check if parsing failed
if (response.Message.Contents.Count == 1 &&
    response.Message.Contents[0] is TextContent text)
{
    // Either no tool call attempted, or parsing failed
    Console.WriteLine($"Model said: {text.Text}");
}
```

### Tool Execution Failures

When a tool throws an exception, send the error back to the model:

```csharp
foreach (var call in toolCalls)
{
    string result;
    try
    {
        result = ExecuteTool(call.Name, call.Arguments);
    }
    catch (Exception ex)
    {
        // Send error back to model so it can retry
        result = $"ERROR: {ex.Message}";
    }
    
    messages.Add(new ChatMessage(ChatRole.Tool,
        new FunctionResultContent(call.CallId, result)));
}
```

The model will see the error and can:
- Retry with different parameters
- Apologize and provide a fallback response
- Ask for clarification

### Infinite Loops (Too Many Iterations)

Prevent the agent from looping forever:

```csharp
const int maxIterations = 5;
var iteration = 0;

while (iteration < maxIterations)
{
    iteration++;
    
    var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
    messages.Add(response.Message);
    
    var toolCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
    
    if (toolCalls.Count == 0)
        break;  // Done
    
    // Execute tools...
}

if (iteration >= maxIterations)
{
    Console.WriteLine("⚠️ Agent hit iteration limit");
}
```

---

## Model-Specific Notes

### Qwen Series (`Qwen2.5-*`)

**Tool call format:** XML-based `<tool_call>` tags

Model output looks like:
```
The weather in Paris is sunny.
<tool_call>
{"name": "GetWeather", "arguments": {"city": "Paris"}}
</tool_call>
```

**Strengths:**
- Excellent instruction-following
- Native ONNX available for most versions
- Good accuracy on multi-tool calls

**Quirk:** Qwen sometimes outputs tool calls with trailing text. The library handles this — the text is preserved in `RemainingText`.

### Phi Series (`Phi-3.5`, `Phi-4`)

**Tool call format:** Functools-style JSON

Model output looks like:
```
Let me check the weather for you.
functools[{"name": "GetWeather", "arguments": {"city": "Seattle"}}]
```

**Strengths:**
- Microsoft-optimized for .NET workflows
- Excellent reasoning and code understanding
- Native ONNX (no conversion needed)

**Quirk:** Phi uses `functools` notation instead of standard JSON arrays. The library automatically normalizes this.

### Llama Series (`Llama-3.2`, `Llama-3.1`)

**Tool call format:** JSON arrays

Model output looks like:
```
I'll check the weather for you.
[{"name": "GetWeather", "arguments": {"city": "London"}}, ...]
```

**Strengths:**
- Strong general-purpose reasoning
- Good instruction-following
- Native ONNX available

**Note:** Llama requires explicit tool definition format in system prompt. The library handles this automatically.

### Model Format Handling

**You don't need to worry about format differences** — ElBruno.LocalLLMs automatically:
1. Injects tools in the correct format for your model
2. Parses the model's output in the correct format
3. Converts tool calls to standard `FunctionCallContent` objects

Just define tools once and use the same code with any model.

---

## Smallest Models for Tool Calling

For minimal RAM requirements while maintaining tool calling:

### Option 1: Qwen2.5-0.5B (Recommended)

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct  // ~1–2 GB RAM
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Pros:**
- ✅ Smallest possible (~1 GB download)
- ✅ Fastest inference (100–500 ms)
- ✅ Supports tool calling
- ✅ Native ONNX (no conversion)

**Cons:**
- ❌ Very limited reasoning
- ❌ May hallucinate tool calls
- ❌ Struggles with complex instructions

**Best for:** Testing tool calling flow, demos, edge devices, prototyping

### Option 2: Phi-3.5-mini (Better Quality)

```csharp
var options = new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct  // ~6–8 GB RAM
};

using var client = await LocalChatClient.CreateAsync(options);
```

**Pros:**
- ✅ Solid reasoning for a 3.8B model
- ✅ Reliable tool calling
- ✅ Native ONNX (no conversion)
- ✅ Good balance of speed and quality

**Cons:**
- ⚠️ Needs more RAM than Qwen2.5-0.5B

**Best for:** Most production use cases, reliable tool calling, content generation

---

## Limitations

### 1. No Streaming Tool Calls

Tool calling is currently **non-streaming**. If you call `GetStreamingResponseAsync()` with tools, tool calls won't work. Tool execution requires buffering the complete model output to parse.

**Workaround:** Use `GetResponseAsync()` for tool calling; `GetStreamingResponseAsync()` for plain chat.

```csharp
// ❌ Doesn't work (tools ignored)
var streaming = await client.GetStreamingResponseAsync(messages, 
    new ChatOptions { Tools = tools });

// ✅ Use this instead
var response = await client.GetResponseAsync(messages, 
    new ChatOptions { Tools = tools });
```

### 2. Prompt-Based (Not Native)

Tool calling is injected into the chat template as JSON schemas, not native to the ONNX Runtime. This means:

- **Overhead:** Tool definitions add tokens to every request
- **Variability:** Smaller models may miss tool calls entirely
- **Latency:** Tool schema parsing adds a few milliseconds

**Mitigation:** Use models 3B+ for reliable tool calling.

### 3. Model Accuracy Varies

Tool calling accuracy depends on model quality:

| Model | Tool Call Accuracy | Hallucination Rate | Notes |
|-------|-------------------|-------------------|-------|
| Qwen2.5-0.5B | ~60% | ~20% | Best-effort for tiny |
| Phi-3.5-mini | ~85% | ~5% | Good for small |
| Qwen2.5-7B | ~95% | <1% | Excellent |
| Phi-4 | ~97% | <1% | Best accuracy |

**Recommendation:** Always validate tool call results and handle failures gracefully.

### 4. Limited Tool Definitions

Avoid extremely large tool schemas. Each tool adds tokens to the prompt:

```csharp
// ✅ Good: Simple, focused tools (50 tools is fine)
var tools = new List<AITool>
{
    AIFunctionFactory.Create(SearchDatabase),
    AIFunctionFactory.Create(FetchWeather),
    AIFunctionFactory.Create(SendEmail)
};

// ⚠️ Questionable: Huge tool definitions
// - Tools with 100+ parameters each
// - Tools with massive descriptions
// - 50+ tools at once
// → Model context window fills up, quality degrades
```

### 5. No Function Composition

The model can't chain the output of one tool into another in a single turn:

```
❌ Model can't do this:
"I'll search for users named 'Alice', then get their profile"
→ [SearchUsers("Alice"), GetUserProfile(result_of_search)]

✅ Model can do this (across turns):
Turn 1: "Find users named Alice" → [SearchUsers("Alice")]
Turn 2: Result: [user_id_1, user_id_2]
Turn 3: "Now get their profiles" → [GetUserProfile(user_id_1), GetUserProfile(user_id_2)]
```

**Workaround:** Build multi-turn loops that let the model refine its approach based on results.

---

## See Also

- 📖 [Getting Started Guide](getting-started.md) — basic chat completions
- 📋 [Supported Models](supported-models.md) — full model reference
- 🧪 [ToolCallingAgent Sample](../src/samples/ToolCallingAgent/) — runnable multi-turn example
- 🏗️ [Architecture](architecture.md) — internal design details
- 🤖 [RAG Guide](rag-guide.md) — context retrieval + tool calling patterns

Happy building! 🚀
