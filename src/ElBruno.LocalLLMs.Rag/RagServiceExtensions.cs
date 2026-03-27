using ElBruno.LocalLLMs.Rag.Chunking;
using ElBruno.LocalLLMs.Rag.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.Rag;

public static class RagServiceExtensions
{
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

    public static IServiceCollection AddLocalRagPipeline(
        this IServiceCollection services,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        Action<RagOptions>? configureOptions = null)
    {
        services.AddSingleton(embeddingGenerator);
        return services.AddLocalRagPipeline(configureOptions);
    }

    public static IServiceCollection AddSqliteDocumentStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IDocumentStore>(sp =>
            new SqliteDocumentStore(connectionString));

        return services;
    }
}
