using ElBruno.LocalLLMs.Rag;
using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║          RAG Chatbot Sample - ElBruno.LocalLLMs          ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Sample company policy documents
var documents = new[]
{
    new Document(
        "policy-001",
        "Company vacation policy: All full-time employees receive 15 days of paid vacation per year. " +
        "Vacation days must be requested at least 2 weeks in advance through the HR portal. " +
        "Unused vacation days can be carried over to the next year, up to a maximum of 5 days."
    ),
    new Document(
        "policy-002",
        "Remote work policy: Employees may work remotely up to 3 days per week with manager approval. " +
        "Remote workers must maintain regular business hours (9 AM - 5 PM) and be available via Teams. " +
        "All remote work arrangements must be documented in the HR system."
    ),
    new Document(
        "policy-003",
        "Expense reimbursement policy: Employees can submit business expenses for reimbursement. " +
        "All expenses require receipts and must be submitted within 30 days. " +
        "Approved categories include travel, meals (up to $50/day), and office supplies. " +
        "Reimbursements are processed bi-weekly."
    )
};

Console.WriteLine("📄 Sample Documents:");
foreach (var doc in documents)
{
    Console.WriteLine($"  - {doc.Id}: {doc.Content.Substring(0, Math.Min(60, doc.Content.Length))}...");
}
Console.WriteLine();

// Create RAG pipeline with mock embedding generator
var mockEmbeddingGenerator = new MockEmbeddingGenerator();
var chunker = new SlidingWindowChunker(chunkSize: 200, overlap: 50);
var store = new InMemoryDocumentStore();
var ragPipeline = new LocalRagPipeline(chunker, store, mockEmbeddingGenerator);

// Index documents
Console.WriteLine("📇 Indexing documents...");
var progress = new Progress<RagIndexProgress>(p =>
{
    Console.WriteLine($"  Progress: {p.Processed}/{p.Total} documents indexed");
});

await ragPipeline.IndexDocumentsAsync(documents, progress);
Console.WriteLine("✅ Indexing complete!");
Console.WriteLine();

// Example queries
var queries = new[]
{
    "How many vacation days do I get?",
    "Can I work from home?",
    "How do I submit expenses?"
};

foreach (var query in queries)
{
    Console.WriteLine($"🔍 Query: {query}");
    var context = await ragPipeline.RetrieveContextAsync(query, topK: 2);

    Console.WriteLine($"📋 Retrieved {context.RetrievedChunks.Count} relevant chunks:");
    foreach (var chunk in context.RetrievedChunks)
    {
        Console.WriteLine($"  - From {chunk.DocumentId}: {chunk.Content.Substring(0, Math.Min(80, chunk.Content.Length))}...");
    }
    Console.WriteLine();
}

Console.WriteLine("ℹ️  This sample demonstrates the RAG pipeline components:");
Console.WriteLine("   • Document chunking (sliding window)");
Console.WriteLine("   • Embedding generation (mock implementation)");
Console.WriteLine("   • Semantic search (cosine similarity)");
Console.WriteLine("   • Context retrieval for chat augmentation");
Console.WriteLine();
Console.WriteLine("💡 To use with a real LLM, inject the retrieved context into your chat prompt:");
Console.WriteLine("   context.RetrievedChunks → format as system message → pass to IChatClient");
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// Mock embedding generator for demonstration
internal sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Random _random = new(42);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(v => new Embedding<float>(GenerateEmbedding(v))).ToList();
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public EmbeddingGeneratorMetadata Metadata => new("mock-embedder");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private ReadOnlyMemory<float> GenerateEmbedding(string text)
    {
        var hash = text.GetHashCode();
        var rng = new Random(hash);
        var vector = new float[384];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2 - 1);
        }
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
        return vector;
    }
}
