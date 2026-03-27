using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ElBruno.LocalLLMs.Rag.Storage;

public sealed class SqliteDocumentStore : IDocumentStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteDocumentStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS chunks (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                content TEXT NOT NULL,
                embedding BLOB NOT NULL,
                metadata TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_document_id ON chunks(document_id);
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO chunks (id, document_id, content, embedding, metadata)
            VALUES (@id, @documentId, @content, @embedding, @metadata)
        ";

        cmd.Parameters.AddWithValue("@id", chunk.Id);
        cmd.Parameters.AddWithValue("@documentId", chunk.DocumentId);
        cmd.Parameters.AddWithValue("@content", chunk.Content);
        cmd.Parameters.AddWithValue("@embedding", SerializeEmbedding(chunk.Embedding));
        cmd.Parameters.AddWithValue("@metadata",
            chunk.Metadata != null ? JsonSerializer.Serialize(chunk.Metadata) : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 5,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, document_id, content, embedding, metadata FROM chunks";

        var results = new List<(DocumentChunk Chunk, float Similarity)>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var documentId = reader.GetString(1);
            var content = reader.GetString(2);
            var embeddingBytes = (byte[])reader[3];
            var metadataJson = reader.IsDBNull(4) ? null : reader.GetString(4);

            var embedding = DeserializeEmbedding(embeddingBytes);
            var metadata = metadataJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson)
                : null;

            var chunk = new DocumentChunk(id, documentId, content, embedding, metadata);
            var similarity = CosineSimilarity(queryEmbedding.Span, embedding.Span);

            if (similarity >= minSimilarity)
            {
                results.Add((chunk, similarity));
            }
        }

        return results
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM chunks";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static byte[] SerializeEmbedding(ReadOnlyMemory<float> embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding.ToArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static ReadOnlyMemory<float> DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        if (a.Length == 0)
            return 0.0f;

        float dotProduct = 0.0f;
        float normA = 0.0f;
        float normB = 0.0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        if (denominator == 0.0f)
            return 0.0f;

        return dotProduct / denominator;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
