# Tool Description Guide — Best Practices for MCP Tool Routing

> **Purpose:** Writing tool descriptions that produce high-quality embedding matches when used with cosine-similarity-based tool routing.  
> **Author:** Tank (QA), based on Morpheus's embedding analysis  
> **Date:** 2026-03-29  
> **Audience:** Developers registering MCP tools with `MCPToolRouter`

---

## 1. Why Tool Descriptions Matter

In the McpToolRouting sample, the pipeline works as follows:

1. **User prompt** → Qwen2.5-0.5B **distills** it to a single sentence
2. The distilled sentence is **embedded** into a vector
3. Each tool's description is **embedded** into a vector
4. **Cosine similarity** between the prompt vector and each tool vector determines routing

The tool description is the **only** information the router has about what a tool does. A poor description means the router will never select the right tool, regardless of how good the distillation is.

---

## 2. The Golden Rules

### Rule 1: Lead with the Action Verb

The distilled prompt will almost always start with an action verb. Your description should too.

| ❌ Bad | ✅ Good |
|--------|---------|
| "This tool is a weather service" | "Get current weather conditions and forecasts for a location" |
| "Email functionality for the application" | "Send, compose, and deliver emails to recipients" |
| "A calculator" | "Perform mathematical calculations, unit conversions, and formula evaluation" |

**Why:** Embedding models encode word order and position. When both the query ("Get weather for Seattle") and the description ("Get current weather conditions") start with the same action verb, cosine similarity is significantly higher.

### Rule 2: Include Synonyms and Related Verbs

Users express the same intent in many different ways. Cover the common variations.

| Tool | ❌ Narrow Description | ✅ Broad Description |
|------|----------------------|---------------------|
| Email | "Send emails" | "Send, compose, and deliver emails to specified recipients" |
| File Search | "Find files" | "Search for files by name, content, or metadata across local storage" |
| Web Search | "Search the web" | "Search the internet for information, articles, and current events" |

**Why:** If a user says "look up" but your description only says "search," the embedding distance is larger. Including synonyms (search, find, look up, query, locate) creates a richer embedding that captures more of the semantic space.

### Rule 3: Mention Key Nouns / Objects

Include the types of things the tool operates on.

| ❌ Missing Objects | ✅ With Objects |
|-------------------|----------------|
| "Manages scheduling" | "Create, read, update, and delete calendar events and appointments" |
| "Runs code" | "Run code snippets in sandboxed environments and return output" |
| "Handles data" | "Execute SQL queries against connected databases and return results" |

**Why:** The distilled prompt will contain nouns like "appointment," "Python script," or "SQL query." If these nouns appear in your description, the embedding vectors will be closer.

### Rule 4: Keep It Between 10–25 Words

Too short and the embedding lacks signal. Too long and the signal gets diluted.

| Words | Risk |
|-------|------|
| 1–5 | Too sparse — embedding has no context, poor discrimination |
| 6–9 | Borderline — may work for unique tools but risky |
| **10–25** | **Sweet spot — enough signal without dilution** |
| 26–40 | Acceptable if the tool is genuinely complex |
| 41+ | Too diluted — embedding becomes too generic, matches everything weakly |

### Rule 5: Avoid Jargon, Use Plain English

The distilled prompt will be in plain English. Match its register.

| ❌ Jargon | ✅ Plain |
|-----------|---------|
| "RESTful CRUD operations on CalDAV resources" | "Create, read, update, and delete calendar events" |
| "Execute arbitrary POSIX-compliant shell commands" | "Run code snippets and shell commands and return output" |
| "Perform vector similarity search over document embeddings" | "Search documents by meaning and relevance" |

---

## 3. Structural Template

Use this template as a starting point for any new tool description:

```
[Primary verb] [object type(s)] [qualifier: how/where/what kind] [and additional capabilities]
```

**Examples:**

- `Get current weather conditions and forecasts for a given location`
- `Send, compose, and deliver emails to specified recipients`
- `Create, read, update, and delete calendar events and appointments`
- `Search for files by name, content, or metadata across local storage`
- `Perform mathematical calculations, unit conversions, and formula evaluation`
- `Search the internet for information, articles, and current events`
- `Execute SQL queries against connected databases and return results`
- `Run code snippets in sandboxed environments and return output`

---

## 4. Common Anti-Patterns

### Anti-Pattern 1: Marketing Speak

```
❌ "The ultimate all-in-one productivity solution for modern teams"
```
This tells the router nothing about what the tool actually does. Embeddings for marketing phrases are generic and match everything poorly.

### Anti-Pattern 2: Implementation Details

```
❌ "Uses OpenWeatherMap API v3.0 with JWT authentication over HTTPS"
```
Users don't mention implementation details in their prompts. This wastes embedding dimensions on irrelevant tokens.

### Anti-Pattern 3: Negations

```
❌ "Weather tool — does NOT handle indoor temperature readings"
```
Embedding models encode "temperature" regardless of negation. This can cause false matches with temperature/thermostat queries.

### Anti-Pattern 4: Overlapping Descriptions

If two tools have nearly identical descriptions, the router cannot distinguish between them.

```
❌ Tool A: "Search for information and return results"
❌ Tool B: "Search for information and return answers"
```

Instead, differentiate by **what** they search:
```
✅ Tool A: "Search for files by name, content, or metadata across local storage"
✅ Tool B: "Search the internet for information, articles, and current events"
```

### Anti-Pattern 5: Empty or Placeholder Descriptions

```
❌ ""
❌ "TODO"
❌ "Tool description goes here"
```
An empty or placeholder description produces a meaningless embedding. The tool will never be routed to.

---

## 5. Testing Your Descriptions

Before registering a tool, run a quick sanity check:

### Step 1: Write 5 Example Prompts

For your tool, write 5 prompts that a user might say when they need this tool:

```
Tool: Weather Lookup
1. "What's the weather in Seattle?"
2. "Is it going to rain tomorrow?"
3. "Temperature in New York City"
4. "Do I need an umbrella today?"
5. "Weather forecast for this weekend"
```

### Step 2: Check Embedding Distance

Use the embedding model to compute cosine similarity between each prompt and your description. All 5 should score in the **top 3** tools.

### Step 3: Check for False Positives

Also test 3 prompts that should NOT route to your tool:

```
1. "Send an email to John" → should NOT match Weather
2. "Calculate 42 * 17" → should NOT match Weather
3. "Find the budget spreadsheet" → should NOT match Weather
```

If any of these score in the top 2 for your tool, your description is too generic.

### Step 4: Compare Against Neighbors

Run your description against all other registered tools with 10+ test prompts. Look for:
- **Correct tool is #1:** Great
- **Correct tool is #2-3:** Acceptable, but consider refining
- **Correct tool is #4+:** Description needs rework

---

## 6. Embedding Model Considerations

The McpToolRouting sample uses `ElBruno.LocalEmbeddings` for embedding generation. Key characteristics that affect description writing:

| Property | Impact on Descriptions |
|----------|----------------------|
| **768-dimensional vectors** | Rich enough to capture nuance — don't oversimplify |
| **Max ~512 tokens** | Descriptions under 50 words are well within limits |
| **Sentence-level optimization** | Write descriptions as complete sentences/phrases, not keyword lists |
| **English-optimized** | Write descriptions in English even if the tool handles other languages |
| **Case-insensitive** (practically) | Don't rely on capitalization for disambiguation |

---

## 7. Reference Descriptions

The benchmark suite (`distillation-benchmarks.md`) uses these 8 tool descriptions as the reference set. They follow all the guidelines above:

| Tool | Description | Word Count |
|------|-------------|------------|
| Weather Lookup | Get current weather conditions and forecasts for a given location | 11 |
| Email Sender | Compose and send emails to specified recipients | 8 |
| Calendar Manager | Create, read, update, and delete calendar events and appointments | 10 |
| File Search | Search for files by name, content, or metadata across local storage | 12 |
| Calculator | Perform mathematical calculations, unit conversions, and formula evaluation | 9 |
| Web Search | Search the internet for information, articles, and current events | 10 |
| Database Query | Execute SQL queries against connected databases and return results | 10 |
| Code Execution | Run code snippets in sandboxed environments and return output | 10 |

Average: ~10 words per description. All start with an action verb. All mention the primary object type.

---

## 8. Checklist for New Tools

Before registering a new MCP tool with `MCPToolRouter`, verify:

- [ ] Description starts with an action verb
- [ ] Description is 10–25 words
- [ ] Key nouns/objects the tool operates on are mentioned
- [ ] At least 2 synonym verbs are included
- [ ] No jargon or implementation details
- [ ] No negations
- [ ] Description is sufficiently different from all existing tool descriptions
- [ ] 5 sample prompts all route to this tool in top-3
- [ ] 3 unrelated prompts do NOT route to this tool in top-2
