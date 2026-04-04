# Zero-Cloud RAG Sample

A complete **Retrieval-Augmented Generation** console app that runs entirely on your machine — no cloud APIs needed.

## What It Does

This sample demonstrates the full RAG pipeline end-to-end:

1. **Creates sample documents** — Five .NET/C# programming tips
2. **Generates real embeddings** — Uses `ElBruno.LocalEmbeddings` (all-MiniLM-L6-v2 ONNX model)
3. **Chunks documents** — Sliding window chunker with overlap
4. **Indexes into a vector store** — In-memory document store with cosine similarity
5. **Retrieves relevant context** — Semantic search for the user's question
6. **Generates a grounded answer** — Phi-3.5-mini-instruct produces a response using only the retrieved context

## Architecture

```
┌────────────┐     ┌──────────┐     ┌────────────┐     ┌──────────────┐
│  Documents │────▶│ Chunking │────▶│ Embedding  │────▶│ Vector Store │
│  (5 tips)  │     │ (sliding │     │ (MiniLM    │     │ (in-memory)  │
│            │     │  window) │     │  ONNX)     │     │              │
└────────────┘     └──────────┘     └────────────┘     └──────┬───────┘
                                                              │
                  ┌──────────┐     ┌────────────┐     ┌──────▼───────┐
                  │  Answer  │◀────│    LLM     │◀────│  Retrieval   │
                  │ (stdout) │     │ (Phi-3.5   │     │ (top-K       │
                  │          │     │  mini)     │     │  similarity) │
                  └──────────┘     └────────────┘     └──────────────┘
                                         ▲
                                         │
                                   ┌─────┴──────┐
                                   │ User Query │
                                   └────────────┘
```

## How to Run

```bash
dotnet run --project src/samples/ZeroCloudRag
```

> **Note:** Requires .NET 10 SDK or later (the `ElBruno.LocalEmbeddings` package targets `net10.0`).

### First Run

On the first run, two models are automatically downloaded:

| Model | Size | Purpose |
|-------|------|---------|
| `sentence-transformers/all-MiniLM-L6-v2` | ~80 MB | Embedding generation |
| `microsoft/Phi-3.5-mini-instruct` (ONNX) | ~2.4 GB | Chat completion |

Downloads are cached locally — subsequent runs start immediately.

## Dependencies

| Package | Purpose |
|---------|---------|
| `ElBruno.LocalLLMs` | Local LLM chat client (Phi-3.5 mini) |
| `ElBruno.LocalLLMs.Rag` | RAG pipeline, chunking, vector store |
| `ElBruno.LocalEmbeddings` | Real local embeddings (all-MiniLM ONNX) |
| `Microsoft.ML.OnnxRuntimeGenAI` | ONNX Runtime for LLM inference |

## GPU Support

Replace the CPU package in the `.csproj` for GPU acceleration:

```xml
<!-- CUDA (NVIDIA) -->
<PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.Cuda" Version="0.12.2" />

<!-- DirectML (Windows, any GPU) -->
<PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.DirectML" Version="0.12.2" />
```

> **Everything runs locally — no cloud APIs needed.**
