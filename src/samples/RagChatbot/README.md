# RAG Chatbot Sample

This sample demonstrates the **RAG (Retrieval-Augmented Generation)** pipeline provided by `ElBruno.LocalLLMs.Rag`.

## What It Does

1. **Creates sample documents** — Company policy documents about vacation, remote work, and expenses
2. **Chunks documents** — Breaks them into overlapping chunks using sliding window chunking
3. **Generates embeddings** — Uses a mock embedding generator (replace with real embeddings in production)
4. **Indexes documents** — Stores chunks with their embeddings in an in-memory vector store
5. **Retrieves context** — Performs semantic search to find relevant chunks for user queries
6. **Demonstrates RAG workflow** — Shows how to retrieve context that can be injected into LLM prompts

## Running the Sample

```bash
cd src/samples/RagChatbot
dotnet run
```

## Key Components

- **`IDocumentChunker`** — `SlidingWindowChunker` with configurable chunk size and overlap
- **`IDocumentStore`** — `InMemoryDocumentStore` using cosine similarity for semantic search
- **`IRagPipeline`** — `LocalRagPipeline` orchestrates chunking, embedding, and retrieval
- **`IEmbeddingGenerator`** — MEAI abstraction (use `ElBruno.LocalEmbeddings` for real embeddings)

## Using with Real LLMs

To integrate RAG with a chat completion:

```csharp
var context = await ragPipeline.RetrieveContextAsync(userQuery, topK: 3);

var systemMessage = new ChatMessage(ChatRole.System, 
    $"Use the following context to answer the user's question:\n\n" +
    string.Join("\n\n", context.RetrievedChunks.Select(c => c.Content)));

var messages = new[] { systemMessage, new ChatMessage(ChatRole.User, userQuery) };
var response = await chatClient.GetResponseAsync(messages);
```

## Next Steps

- Replace `MockEmbeddingGenerator` with a real embedding model (e.g., `ElBruno.LocalEmbeddings`)
- Use `SqliteDocumentStore` for persistent storage
- Tune `chunkSize`, `overlap`, `topK`, and `minSimilarity` for your use case
- Combine with `LocalChatClient` for complete RAG-powered chat
- Try the fine-tuned RAG model (`KnownModels.Qwen25_05B_RAG`) for better source citations — see the [Fine-Tuning Guide](../../../docs/fine-tuning-guide.md)
