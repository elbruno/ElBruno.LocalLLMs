# ElBruno.LocalLLMs — Phase 4: RAG + Tool Routing Architecture Plan

> **Version:** 1.0 — Phase 4 Feature Plan  
> **Author:** Morpheus (Lead/Architect)  
> **Date:** 2026-03-18  
> **Status:** Ready for implementation

---

## 1. Overview

Phase 4 adds two critical capabilities to ElBruno.LocalLLMs:

### Phase 4a: Tool Calling (Tool Routing)

Enable `LocalChatClient` to support function/tool calling through **prompt-based tool routing**. Since ONNX Runtime GenAI doesn't have native function calling support, we implement this by:

- Injecting tool definitions into the chat template
- Parsing the model's text output to detect tool call patterns
- Converting detected tool calls into `FunctionCallContent` items
- Handling `FunctionResultContent` in subsequent messages

### Phase 4b: RAG Pipeline

Provide a clean integration path for Retrieval-Augmented Generation:

- Core abstractions for document storage, chunking, and retrieval
- Integration with `ElBruno.LocalEmbeddings` for vector embeddings
- Sample implementations (in-memory and pluggable stores)
- Clear patterns for context injection into chat messages

**Why this matters:** Tool calling enables agents to take actions (query databases, call APIs, perform calculations). RAG enables grounding LLM responses in private/domain-specific knowledge. Together, they unlock real-world agentic applications running entirely on-premises.

---

## 2. Phase 4a: Tool Calling Architecture

### 2.1 Public API Changes

#### ChatOptions Support

`LocalChatClient` currently ignores `ChatOptions.Tools`. We'll extract and use:

```csharp
// User code
var options = new ChatOptions
{
    Tools = new List<AITool>
    {
        AIFunctionFactory.Create(GetWeather),
        AIFunctionFactory.Create(GetTime)
    },
    ToolMode = ChatToolMode.Auto,  // or RequireAny, None, RequireSpecific
    AllowMultipleToolCalls = true
};

var response = await client.GetResponseAsync(messages, options);

// Response may contain FunctionCallContent
foreach (var item in response.Message.Contents)
{
    if (item is FunctionCallContent call)
    {
        // Execute tool, send result back
        var result = await ExecuteToolAsync(call.Name, call.Arguments);
        messages.Add(new ChatMessage(ChatRole.Tool, 
            new FunctionResultContent(call.CallId, result)));
    }
}
```

#### Model Capability Flags

Add to `ModelDefinition`:

```csharp
public sealed record ModelDefinition
{
    // ... existing properties ...
    
    /// <summary>
    /// Whether this model supports prompt-based tool calling.
    /// </summary>
    public bool SupportsToolCalling { get; init; }
    
    /// <summary>
    /// Tool calling format variant (if supported).
    /// </summary>
    public ToolCallingFormat? ToolFormat { get; init; }
}

public enum ToolCallingFormat
{
    QwenHermes,      // <tool_call>...</tool_call>
    Llama3Json,      // {"name":"...","arguments":{...}}
    Llama3Pythonic,  // [func(param=value), ...]
    Phi4Functools,   // functools[{...}]
    ChatMLJson       // JSON blocks in ChatML
}
```

#### Error Handling

If `ChatOptions.Tools` is provided but `ModelDefinition.SupportsToolCalling` is false:

```csharp
throw new NotSupportedException(
    $"Model '{model.Id}' does not support tool calling. " +
    "Choose a tool-capable model like Qwen2.5-7B-Instruct, Llama-3.2-3B-Instruct, or Phi-4.");
```

### 2.2 Template Formatter Changes

#### Updated Interface

```csharp
internal interface IChatTemplateFormatter
{
    /// <summary>
    /// Formats messages into the model's prompt format.
    /// </summary>
    string FormatMessages(IList<ChatMessage> messages);
    
    /// <summary>
    /// Formats messages with tool definitions injected.
    /// Only implemented for tool-capable formatters.
    /// </summary>
    string FormatMessagesWithTools(
        IList<ChatMessage> messages, 
        IList<AITool> tools,
        ChatToolMode toolMode);
    
    /// <summary>
    /// Parses model output to detect tool call patterns.
    /// Returns null if no tool calls detected.
    /// </summary>
    ToolCallParseResult? ParseToolCalls(string modelOutput);
}

internal sealed record ToolCallParseResult(
    IReadOnlyList<ParsedToolCall> ToolCalls,
    string? RemainingText  // Any text before/after tool calls
);

internal sealed record ParsedToolCall(
    string Name,
    string ArgumentsJson,
    string CallId  // Generated if model doesn't provide one
);
```

#### Default Implementation (No Tool Support)

```csharp
internal abstract class ChatTemplateFormatterBase : IChatTemplateFormatter
{
    public abstract string FormatMessages(IList<ChatMessage> messages);
    
    public virtual string FormatMessagesWithTools(
        IList<ChatMessage> messages, 
        IList<AITool> tools,
        ChatToolMode toolMode)
    {
        throw new NotSupportedException(
            $"{GetType().Name} does not support tool calling.");
    }
    
    public virtual ToolCallParseResult? ParseToolCalls(string modelOutput)
    {
        return null;  // No tool calls detected
    }
}
```

### 2.3 Tool-Aware Formatters

#### Qwen Formatter (Hermes-Style)

```csharp
internal sealed class QwenFormatter : ChatTemplateFormatterBase
{
    public override string FormatMessagesWithTools(
        IList<ChatMessage> messages, 
        IList<AITool> tools,
        ChatToolMode toolMode)
    {
        var sb = new StringBuilder();
        
        // System message with tool definitions
        sb.Append("<|im_start|>system\n");
        sb.Append("You are a function calling AI model. ");
        sb.Append("You have access to the following tools:\n\n");
        sb.Append("<tools>\n");
        
        foreach (var tool in tools)
        {
            var toolJson = SerializeToolToJson(tool);
            sb.Append(toolJson).Append('\n');
        }
        
        sb.Append("</tools>\n\n");
        sb.Append("To call a function, respond with XML tags:\n");
        sb.Append("<tool_call>\n");
        sb.Append("{\"name\": \"function_name\", \"arguments\": {\"param\": \"value\"}}\n");
        sb.Append("</tool_call>\n");
        
        if (toolMode == ChatToolMode.RequireAny)
        {
            sb.Append("You MUST call at least one tool.\n");
        }
        
        sb.Append("<|im_end|>\n");
        
        // Format remaining messages
        foreach (var message in messages.Where(m => m.Role != ChatRole.System))
        {
            sb.Append(FormatSingleMessage(message));
        }
        
        sb.Append("<|im_start|>assistant\n");
        return sb.ToString();
    }
    
    public override ToolCallParseResult? ParseToolCalls(string modelOutput)
    {
        // Match: <tool_call>\n{...}\n</tool_call>
        var pattern = @"<tool_call>\s*(\{.*?\})\s*</tool_call>";
        var matches = Regex.Matches(modelOutput, pattern, RegexOptions.Singleline);
        
        if (matches.Count == 0)
            return null;
        
        var calls = new List<ParsedToolCall>();
        foreach (Match match in matches)
        {
            var json = match.Groups[1].Value;
            var parsed = JsonSerializer.Deserialize<ToolCallJson>(json);
            
            calls.Add(new ParsedToolCall(
                Name: parsed.Name,
                ArgumentsJson: JsonSerializer.Serialize(parsed.Arguments),
                CallId: GenerateCallId()
            ));
        }
        
        // Extract remaining text (text before/after tool calls)
        var remainingText = Regex.Replace(
            modelOutput, 
            pattern, 
            "", 
            RegexOptions.Singleline).Trim();
        
        return new ToolCallParseResult(calls, remainingText);
    }
    
    private record ToolCallJson(string Name, JsonElement Arguments);
}
```

#### Llama3 Formatter (JSON Style)

```csharp
internal sealed class Llama3Formatter : ChatTemplateFormatterBase
{
    public override string FormatMessagesWithTools(
        IList<ChatMessage> messages, 
        IList<AITool> tools,
        ChatToolMode toolMode)
    {
        // Inject into system message
        var systemMsg = "You have access to the following functions:\n\n";
        
        foreach (var tool in tools)
        {
            systemMsg += $"- {tool.Metadata.Name}: {tool.Metadata.Description}\n";
            systemMsg += $"  Parameters: {SerializeParameterSchema(tool)}\n\n";
        }
        
        systemMsg += "To call a function, respond with JSON:\n";
        systemMsg += "{\"name\": \"function_name\", \"parameters\": {\"param\": \"value\"}}\n";
        
        if (toolMode == ChatToolMode.RequireAny)
        {
            systemMsg += "You MUST call a function to answer.\n";
        }
        
        // Build prompt
        var sb = new StringBuilder();
        sb.Append("<|begin_of_text|>");
        sb.Append($"<|start_header_id|>system<|end_header_id|>\n\n{systemMsg}<|eot_id|>");
        
        // ... rest of messages ...
        
        return sb.ToString();
    }
    
    public override ToolCallParseResult? ParseToolCalls(string modelOutput)
    {
        // Try to parse as JSON object or array
        var trimmed = modelOutput.Trim();
        
        // Single call: {"name":"...","parameters":{...}}
        if (TryParseJsonToolCall(trimmed, out var singleCall))
        {
            return new ToolCallParseResult(
                new[] { singleCall },
                RemainingText: null
            );
        }
        
        // Multiple calls: [{...}, {...}]
        if (TryParseJsonToolCallArray(trimmed, out var multipleCalls))
        {
            return new ToolCallParseResult(multipleCalls, null);
        }
        
        return null;
    }
}
```

#### Phi4 Formatter (Functools Style)

```csharp
internal sealed class Phi4Formatter : ChatTemplateFormatterBase
{
    public override ToolCallParseResult? ParseToolCalls(string modelOutput)
    {
        // Match: functools[{...}] or functools[{...}, {...}]
        var pattern = @"functools\[(.*?)\]";
        var match = Regex.Match(modelOutput, pattern, RegexOptions.Singleline);
        
        if (!match.Success)
            return null;
        
        var jsonArray = match.Groups[1].Value;
        var parsed = JsonSerializer.Deserialize<ToolCallJson[]>($"[{jsonArray}]");
        
        var calls = parsed.Select(p => new ParsedToolCall(
            Name: p.Name,
            ArgumentsJson: JsonSerializer.Serialize(p.Arguments),
            CallId: GenerateCallId()
        )).ToList();
        
        var remainingText = modelOutput.Replace(match.Value, "").Trim();
        
        return new ToolCallParseResult(calls, remainingText);
    }
}
```

### 2.4 LocalChatClient Integration

#### Modified GetResponseAsync

```csharp
public async Task<ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentNullException.ThrowIfNull(messages);

    await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

    var messageList = messages as IList<ChatMessage> ?? messages.ToList();
    var genParams = BuildGenerationParameters(options);
    
    // Check for tool calling
    var hasTools = options?.Tools?.Count > 0;
    
    if (hasTools && !_options.Model.SupportsToolCalling)
    {
        throw new NotSupportedException(
            $"Model '{_options.Model.Id}' does not support tool calling.");
    }
    
    // Format prompt with or without tools
    string prompt;
    if (hasTools)
    {
        prompt = _formatter.FormatMessagesWithTools(
            messageList, 
            options.Tools, 
            options.ToolMode ?? ChatToolMode.Auto);
    }
    else
    {
        prompt = _formatter.FormatMessages(messageList);
    }

    // Generate response
    var responseText = await Task.Run(
        () => _model!.Generate(prompt, genParams, cancellationToken),
        cancellationToken).ConfigureAwait(false);

    // Parse for tool calls
    ChatMessage responseMessage;
    
    if (hasTools)
    {
        var parseResult = _formatter.ParseToolCalls(responseText);
        
        if (parseResult is not null)
        {
            // Build multi-content message
            var contents = new List<AIContent>();
            
            if (!string.IsNullOrWhiteSpace(parseResult.RemainingText))
            {
                contents.Add(new TextContent(parseResult.RemainingText));
            }
            
            foreach (var call in parseResult.ToolCalls)
            {
                contents.Add(new FunctionCallContent(
                    callId: call.CallId,
                    name: call.Name,
                    arguments: ParseArgumentsAsDict(call.ArgumentsJson)
                ));
            }
            
            responseMessage = new ChatMessage(ChatRole.Assistant, contents);
        }
        else
        {
            // No tool calls detected - plain text response
            responseMessage = new ChatMessage(ChatRole.Assistant, responseText.Trim());
        }
    }
    else
    {
        responseMessage = new ChatMessage(ChatRole.Assistant, responseText.Trim());
    }

    return new ChatResponse(responseMessage)
    {
        ModelId = _options.Model.Id,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
```

#### Handling Tool Results in Subsequent Messages

Tool results come back as `ChatRole.Tool` messages with `FunctionResultContent`. The formatter must convert these into the model's expected format.

```csharp
// In formatter
private string FormatSingleMessage(ChatMessage message)
{
    if (message.Role == ChatRole.Tool)
    {
        // Extract FunctionResultContent
        var resultContent = message.Contents
            .OfType<FunctionResultContent>()
            .FirstOrDefault();
        
        if (resultContent is not null)
        {
            // Qwen format:
            // <|im_start|>tool
            // <tool_response>
            // {"result": "..."}
            // </tool_response>
            // <|im_end|>
            
            var resultJson = JsonSerializer.Serialize(new { result = resultContent.Result });
            return $"<|im_start|>tool\n<tool_response>\n{resultJson}\n</tool_response>\n<|im_end|>\n";
        }
    }
    
    // ... handle other roles ...
}
```

### 2.5 Model Support Matrix (Phase 1)

| Model                   | Tier       | Tool Support | Format          | Status        |
|-------------------------|------------|--------------|-----------------|---------------|
| Qwen2.5-3B-Instruct     | 🟢 Small   | ✅ Yes       | QwenHermes      | Phase 4a      |
| Qwen2.5-7B-Instruct     | 🔵 Medium  | ✅ Yes       | QwenHermes      | Phase 4a      |
| Llama-3.2-3B-Instruct   | 🟢 Small   | ✅ Yes       | Llama3Json      | Phase 4a      |
| Phi-4                   | 🔵 Medium  | ✅ Yes       | Phi4Functools   | Phase 4a      |
| Phi-3.5-mini-instruct   | ⚪ Tiny    | ❌ No        | -               | -             |
| DeepSeek-R1-Distill-Qwen-7B | 🔵 Medium | ✅ Yes   | QwenHermes      | Phase 4a      |

### 2.6 Streaming Support

Streaming tool calls is complex because:

1. Tool call patterns may span multiple tokens
2. We need to buffer until we can parse a complete tool call
3. Some models emit partial JSON that becomes valid later

**Phase 4a decision:** Tool calling is **non-streaming only**. If `GetStreamingResponseAsync` is called with `ChatOptions.Tools`, we either:

- Throw `NotSupportedException` (conservative)
- Fall back to `GetResponseAsync` internally (user-friendly)

Future enhancement: Add streaming tool call support with buffered parsing.

---

## 3. Phase 4b: RAG Pipeline Architecture

### 3.1 Package Structure Decision

**Option A:** Core library (`ElBruno.LocalLLMs` includes basic RAG)  
**Option B:** Extension package (`ElBruno.LocalLLMs.Rag`)

**Decision:** Extension package `ElBruno.LocalLLMs.Rag`

**Rationale:**

- RAG is optional — not all users need it
- Keeps core library focused on chat completions
- Allows separate versioning and dependencies
- Follows pattern from LocalEmbeddings (`.VectorData`, `.KernelMemory`)
- Users can plug in their own RAG implementations

### 3.2 Core Abstractions

```csharp
namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Represents a document to be indexed for RAG.
/// </summary>
public sealed record Document(
    string Id,
    string Content,
    IDictionary<string, object>? Metadata = null
);

/// <summary>
/// Represents a chunk of a document with its embedding.
/// </summary>
public sealed record DocumentChunk(
    string Id,
    string DocumentId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IDictionary<string, object>? Metadata = null
);

/// <summary>
/// Chunks documents into smaller pieces for embedding.
/// </summary>
public interface IDocumentChunker
{
    IEnumerable<DocumentChunk> ChunkDocument(
        Document document,
        int chunkSize = 512,
        int overlapSize = 50);
}

/// <summary>
/// Stores and retrieves document chunks with their embeddings.
/// </summary>
public interface IDocumentStore
{
    Task AddAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default);
    
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the RAG pipeline: chunking, embedding, storage, retrieval.
/// </summary>
public interface IRagPipeline
{
    Task IndexDocumentsAsync(
        IEnumerable<Document> documents,
        IProgress<RagIndexProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<RagContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context retrieved from RAG pipeline to inject into chat.
/// </summary>
public sealed record RagContext(
    string Query,
    IReadOnlyList<DocumentChunk> RetrievedChunks,
    string FormattedContext  // Ready to inject into system/user message
);
```

### 3.3 Default Implementations

#### Simple Document Chunker

```csharp
public sealed class SlidingWindowChunker : IDocumentChunker
{
    public IEnumerable<DocumentChunk> ChunkDocument(
        Document document,
        int chunkSize = 512,
        int overlapSize = 50)
    {
        var content = document.Content;
        var chunkIndex = 0;
        var position = 0;
        
        while (position < content.Length)
        {
            var end = Math.Min(position + chunkSize, content.Length);
            var chunkContent = content.Substring(position, end - position);
            
            yield return new DocumentChunk(
                Id: $"{document.Id}_chunk_{chunkIndex}",
                DocumentId: document.Id,
                Content: chunkContent,
                Embedding: ReadOnlyMemory<float>.Empty,  // Set during indexing
                Metadata: document.Metadata
            );
            
            chunkIndex++;
            position += (chunkSize - overlapSize);
        }
    }
}
```

#### In-Memory Vector Store

```csharp
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly List<DocumentChunk> _chunks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task AddAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _chunks.AddRange(chunks);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var results = _chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Similarity = CosineSimilarity(queryEmbedding, chunk.Embedding)
                })
                .Where(r => r.Similarity >= minSimilarity)
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .Select(r => r.Chunk)
                .ToList();
            
            return results;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _chunks.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    private static float CosineSimilarity(
        ReadOnlyMemory<float> a, 
        ReadOnlyMemory<float> b)
    {
        var spanA = a.Span;
        var spanB = b.Span;
        
        if (spanA.Length != spanB.Length)
            throw new ArgumentException("Vectors must have same dimension");
        
        float dot = 0, magA = 0, magB = 0;
        
        for (int i = 0; i < spanA.Length; i++)
        {
            dot += spanA[i] * spanB[i];
            magA += spanA[i] * spanA[i];
            magB += spanB[i] * spanB[i];
        }
        
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }
}
```

#### RAG Pipeline Implementation

```csharp
public sealed class LocalRagPipeline : IRagPipeline
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentStore _store;
    
    public LocalRagPipeline(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IDocumentChunker? chunker = null,
        IDocumentStore? store = null)
    {
        _embeddingGenerator = embeddingGenerator;
        _chunker = chunker ?? new SlidingWindowChunker();
        _store = store ?? new InMemoryDocumentStore();
    }
    
    public async Task IndexDocumentsAsync(
        IEnumerable<Document> documents,
        IProgress<RagIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var documentList = documents.ToList();
        var totalDocs = documentList.Count;
        var processedDocs = 0;
        
        foreach (var doc in documentList)
        {
            // Chunk document
            var chunks = _chunker.ChunkDocument(doc).ToList();
            
            // Generate embeddings for all chunks
            var chunkTexts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingGenerator.GenerateAsync(
                chunkTexts,
                cancellationToken: cancellationToken);
            
            // Attach embeddings to chunks
            var embeddedChunks = chunks
                .Zip(embeddings, (chunk, embedding) => chunk with 
                { 
                    Embedding = embedding.Vector 
                })
                .ToList();
            
            // Store chunks
            await _store.AddAsync(embeddedChunks, cancellationToken);
            
            processedDocs++;
            progress?.Report(new RagIndexProgress(processedDocs, totalDocs));
        }
    }
    
    public async Task<RagContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Embed the query
        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(
            query,
            cancellationToken: cancellationToken);
        
        // Retrieve similar chunks
        var chunks = await _store.SearchAsync(
            queryEmbedding.Vector,
            topK,
            minSimilarity: 0.3f,  // Configurable threshold
            cancellationToken);
        
        // Format context for injection
        var formattedContext = FormatRetrievedContext(chunks);
        
        return new RagContext(query, chunks, formattedContext);
    }
    
    private static string FormatRetrievedContext(IReadOnlyList<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
            return "No relevant context found.";
        
        var sb = new StringBuilder();
        sb.AppendLine("Relevant context:");
        sb.AppendLine();
        
        for (int i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {chunks[i].Content}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
```

### 3.4 Integration with LocalChatClient

RAG pipeline is independent of `LocalChatClient`. Integration happens at the application level:

```csharp
// Setup
var embeddingGenerator = new LocalEmbeddingGenerator();
var ragPipeline = new LocalRagPipeline(embeddingGenerator);
var chatClient = new LocalChatClient(new LocalLLMsOptions 
{ 
    Model = KnownModels.Qwen2_5_7B_Instruct 
});

// Index documents
var documents = new[]
{
    new Document("doc1", "Company policy: Remote work is allowed 3 days per week."),
    new Document("doc2", "Annual leave: 25 days per year, must be booked 2 weeks in advance."),
};

await ragPipeline.IndexDocumentsAsync(documents);

// Query with RAG
var userQuery = "What's the remote work policy?";

var context = await ragPipeline.RetrieveContextAsync(userQuery, topK: 3);

var messages = new List<ChatMessage>
{
    new(ChatRole.System, 
        "You are a helpful assistant. Answer using the provided context.\n\n" + 
        context.FormattedContext),
    new(ChatRole.User, userQuery)
};

var response = await chatClient.GetResponseAsync(messages);
Console.WriteLine(response.Message.Text);
```

### 3.5 Extension: Persistent Store with SQLite

For production scenarios, an in-memory store isn't sufficient. We'll provide a SQLite-based implementation:

```csharp
public sealed class SqliteDocumentStore : IDocumentStore, IAsyncDisposable
{
    private readonly string _connectionString;
    
    public SqliteDocumentStore(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
        InitializeDatabaseAsync().GetAwaiter().GetResult();
    }
    
    private async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var createTable = @"
            CREATE TABLE IF NOT EXISTS document_chunks (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                content TEXT NOT NULL,
                embedding BLOB NOT NULL,
                metadata TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_document_id 
                ON document_chunks(document_id);
        ";
        
        using var command = new SqliteCommand(createTable, connection);
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task AddAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        foreach (var chunk in chunks)
        {
            var insert = @"
                INSERT OR REPLACE INTO document_chunks 
                (id, document_id, content, embedding, metadata)
                VALUES (@id, @documentId, @content, @embedding, @metadata)
            ";
            
            using var command = new SqliteCommand(insert, connection, transaction);
            command.Parameters.AddWithValue("@id", chunk.Id);
            command.Parameters.AddWithValue("@documentId", chunk.DocumentId);
            command.Parameters.AddWithValue("@content", chunk.Content);
            command.Parameters.AddWithValue("@embedding", 
                MemoryMarshal.AsBytes(chunk.Embedding.Span).ToArray());
            command.Parameters.AddWithValue("@metadata", 
                chunk.Metadata is not null 
                    ? JsonSerializer.Serialize(chunk.Metadata) 
                    : DBNull.Value);
            
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        await transaction.CommitAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        // SQLite doesn't have native vector similarity
        // Need to retrieve all and compute similarity in-memory
        // For large datasets, use a proper vector DB or extension
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var select = "SELECT id, document_id, content, embedding, metadata FROM document_chunks";
        using var command = new SqliteCommand(select, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var results = new List<(DocumentChunk Chunk, float Similarity)>();
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var embeddingBlob = (byte[])reader["embedding"];
            var embedding = MemoryMarshal.Cast<byte, float>(embeddingBlob).ToArray();
            
            var chunk = new DocumentChunk(
                Id: reader.GetString(0),
                DocumentId: reader.GetString(1),
                Content: reader.GetString(2),
                Embedding: embedding,
                Metadata: reader.IsDBNull(4) 
                    ? null 
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(4))
            );
            
            var similarity = CosineSimilarity(queryEmbedding, chunk.Embedding);
            
            if (similarity >= minSimilarity)
            {
                results.Add((chunk, similarity));
            }
        }
        
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .Select(r => r.Chunk)
            .ToList();
    }
    
    // ... CosineSimilarity, ClearAsync, DisposeAsync ...
}
```

### 3.6 Dependency Injection Support

```csharp
public static class RagServiceExtensions
{
    public static IServiceCollection AddLocalRag(
        this IServiceCollection services,
        Action<RagOptions>? configure = null)
    {
        var options = new RagOptions();
        configure?.Invoke(options);
        
        services.AddSingleton<IDocumentChunker>(
            options.Chunker ?? new SlidingWindowChunker());
        
        services.AddSingleton<IDocumentStore>(sp =>
        {
            if (options.Store is not null)
                return options.Store;
            
            if (options.UseSqlite && !string.IsNullOrEmpty(options.SqlitePath))
                return new SqliteDocumentStore(options.SqlitePath);
            
            return new InMemoryDocumentStore();
        });
        
        // Requires ElBruno.LocalEmbeddings to be registered separately
        services.AddSingleton<IRagPipeline, LocalRagPipeline>();
        
        return services;
    }
}

public sealed class RagOptions
{
    public IDocumentChunker? Chunker { get; set; }
    public IDocumentStore? Store { get; set; }
    public bool UseSqlite { get; set; }
    public string? SqlitePath { get; set; }
}
```

Usage:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalEmbeddings();
builder.Services.AddLocalLLMs();
builder.Services.AddLocalRag(options =>
{
    options.UseSqlite = true;
    options.SqlitePath = "rag_store.db";
});

var app = builder.Build();

app.MapGet("/ask", async (
    string query,
    IRagPipeline rag,
    IChatClient chat) =>
{
    var context = await rag.RetrieveContextAsync(query);
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, "Answer using context:\n" + context.FormattedContext),
        new(ChatRole.User, query)
    };
    var response = await chat.GetResponseAsync(messages);
    return response.Message.Text;
});

app.Run();
```

---

## 4. Implementation Order

### Phase 4a: Tool Calling (2-3 weeks)

**Week 1: Core Infrastructure**

1. Add `SupportsToolCalling` and `ToolCallingFormat` to `ModelDefinition`
2. Update `KnownModels` with tool support flags
3. Extend `IChatTemplateFormatter` with tool methods
4. Create `ChatTemplateFormatterBase` with default implementations
5. Add `ToolCallParseResult` and `ParsedToolCall` types

**Week 2: Model-Specific Formatters**
6. Implement `QwenFormatter.FormatMessagesWithTools` and `ParseToolCalls`
7. Implement `Llama3Formatter` tool support
8. Implement `Phi4Formatter` tool support
9. Add tool result formatting for each formatter

**Week 3: Integration & Testing**
10. Update `LocalChatClient.GetResponseAsync` to handle tools
11. Add `BuildGenerationParameters` support for tool mode
12. Write unit tests for each formatter's tool parsing
13. Write integration tests with real models
14. Document tool calling in Getting Started guide
15. Create `samples/ToolCallingAgent` sample

### Phase 4b: RAG Pipeline (2 weeks)

**Week 1: Core Abstractions**

1. Create `ElBruno.LocalLLMs.Rag` project
2. Define interfaces: `IDocumentChunker`, `IDocumentStore`, `IRagPipeline`
3. Implement `SlidingWindowChunker`
4. Implement `InMemoryDocumentStore`
5. Implement `LocalRagPipeline`

**Week 2: Persistence & Samples**
6. Implement `SqliteDocumentStore`
7. Add `RagServiceExtensions` for DI
8. Write unit tests for chunking and similarity
9. Write integration tests with LocalEmbeddings
10. Create `samples/RagChatbot` sample
11. Document RAG pipeline in docs
12. Create `docs/rag-guide.md` with patterns

---

## 5. File Inventory

### New Files (Phase 4a: Tool Calling)

```
src/ElBruno.LocalLLMs/
  Models/
    ToolCallingFormat.cs          — Enum for tool calling variants
  Templates/
    ChatTemplateFormatterBase.cs  — Base class with default tool methods
    ToolCallParseResult.cs         — Parse result types
  Execution/
    ToolCallParser.cs              — Shared parsing utilities
    
tests/ElBruno.LocalLLMs.Tests/
  Templates/
    QwenFormatterToolTests.cs
    Llama3FormatterToolTests.cs
    Phi4FormatterToolTests.cs
    
tests/ElBruno.LocalLLMs.IntegrationTests/
  ToolCallingIntegrationTests.cs
  
samples/
  ToolCallingAgent/
    Program.cs
    ToolCallingAgent.csproj
    README.md
    
docs/
  tool-calling-guide.md            — Tool calling documentation
```

### Modified Files (Phase 4a)

```
src/ElBruno.LocalLLMs/
  Models/
    ModelDefinition.cs             — Add SupportsToolCalling, ToolFormat
    KnownModels.cs                 — Update models with tool flags
  Templates/
    IChatTemplateFormatter.cs      — Add tool methods
    QwenFormatter.cs               — Implement tool support
    Llama3Formatter.cs             — Implement tool support
    Phi3Formatter.cs               — (remains no-op for tools)
  LocalChatClient.cs               — Add tool handling logic
  
docs/
  getting-started.md               — Add tool calling section
  supported-models.md              — Add tool support column
```

### New Files (Phase 4b: RAG Pipeline)

```
src/ElBruno.LocalLLMs.Rag/
  ElBruno.LocalLLMs.Rag.csproj
  Document.cs
  DocumentChunk.cs
  IDocumentChunker.cs
  IDocumentStore.cs
  IRagPipeline.cs
  RagContext.cs
  RagIndexProgress.cs
  Chunking/
    SlidingWindowChunker.cs
  Storage/
    InMemoryDocumentStore.cs
    SqliteDocumentStore.cs
  LocalRagPipeline.cs
  RagServiceExtensions.cs
  
tests/ElBruno.LocalLLMs.Rag.Tests/
  ChunkerTests.cs
  InMemoryStoreTests.cs
  RagPipelineTests.cs
  
tests/ElBruno.LocalLLMs.Rag.IntegrationTests/
  RagEndToEndTests.cs
  
samples/
  RagChatbot/
    Program.cs
    RagChatbot.csproj
    sample_documents/
      policy1.txt
      policy2.txt
    README.md
    
docs/
  rag-guide.md                     — RAG pipeline documentation
```

---

## 6. Sample Code

### 6.1 Tool Calling Sample

```csharp
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

// Define tools
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(GetTime)
};

// Create client with tool-capable model
var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen2_5_7B_Instruct
});

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant with access to tools."),
    new(ChatRole.User, "What's the weather in Paris and what time is it there?")
};

// Request with tools
var response = await client.GetResponseAsync(messages, new ChatOptions
{
    Tools = tools,
    ToolMode = ChatToolMode.Auto
});

// Handle tool calls
while (HasToolCalls(response.Message))
{
    messages.Add(response.Message);  // Add assistant's tool call request
    
    foreach (var content in response.Message.Contents.OfType<FunctionCallContent>())
    {
        Console.WriteLine($"Calling tool: {content.Name}");
        
        var result = await ExecuteToolAsync(content);
        
        messages.Add(new ChatMessage(ChatRole.Tool, 
            new FunctionResultContent(content.CallId, result)));
    }
    
    // Continue conversation
    response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools });
}

Console.WriteLine($"Final answer: {response.Message.Text}");

// Tool implementations
[Description("Get current weather for a city")]
static string GetWeather(
    [Description("City name")] string city)
{
    return $"The weather in {city} is sunny, 18°C";
}

[Description("Get current time for a timezone")]
static string GetTime(
    [Description("Timezone, e.g. 'Europe/Paris'")] string timezone)
{
    var time = TimeZoneInfo.ConvertTime(
        DateTime.UtcNow, 
        TimeZoneInfo.FindSystemTimeZoneById(timezone));
    return time.ToString("HH:mm");
}

static bool HasToolCalls(ChatMessage message)
{
    return message.Contents.Any(c => c is FunctionCallContent);
}

static async Task<string> ExecuteToolAsync(FunctionCallContent call)
{
    // Use AIFunctionFactory to invoke or implement manually
    return call.Name switch
    {
        "GetWeather" => GetWeather(call.Arguments["city"]?.ToString() ?? "Unknown"),
        "GetTime" => GetTime(call.Arguments["timezone"]?.ToString() ?? "UTC"),
        _ => "Tool not found"
    };
}
```

### 6.2 RAG Pipeline Sample

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalEmbeddings;
using Microsoft.Extensions.AI;

// Setup embedding generator and RAG pipeline
var embeddingGenerator = await LocalEmbeddingGenerator.CreateAsync();
var ragPipeline = new LocalRagPipeline(
    embeddingGenerator,
    store: new SqliteDocumentStore("company_kb.db")
);

// Index documents
var documents = new[]
{
    new Document("policy-remote", 
        "Remote Work Policy: Employees may work remotely up to 3 days per week. " +
        "Must maintain availability during core hours 10am-4pm."),
    new Document("policy-leave", 
        "Annual Leave Policy: 25 days per year. Must be requested 2 weeks in advance. " +
        "Maximum 10 consecutive days without manager approval."),
    new Document("policy-expenses", 
        "Expense Policy: All business expenses must be submitted within 30 days. " +
        "Receipts required for amounts over $50. Manager approval needed for $500+.")
};

Console.WriteLine("Indexing documents...");
await ragPipeline.IndexDocumentsAsync(documents);

// Create chat client
var chatClient = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen2_5_7B_Instruct
});

// Query loop
while (true)
{
    Console.Write("\nAsk a question (or 'quit'): ");
    var query = Console.ReadLine();
    if (query?.ToLower() == "quit") break;
    
    // Retrieve relevant context
    var context = await ragPipeline.RetrieveContextAsync(query!, topK: 3);
    
    Console.WriteLine($"\nRetrieved {context.RetrievedChunks.Count} relevant chunks");
    
    // Build messages with context
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, 
            "You are a helpful HR assistant. Answer questions using the provided company policy context. " +
            "If the context doesn't contain relevant information, say so.\n\n" +
            context.FormattedContext),
        new(ChatRole.User, query!)
    };
    
    // Get response
    var response = await chatClient.GetResponseAsync(messages);
    
    Console.WriteLine($"\nAnswer: {response.Message.Text}");
}

Console.WriteLine("\nGoodbye!");
```

### 6.3 Combined RAG + Tool Calling Sample

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalEmbeddings;
using Microsoft.Extensions.AI;

// Setup RAG
var embeddingGenerator = await LocalEmbeddingGenerator.CreateAsync();
var ragPipeline = new LocalRagPipeline(embeddingGenerator);

await ragPipeline.IndexDocumentsAsync(new[]
{
    new Document("prod-api", "Product API endpoint: GET /api/products/{id}"),
    new Document("user-api", "User API endpoint: POST /api/users with JSON body")
});

// Setup chat with tools
var chatClient = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen2_5_7B_Instruct
});

var tools = new List<AITool>
{
    AIFunctionFactory.Create(SearchDocumentation)
};

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant with access to documentation search."),
    new(ChatRole.User, "How do I create a new user via the API?")
};

// Agent loop
var maxTurns = 5;
for (int turn = 0; turn < maxTurns; turn++)
{
    var response = await chatClient.GetResponseAsync(messages, new ChatOptions
    {
        Tools = tools,
        ToolMode = ChatToolMode.Auto
    });
    
    if (!HasToolCalls(response.Message))
    {
        Console.WriteLine($"Final answer: {response.Message.Text}");
        break;
    }
    
    messages.Add(response.Message);
    
    foreach (var call in response.Message.Contents.OfType<FunctionCallContent>())
    {
        Console.WriteLine($"Tool call: {call.Name}({call.Arguments["query"]})");
        
        var result = await SearchDocumentation(call.Arguments["query"]?.ToString() ?? "");
        
        messages.Add(new ChatMessage(ChatRole.Tool, 
            new FunctionResultContent(call.CallId, result)));
    }
}

[Description("Search technical documentation")]
async Task<string> SearchDocumentation(
    [Description("Search query")] string query)
{
    var context = await ragPipeline.RetrieveContextAsync(query, topK: 2);
    return context.FormattedContext;
}

static bool HasToolCalls(ChatMessage message)
{
    return message.Contents.Any(c => c is FunctionCallContent);
}
```

---

## 7. Testing Strategy

### Phase 4a: Tool Calling Tests

**Unit Tests:**

- Tool definition serialization to JSON schema
- Tool call parsing for each format (Qwen, Llama3, Phi4)
- Tool result formatting for each template
- Multi-call parsing
- Edge cases: malformed JSON, missing arguments, extra text

**Integration Tests:**

- Round-trip: tools → prompt → model → parse → FunctionCallContent
- Tool result → format → model → final answer
- Multi-turn tool calling loops
- Models: Qwen2.5-7B, Llama-3.2-3B, Phi-4

### Phase 4b: RAG Tests

**Unit Tests:**

- Document chunking with various sizes and overlaps
- Cosine similarity calculations
- In-memory store add/search operations
- Context formatting

**Integration Tests:**

- End-to-end: index → embed → store → query → retrieve → format
- SQLite store persistence across sessions
- Large document sets (1000+ chunks)
- Similarity threshold filtering

---

## 8. Documentation Plan

### New Documents

1. **`docs/tool-calling-guide.md`** — Comprehensive guide to tool calling
   - Supported models
   - Defining tools with AIFunctionFactory
   - Tool calling loop patterns
   - Debugging tool calls
   - Model-specific formats

2. **`docs/rag-guide.md`** — RAG pipeline guide
   - When to use RAG vs fine-tuning
   - Document chunking strategies
   - Choosing embedding models
   - In-memory vs persistent stores
   - Context formatting best practices
   - Combining RAG with tool calling

### Updated Documents

- **`docs/getting-started.md`** — Add tool calling quickstart section
- **`docs/supported-models.md`** — Add "Tool Support" column
- **`CONTRIBUTING.md`** — Add "Adding tool support to a model"

---

## 9. Future Enhancements (Post-Phase 4)

1. **Streaming tool calls** — Buffer and parse partial tool call JSON
2. **Native ONNX tool calling** — If/when ONNX GenAI adds native support
3. **Advanced chunking** — Semantic chunking, recursive splitting, markdown-aware
4. **Vector database integrations** — Qdrant, Milvus, Weaviate adapters
5. **Hybrid search** — Combine vector similarity with keyword search (BM25)
6. **Tool call caching** — Cache deterministic tool results
7. **Parallel tool execution** — Execute multiple tool calls concurrently
8. **Tool call validation** — JSON schema validation before execution

---

## 10. Open Questions

1. **Tool mode enforcement:** If `ChatToolMode.RequireAny` is set but the model doesn't call a tool, do we:
   - Return an error?
   - Retry with stronger prompt injection?
   - Fall back to normal response?

2. **Tool result size limits:** If a tool returns 50KB of JSON, do we:
   - Truncate automatically?
   - Let the user handle it?
   - Summarize with a secondary LLM call?

3. **RAG context window management:** If retrieved context exceeds model's max tokens:
   - Auto-summarize chunks?
   - Use only top-K chunks that fit?
   - Throw an error?

4. **SQLite vs specialized vector DBs:** Should we recommend migration to Qdrant/Milvus at a certain scale, or optimize SQLite with extensions (e.g., sqlite-vss)?

---

## 11. Success Criteria

**Phase 4a is complete when:**

- [ ] At least 3 models support tool calling (Qwen2.5-7B, Llama-3.2-3B, Phi-4)
- [ ] All formatters have >90% test coverage for tool parsing
- [ ] Integration tests pass with real models making real tool calls
- [ ] `samples/ToolCallingAgent` demonstrates multi-turn agent loop
- [ ] Documentation explains tool calling clearly with examples

**Phase 4b is complete when:**

- [ ] RAG pipeline can index, embed, store, and retrieve documents
- [ ] Both in-memory and SQLite stores work correctly
- [ ] Integration tests verify end-to-end RAG with LocalEmbeddings
- [ ] `samples/RagChatbot` demonstrates practical RAG usage
- [ ] Documentation explains RAG patterns and best practices

---

**End of Plan**

Questions or concerns? Bring them to Morpheus before implementation starts.
