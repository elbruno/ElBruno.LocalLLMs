using LocalEmbeddings;
using LocalEmbeddings.Options;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║       Zero-Cloud RAG Sample — ElBruno.LocalLLMs          ║");
Console.WriteLine("║  Everything runs locally. No cloud APIs needed.          ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Step 1: Create sample documents ────────────────────────────────────────
Console.WriteLine("📄 Step 1 — Creating sample documents...");

var documents = new[]
{
    new Document(
        "tip-async",
        "C# async/await best practices: Always use async all the way up — avoid blocking calls " +
        "like .Result or .Wait() on tasks. Use ConfigureAwait(false) in library code to avoid " +
        "deadlocks. Prefer ValueTask over Task when the result is often available synchronously. " +
        "Name async methods with the Async suffix to signal callers."
    ),
    new Document(
        "tip-span",
        "Using Span<T> and Memory<T> for high-performance C#: Span<T> provides a type-safe view " +
        "over contiguous memory without heap allocations. Use it for parsing, slicing buffers, and " +
        "interop scenarios. Span<T> is stack-only and cannot be stored on the heap — use Memory<T> " +
        "when you need to store a reference across async boundaries."
    ),
    new Document(
        "tip-di",
        "Dependency Injection in .NET: Register services with AddSingleton, AddScoped, or AddTransient " +
        "in the IServiceCollection. Prefer constructor injection over service locator patterns. Use " +
        "IOptions<T> for configuration binding. Avoid captive dependencies — never inject a scoped " +
        "service into a singleton."
    ),
    new Document(
        "tip-record",
        "C# records and immutability: Records provide value-based equality and built-in ToString. " +
        "Use 'record class' for reference types and 'record struct' for value types. Records support " +
        "non-destructive mutation with the 'with' expression. They are ideal for DTOs, events, and " +
        "any data that should be treated as immutable values."
    ),
    new Document(
        "tip-linq",
        "LINQ performance tips: Avoid multiple enumerations of IEnumerable — materialize with " +
        "ToList() or ToArray() when the source is expensive. Use Where() before Select() to reduce " +
        "projections. Consider using compiled LINQ queries with EF Core. For hot paths, manual loops " +
        "can outperform LINQ due to eliminated delegate allocations."
    )
};

foreach (var doc in documents)
{
    Console.WriteLine($"  • [{doc.Id}] {doc.Content[..Math.Min(65, doc.Content.Length)]}...");
}
Console.WriteLine();

// ── Step 2: Initialize the real local embedding generator ──────────────────
Console.WriteLine("🧠 Step 2 — Initializing local embedding generator (all-MiniLM-L6-v2)...");
Console.WriteLine("         (First run downloads the ONNX model — this is a one-time operation)");

using var embeddingGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());
Console.WriteLine("  ✅ Embedding generator ready.");
Console.WriteLine();

// ── Step 3: Initialize the chunker ─────────────────────────────────────────
Console.WriteLine("✂️  Step 3 — Creating sliding window chunker (size=200, overlap=50)...");
var chunker = new SlidingWindowChunker(chunkSize: 200, overlap: 50);
Console.WriteLine("  ✅ Chunker ready.");
Console.WriteLine();

// ── Step 4: Initialize the document store ──────────────────────────────────
Console.WriteLine("🗄️  Step 4 — Creating in-memory document store...");
var store = new InMemoryDocumentStore();
Console.WriteLine("  ✅ Store ready.");
Console.WriteLine();

// ── Step 5: Create the RAG pipeline with real embeddings ───────────────────
Console.WriteLine("🔗 Step 5 — Assembling RAG pipeline...");
var ragPipeline = new LocalRagPipeline(chunker, store, embeddingGenerator);
Console.WriteLine("  ✅ RAG pipeline ready (chunker → embedder → vector store).");
Console.WriteLine();

// ── Step 6: Index all documents with progress ──────────────────────────────
Console.WriteLine("📇 Step 6 — Indexing documents...");
var progress = new Progress<RagIndexProgress>(p =>
{
    Console.WriteLine($"  Indexed {p.Processed}/{p.Total} documents");
});

await ragPipeline.IndexDocumentsAsync(documents, progress);
Console.WriteLine("  ✅ All documents indexed with real embeddings!");
Console.WriteLine();

// ── Step 7: Define the user query ──────────────────────────────────────────
var userQuery = "How do I avoid deadlocks when using async in C#?";
Console.WriteLine($"🔍 Step 7 — User query: \"{userQuery}\"");
Console.WriteLine();

// ── Step 8: Retrieve relevant context via RAG ──────────────────────────────
Console.WriteLine("📋 Step 8 — Retrieving relevant context from vector store...");
var context = await ragPipeline.RetrieveContextAsync(userQuery, topK: 3);

Console.WriteLine($"  Found {context.RetrievedChunks.Count} relevant chunks:");
foreach (var chunk in context.RetrievedChunks)
{
    Console.WriteLine($"  • [{chunk.DocumentId}] {chunk.Content[..Math.Min(80, chunk.Content.Length)]}...");
}
Console.WriteLine();

// ── Step 9: Initialize the local LLM ──────────────────────────────────────
Console.WriteLine("🤖 Step 9 — Loading local LLM (Phi-3.5-mini-instruct, native ONNX)...");
Console.WriteLine("         (First run downloads the model — this is a one-time operation)");

using var chatClient = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
});

Console.WriteLine("  ✅ LLM ready.");
Console.WriteLine();

// ── Step 10: Send query + RAG context to the LLM ──────────────────────────
Console.WriteLine("💬 Step 10 — Sending query + RAG context to the LLM for a grounded answer...");
Console.WriteLine();

var ragContext = string.Join("\n\n", context.RetrievedChunks.Select(c => c.Content));

var messages = new ChatMessage[]
{
    new(ChatRole.System,
        "You are a helpful C# programming assistant. Answer the user's question using ONLY " +
        "the context provided below. If the context does not contain enough information, say so. " +
        "Be concise and practical.\n\n" +
        "--- Retrieved Context ---\n" +
        ragContext),
    new(ChatRole.User, userQuery)
};

// ── Step 11: Stream the grounded response ──────────────────────────────────
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("📝 Grounded Answer:");
Console.WriteLine("───────────────────────────────────────────────────────────");

await foreach (var update in chatClient.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("✅ Done! The entire pipeline ran locally — zero cloud APIs used.");
Console.WriteLine();
Console.WriteLine("Pipeline: Documents → Chunking → Embedding → Vector Store → Query → Retrieval → LLM → Answer");
