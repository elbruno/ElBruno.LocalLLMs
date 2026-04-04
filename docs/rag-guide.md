# RAG Guide — ElBruno.LocalLLMs

Build Retrieval-Augmented Generation (RAG) pipelines with local LLMs to ground responses in private documents and domain knowledge.

---

## Table of Contents

1. [Overview](#overview)
2. [When to Use RAG](#when-to-use-rag)
3. [Architecture](#architecture)
4. [Installation](#installation)
5. [Core Concepts](#core-concepts)
6. [Quick Start](#quick-start)
7. [Document Chunking](#document-chunking)
8. [Vector Stores](#vector-stores)
9. [Dependency Injection](#dependency-injection)
10. [Combining RAG + Tool Calling](#combining-rag--tool-calling)
11. [Best Practices](#best-practices)
12. [Limitations](#limitations)

---

## Overview

**Retrieval-Augmented Generation (RAG)** combines document retrieval with local LLM inference to answer questions grounded in your private data.

**The problem RAG solves:**

- LLMs have knowledge cutoffs (trained on data up to a certain date)
- LLMs hallucinate when asked about proprietary or real-time information
- You can't fine-tune models for every domain-specific knowledge update

**The RAG solution:**

1. **Index** — split your documents into chunks, embed them, store them
2. **Retrieve** — embed user query, find similar chunks
3. **Inject** — inject relevant chunks into chat context
4. **Chat** — LLM responds grounded in retrieved context

**Result:** Accurate, up-to-date answers from your documents without retraining.

### Why Local RAG?

- 🔒 **Privacy** — documents stay on-device, no cloud calls
- ⚡ **Speed** — retrieve + inference happen locally, no network latency
- 💰 **Cost** — no per-query API charges
- 🛠️ **Control** — use your own embeddings, chunking strategies, vector stores

---

## When to Use RAG

**Use RAG when:**
- ✅ You have domain-specific documents (policies, manuals, company data)
- ✅ You need current information (news, pricing, status updates)
- ✅ You want to reduce hallucinations
- ✅ You need to cite sources in responses
- ✅ You have sensitive/proprietary information

**Don't use RAG when:**
- ❌ You're asking general knowledge questions (use LLM directly)
- ❌ You have <1 MB of text (indexing overhead not worth it)
- ❌ Your knowledge base changes every second (static indexing is too slow)
- ❌ You need sub-100ms latency (indexing + retrieval adds overhead)

**Comparison:**

| Scenario | Use | Why |
|----------|-----|-----|
| "What's the capital of France?" | Plain Chat | LLM knows this from training |
| "What's our Q4 earnings per our latest report?" | RAG | LLM doesn't know private earnings |
| "Fix my code error X" | Plain Chat | General programming knowledge |
| "How do I configure our proprietary tool Y?" | RAG | Company-specific documentation |
| "What time is it?" | Tool Calling | Real-time data via API |
| "When did we release feature Z?" | RAG + Tool Calling | Historical docs + database lookup |

---

## Architecture

RAG pipelines have 5 stages:

```
┌──────────────────────────────────────────────────┐
│ Stage 1: Document → Read your documents
├──────────────────────────────────────────────────┤
│ Stage 2: Chunk → Split into small pieces (512 tokens)
├──────────────────────────────────────────────────┤
│ Stage 3: Embed → Convert text → vector embedding
├──────────────────────────────────────────────────┤
│ Stage 4: Store → Save vectors in document store
├──────────────────────────────────────────────────┤
│ Stage 5: Retrieve → Query: embed → find similar
├──────────────────────────────────────────────────┤
│ Stage 6: Inject → Add chunks to chat context
├──────────────────────────────────────────────────┤
│ Stage 7: Chat → LLM responds with context
└──────────────────────────────────────────────────┘
```

**Flow:**

```
[Indexing Phase]
Documents → Chunk (512 tokens, 50 token overlap)
         → Embed (via LocalEmbeddingGenerator)
         → Store (in-memory or SQLite)

[Retrieval Phase]
User Query → Embed (same embedder)
          → Search (cosine similarity, top-5)
          → Format (inject into chat prompt)
          → Chat (LLM responds with context)
```

---

## Installation

### Add the Package

```bash
dotnet add package ElBruno.LocalLLMs.Rag
```

### Dependencies

`ElBruno.LocalLLMs.Rag` depends on:
- `ElBruno.LocalLLMs` (core chat)
- `ElBruno.LocalEmbeddings` (embeddings)
- `Microsoft.Extensions.AI` (standard interfaces)

```xml
<ItemGroup>
    <PackageReference Include="ElBruno.LocalLLMs" Version="0.1.0" />
    <PackageReference Include="ElBruno.LocalLLMs.Rag" Version="0.1.0" />
    <PackageReference Include="ElBruno.LocalEmbeddings" Version="0.2.0" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="1.0.0" />
</ItemGroup>
```

---

## Core Concepts

### Document

A source text to be indexed:

```csharp
var doc = new Document(
    Id: "company-policy-2025",
    Content: "Remote work is allowed 3 days per week. Flexible hours 8am–6pm...",
    Metadata: new Dictionary<string, object> {
        { "source", "HR Handbook" },
        { "updated", "2025-01-15" }
    }
);
```

**Metadata** is optional but recommended — use it to track source, date, category, etc.

### DocumentChunk

A small piece of a document with an embedding:

```csharp
var chunk = new DocumentChunk(
    Id: "company-policy-2025_chunk_0",
    DocumentId: "company-policy-2025",
    Content: "Remote work is allowed 3 days per week...",
    Embedding: new ReadOnlyMemory<float>(new[] { 0.1f, 0.2f, ... }),
    Metadata: new Dictionary<string, object> {
        { "source", "HR Handbook" },
        { "section", "Work Arrangements" }
    }
);
```

Chunks are created by chunking documents and embeddings are assigned during indexing.

### IDocumentChunker

Splits documents into chunks for embedding:

```csharp
public interface IDocumentChunker
{
    IEnumerable<DocumentChunk> ChunkDocument(
        Document document,
        int chunkSize = 512,        // tokens per chunk
        int overlapSize = 50);      // overlap between chunks
}
```

**Built-in implementation:** `SlidingWindowChunker`

### IDocumentStore

Stores chunks and searches by embedding similarity:

```csharp
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
```

**Built-in implementations:**
- `InMemoryDocumentStore` — fast, searchable by cosine similarity
- `SqliteDocumentStore` — persistent, survives app restart

### IRagPipeline

Orchestrates the full RAG workflow:

```csharp
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
```

**Built-in implementation:** `LocalRagPipeline`

### RagContext

The retrieved context ready to inject into chat:

```csharp
var context = await ragPipeline.RetrieveContextAsync("What's the remote work policy?");

Console.WriteLine(context.Query);                  // "What's the remote work policy?"
Console.WriteLine(context.RetrievedChunks.Count);  // 5 (top-5 results)
Console.WriteLine(context.FormattedContext);       // "Relevant context:\n[1] ...\n[2] ..."
```

---

## Quick Start

Here's a working example — index documents and chat with RAG in **~40 lines**:

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalEmbeddings;
using Microsoft.Extensions.AI;

// Step 1: Create embedding generator
var embeddingGenerator = new LocalEmbeddingGenerator();

// Step 2: Create RAG pipeline
var ragPipeline = new LocalRagPipeline(embeddingGenerator);

// Step 3: Index documents
var documents = new[]
{
    new Document("doc1", "Remote work policy: Allowed 3 days/week. Flexible hours 8am–6pm."),
    new Document("doc2", "Vacation policy: 25 days/year. Book 2 weeks in advance."),
    new Document("doc3", "Equipment: Get a laptop, monitor, and chair reimbursed.")
};

Console.WriteLine("📚 Indexing documents...");
await ragPipeline.IndexDocumentsAsync(documents);
Console.WriteLine("✅ Indexed 3 documents\n");

// Step 4: Create chat client
var chatClient = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

// Step 5: Chat with RAG
var userQuery = "What's the remote work policy?";
Console.WriteLine($"👤 User: {userQuery}");

// Retrieve relevant context
var context = await ragPipeline.RetrieveContextAsync(userQuery);

// Inject context into system message
var messages = new List<ChatMessage>
{
    new(ChatRole.System, $"You are a helpful HR assistant.\n\n{context.FormattedContext}"),
    new(ChatRole.User, userQuery)
};

// Get response from LLM
var response = await chatClient.GetResponseAsync(messages);
Console.WriteLine($"🤖 Assistant: {response.Text}");
```

**Output:**
```
📚 Indexing documents...
✅ Indexed 3 documents

👤 User: What's the remote work policy?
🤖 Assistant: Based on the company policy, remote work is allowed 3 days per week with flexible hours between 8am and 6pm.
```

---

## Document Chunking

### Why Chunk?

Embeddings work best on short texts (100–1000 tokens). Chunking:
- ✅ Fits within embedding model context
- ✅ Improves semantic relevance (each chunk is coherent)
- ✅ Reduces memory footprint

### SlidingWindowChunker

The default chunker uses a sliding window with overlap:

```csharp
var chunker = new SlidingWindowChunker();

var doc = new Document("policy",
    "Section 1: Remote work allowed 3 days/week. " +
    "Section 2: Vacation 25 days/year. " +
    "Section 3: Equipment budget $2000.");

// Split into chunks of 50 tokens with 10 token overlap
var chunks = chunker.ChunkDocument(doc, chunkSize: 50, overlapSize: 10);

foreach (var chunk in chunks)
{
    Console.WriteLine($"[{chunk.Id}]: {chunk.Content}");
}
```

**Output:**
```
[policy_chunk_0]: Section 1: Remote work allowed 3 days/week. Section 2: Vacation...
[policy_chunk_1]: Section 2: Vacation 25 days/year. Section 3: Equipment budget...
[policy_chunk_2]: Section 3: Equipment budget $2000. [end]
```

### Choosing Chunk Size

| Chunk Size | Use Case | Pros | Cons |
|------------|----------|------|------|
| 128 tokens | Micro-documents, Q&A | Very specific chunks | May fragment long ideas |
| 256 tokens | Short docs, fine-grained | Good balance | Still fragments |
| 512 tokens | **Standard** | Best semantic coherence | May include unrelated content |
| 1024 tokens | Long documents, essays | Keeps ideas together | Requires larger embeddings |

**Recommendation:** Start with 512 tokens, adjust based on your documents.

### Choosing Overlap

Overlap helps preserve context at chunk boundaries:

| Overlap | Trade-off | Use Case |
|---------|-----------|----------|
| 0 tokens | No overlap | Huge documents, memory-constrained |
| 25 tokens | **Standard** | Most cases |
| 50 tokens | High overlap | Critical information at boundaries |

**Recommendation:** Use 25–50 token overlap. Larger overlap = bigger index, better boundary handling.

### Custom Chunker

Implement `IDocumentChunker` for custom logic:

```csharp
public sealed class ParagraphChunker : IDocumentChunker
{
    public IEnumerable<DocumentChunk> ChunkDocument(
        Document document,
        int chunkSize = 512,
        int overlapSize = 50)
    {
        var paragraphs = document.Content.Split("\n\n");
        var chunkIndex = 0;

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(para))
                continue;

            yield return new DocumentChunk(
                Id: $"{document.Id}_chunk_{chunkIndex}",
                DocumentId: document.Id,
                Content: para,
                Embedding: ReadOnlyMemory<float>.Empty,
                Metadata: document.Metadata
            );

            chunkIndex++;
        }
    }
}

// Use custom chunker
var customChunker = new ParagraphChunker();
var ragPipeline = new LocalRagPipeline(embeddingGenerator, chunker: customChunker);
```

---

## Vector Stores

### InMemoryDocumentStore

Stores chunks in memory. Fast, searchable by cosine similarity.

```csharp
var store = new InMemoryDocumentStore();

// Index
await store.AddAsync(chunks);

// Search
var results = await store.SearchAsync(queryEmbedding, topK: 5, minSimilarity: 0.5f);

// Clear
await store.ClearAsync();
```

**Pros:**
- ✅ Instant retrieval (milliseconds)
- ✅ No external dependencies
- ✅ Simple, embedded

**Cons:**
- ❌ Data lost on app restart
- ❌ Doesn't scale beyond ~100k chunks (RAM limited)
- ❌ Brute-force search (O(n) complexity)

**Best for:** Development, testing, small knowledge bases (<50k chunks)

### SqliteDocumentStore

Stores chunks in SQLite. Persistent, queryable.

```csharp
var store = new SqliteDocumentStore("./knowledge.db");

// Index
await store.AddAsync(chunks);

// Search (persists across runs)
var results = await store.SearchAsync(queryEmbedding, topK: 5);

// Clear
await store.ClearAsync();
```

**Pros:**
- ✅ Persistent (survives app restart)
- ✅ Standard SQL interface
- ✅ Scales to millions of chunks
- ✅ Works offline

**Cons:**
- ⚠️ Slower than in-memory (SQLite overhead)
- ❌ Still brute-force search (no vector indices yet)
- ❌ Requires disk I/O

**Best for:** Production, persistent knowledge bases, large document collections

### Choosing a Store

| Store | Size | Persistence | Latency | Best For |
|-------|------|-------------|---------|----------|
| **InMemory** | <50k chunks | ❌ No | ⚡⚡⚡ | Dev, testing, small RAG |
| **SQLite** | 1M+ chunks | ✅ Yes | ⚡⚡ | Production, persistent RAG |

---

## Dependency Injection

### ASP.NET Core Registration

Use `RagServiceExtensions.AddLocalRag()`:

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalEmbeddings;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Register core services
builder.Services
    .AddLocalLLMs(options => {
        options.Model = KnownModels.Phi35MiniInstruct;
    })
    .AddLocalEmbeddings()
    .AddLocalRag(options => {
        options.Store = new SqliteDocumentStore("./knowledge.db");
    });

var app = builder.Build();

// Inject into endpoints
app.MapPost("/ask", async (IChatClient chat, IRagPipeline rag, string question) =>
{
    var context = await rag.RetrieveContextAsync(question);
    
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, context.FormattedContext),
        new(ChatRole.User, question)
    };
    
    var response = await chat.GetResponseAsync(messages);
    return response.Text;
});

app.Run();
```

### Manual Registration

```csharp
var embeddingGenerator = new LocalEmbeddingGenerator();
var store = new SqliteDocumentStore("./knowledge.db");
var chunker = new SlidingWindowChunker();
var ragPipeline = new LocalRagPipeline(embeddingGenerator, chunker, store);

// Now use ragPipeline
```

---

## Combining RAG + Tool Calling

The real power: **tools that search documents + RAG that injects context**.

### Pattern: Tool-Based Document Search

Define a tool that searches the RAG store:

```csharp
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// Setup RAG
var embeddingGenerator = new LocalEmbeddingGenerator();
var ragPipeline = new LocalRagPipeline(embeddingGenerator);

// Index documents
var documents = new[] {
    new Document("doc1", "Remote work: 3 days/week"),
    new Document("doc2", "Vacation: 25 days/year"),
};
await ragPipeline.IndexDocumentsAsync(documents);

// Create chat client
var chat = await LocalChatClient.CreateAsync();

// Define a tool that uses RAG
[Description("Search company policies")]
static async Task<string> SearchPolicies(
    [Description("What to search for")] string query,
    IRagPipeline rag)
{
    var context = await rag.RetrieveContextAsync(query, topK: 3);
    return context.FormattedContext;
}

// Setup tool calling
var tools = new List<AITool>
{
    AIFunctionFactory.Create(SearchPolicies)
};

var messages = new List<ChatMessage>
{
    new(ChatRole.User, "What's our remote work policy and vacation allowance?")
};

// Agent loop
while (true)
{
    var response = await chat.GetResponseAsync(messages, new ChatOptions { Tools = tools });
    messages.Add(response.Message);

    var toolCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
    if (toolCalls.Count == 0)
        break;

    foreach (var call in toolCalls)
    {
        // Execute SearchPolicies tool with RAG
        var result = await SearchPolicies((string)call.Arguments?["query"]!, ragPipeline);
        messages.Add(new ChatMessage(ChatRole.Tool,
            new FunctionResultContent(call.CallId, result)));
    }
}

Console.WriteLine(response.Message.Text);
```

**Flow:**
```
User: "What's our remote work policy?"
Model: "I'll search the policies"
  → Calls SearchPolicies("remote work")
  → RAG retrieves relevant chunks
Tool Result: "[1] Remote work: 3 days/week..."
Model: "Based on the policy, remote work is..."
```

### Best Practices for RAG + Tools

1. **Tool describes what it searches, not format**
   ```csharp
   // ✅ Good
   [Description("Search company HR policies")]
   static string SearchPolicies(string query) { ... }

   // ❌ Avoid
   [Description("Return JSON array of matching chunks")]
   static string SearchPolicies(string query) { ... }
   ```

2. **Let the model decide when to call**
   - Model sees tool exists
   - Model decides when it needs to search
   - You don't need to inject context manually

3. **Tools return formatted text, not raw vectors**
   - Tool returns human-readable context
   - Model reads and synthesizes
   - No embedding/vector logic exposed to model

---

## Best Practices

### 1. Chunk Size Guidelines

```csharp
// Small documents (< 5000 tokens)
var chunker = new SlidingWindowChunker();
var chunks = chunker.ChunkDocument(doc, chunkSize: 256, overlapSize: 25);

// Medium documents (5k–50k tokens)
var chunks = chunker.ChunkDocument(doc, chunkSize: 512, overlapSize: 50);

// Large documents (> 50k tokens)
var chunks = chunker.ChunkDocument(doc, chunkSize: 1024, overlapSize: 100);
```

### 2. Embedding Model Selection

Match embedding model to your documents:

```csharp
// General-purpose, works for most cases
var embeddingGenerator = new LocalEmbeddingGenerator();

// For scientific/technical documents
var embeddingGenerator = new LocalEmbeddingGenerator(
    model: KnownEmbeddingModels.BgeSmallEnV15
);
```

### 3. Context Window Management

Ensure injected context fits in model's context window:

```csharp
var context = await ragPipeline.RetrieveContextAsync(query, topK: 5);

// Check: tokens = ~4 * chars / 5 (rough estimate)
var estimatedTokens = context.FormattedContext.Length / 5 * 4;
if (estimatedTokens > 2000)  // Leave room for user message
{
    // Use fewer chunks or smaller model
    context = await ragPipeline.RetrieveContextAsync(query, topK: 3);
}
```

### 4. Monitoring Retrieval Quality

Check if the right chunks are being retrieved:

```csharp
var context = await ragPipeline.RetrieveContextAsync("What's our vacation policy?");

Console.WriteLine($"Retrieved {context.RetrievedChunks.Count} chunks:");
foreach (var chunk in context.RetrievedChunks)
{
    Console.WriteLine($"  - {chunk.Content.Substring(0, 50)}...");
}
```

If irrelevant chunks appear:
- **Increase minSimilarity threshold**
- **Reduce chunkSize** (too large chunks dilute relevance)
- **Adjust topK** (retrieve fewer results)

### 5. Incremental Indexing

Add documents without re-indexing everything:

```csharp
// Index existing docs
var documents = new[] { doc1, doc2, doc3 };
await ragPipeline.IndexDocumentsAsync(documents);

// Later: add new documents (doesn't re-index old ones)
var newDoc = new Document("doc4", "New policy...");
await ragPipeline.IndexDocumentsAsync(new[] { newDoc });
```

### 6. Source Attribution

Track document sources for citations:

```csharp
var doc = new Document(
    "earnings-q4-2025",
    "Q4 revenue was $15M...",
    Metadata: new Dictionary<string, object> {
        { "source", "Investor Report" },
        { "url", "https://company.com/earnings-q4-2025.pdf" },
        { "date", "2025-01-15" }
    }
);

await ragPipeline.IndexDocumentsAsync(new[] { doc });

var context = await ragPipeline.RetrieveContextAsync("What was Q4 revenue?");
// Each chunk retains metadata for attribution
```

---

## Limitations

### 1. No Built-in Vector Indices

Retrieval uses brute-force cosine similarity search. For large datasets (>100k chunks):

```csharp
// Linear search through all chunks — O(n) complexity
var results = await store.SearchAsync(queryEmbedding, topK: 5);
```

**Workaround:** Use SQLite store for persistence; consider external vector DB (Milvus, Pinecone) for massive scale.

### 2. No Semantic Chunking Yet

Chunks are created by token count, not semantic boundaries:

```
❌ Current: Split at 512 tokens regardless of meaning
    "Remote work policy... [CHUNK BREAK] ...requires approval."
    → Splits mid-sentence

✅ Future: Split at sentence/paragraph boundaries
```

**Workaround:** Implement custom chunker for semantic splits.

### 3. SQLite Search is Brute-Force

SQLiteDocumentStore scans all rows every search:

```csharp
// Scans entire table for similarity
var results = await store.SearchAsync(queryEmbedding, topK: 5);
// For 1M chunks: ~1–5 second latency
```

**Workaround:**
- Use InMemoryDocumentStore for <50k chunks
- Batch index updates (index in background)
- Consider external vector DB for production-scale

### 4. No Re-ranking Yet

Retrieved chunks are returned in order of embedding similarity. No re-ranking step to improve relevance:

```
Retrieved chunks (by similarity):
[1] Similarity: 0.87 — Remote work policy (relevant)
[2] Similarity: 0.85 — Work from home benefits (somewhat relevant)
[3] Similarity: 0.82 — Office dress code (not relevant, but semantically close)
```

**Workaround:** Manually filter by similarity threshold; use `minSimilarity` parameter.

### 5. Static Embeddings

Embeddings are generated once at indexing time:

```csharp
// Embedding never updates
await ragPipeline.IndexDocumentsAsync(documents);

// Later: documents changed but embeddings didn't
var doc = new Document("doc1", "NEW CONTENT...");
// You must re-index to update embeddings
await ragPipeline.IndexDocumentsAsync(new[] { doc });
```

**Workaround:** Periodically re-index; implement versioning (doc1_v1, doc1_v2).

### 6. Embedding Dimension Mismatch

All embeddings must have the same dimension:

```csharp
// ❌ This fails silently
var embedder1 = new LocalEmbeddingGenerator();  // 384 dimensions
var embedder2 = new LocalEmbeddingGenerator(model: "other");  // 768 dimensions

// Mixing documents from both embedders causes silent failures
```

**Mitigation:** Use one embedding generator consistently; store it with your vector store.

---

## Zero-Cloud RAG Sample

The **ZeroCloudRag** sample demonstrates a complete end-to-end RAG pipeline using **all local models**—no cloud APIs, everything runs on your machine.

### What It Shows

- **Local Embeddings** — document indexing with `ElBruno.LocalEmbeddings`
- **Local LLM** — inference with `ElBruno.LocalLLMs`
- **RAG Pipeline** — combining embeddings + retrieval + chat
- **Dependency Injection** — clean registration in ASP.NET Core
- **Production patterns** — error handling, chunking, retrieval monitoring

### Key Architecture

```csharp
// The DI pattern: embeddings + LLMs + RAG pipeline
builder.Services.AddLocalEmbeddings();  // Local embeddings
builder.Services.AddLocalLLMs();        // Local LLM inference
builder.Services.AddRagPipeline();      // RAG orchestration

// Inject and use anywhere
public class RagService(
    IEmbeddingGenerator embeddingGenerator,
    IChatClient chatClient,
    IRagPipeline ragPipeline)
{
    // Build your RAG system
}
```

### Why Zero-Cloud?

- **🔒 Privacy** — documents never leave your machine
- **⚡ Speed** — no network round-trips
- **💰 Cost** — zero API charges
- **🛠️ Control** — you own the entire pipeline

### Location

See the full working sample at: [`src/samples/ZeroCloudRag/`](../src/samples/ZeroCloudRag/)

---

## See Also

- 📖 [Getting Started Guide](getting-started.md) — basic chat completions
- 🔧 [Tool Calling Guide](tool-calling-guide.md) — agents + tools
- 📋 [Supported Models](supported-models.md) — model reference
- 🧪 [Samples](../src/samples/) — runnable examples
- 🏗️ [Architecture](architecture.md) — internal design

Happy building! 🚀
