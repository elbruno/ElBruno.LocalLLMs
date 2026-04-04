using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.Rag;

/// <summary>
/// Extension methods for configuring RAG services in dependency injection.
/// </summary>
public static class RagServiceExtensions
{
    /// <summary>
    /// Adds the local RAG pipeline with in-memory document store to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure RAG options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalRagPipeline(
        this IServiceCollection services,
        Action<RagOptions>? configureOptions = null)
    {
        var options = new RagOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IDocumentChunker>(sp =>
            new SlidingWindowChunker(options.ChunkSize, options.ChunkOverlap));
        services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();
        services.AddSingleton<IRagPipeline, LocalRagPipeline>();

        return services;
    }

    /// <summary>
    /// Adds the local RAG pipeline with a specific embedding generator and in-memory document store to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="embeddingGenerator">The embedding generator to use for creating document embeddings.</param>
    /// <param name="configureOptions">Optional action to configure RAG options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalRagPipeline(
        this IServiceCollection services,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        Action<RagOptions>? configureOptions = null)
    {
        services.AddSingleton(embeddingGenerator);
        return services.AddLocalRagPipeline(configureOptions);
    }

    /// <summary>
    /// Adds a SQLite-based document store to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQLite database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqliteDocumentStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IDocumentStore>(sp =>
            new SqliteDocumentStore(connectionString));

        return services;
    }
}
