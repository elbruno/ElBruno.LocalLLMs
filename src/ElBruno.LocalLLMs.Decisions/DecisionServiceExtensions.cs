using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Extension methods for registering local decision services in dependency injection.
/// </summary>
public static class DecisionServiceExtensions
{
    /// <summary>Registers a local Kev 0.8B ONNX export as the shared decision provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelPath">Directory produced by <c>scripts/convert_kev.py</c>. No model is downloaded.</param>
    /// <param name="intraOpNumThreads">CPU threads within each native operator.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>The first registered shared provider keeps the <see cref="IDecisionClient"/> slot.</remarks>
    public static IServiceCollection AddLocalKevDecisions(this IServiceCollection services, string modelPath, int intraOpNumThreads = 4)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intraOpNumThreads);
        services.AddSingleton(_ => new KevOnnxDecisionClient(modelPath, intraOpNumThreads));
        services.TryAddSingleton<IDecisionClient>(provider => provider.GetRequiredService<KevOnnxDecisionClient>());
        return services;
    }

    /// <summary>
    /// Registers a Laya decision model that runs in-process through ONNX Runtime, as the shared
    /// <see cref="IDecisionClient"/> provider and as <see cref="ILayaDecisionClient"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure the model source and threshold.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The client is registered as a singleton because loading the model is expensive and the
    /// loaded session is safe to share. The model is downloaded lazily on first use, so
    /// registration does not block startup.
    /// The shared <see cref="IDecisionClient"/> uses <c>TryAdd</c>, like the other ElBruno.AI.Decisions providers,
    /// so the first provider registered keeps that slot; <see cref="ILayaDecisionClient"/> is always available.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddLocalDecisions(options =>
    /// {
    ///     options.ModelRepository = "elbruno/laya-onnx";
    ///     options.DecisionThreshold = 0.7;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalDecisions(
        this IServiceCollection services,
        Action<DecisionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DecisionOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton(_ => new LayaOnnxDecisionClient(options));
        services.AddSingleton<ILayaDecisionClient>(provider => provider.GetRequiredService<LayaOnnxDecisionClient>());
        services.TryAddSingleton<IDecisionClient>(provider => provider.GetRequiredService<LayaOnnxDecisionClient>());

        return services;
    }
}
