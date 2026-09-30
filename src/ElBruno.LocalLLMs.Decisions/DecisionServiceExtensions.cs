using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Extension methods for registering local decision services in dependency injection.
/// </summary>
public static class DecisionServiceExtensions
{
    /// <summary>
    /// Registers an <see cref="IDecisionClient"/> that runs a Laya decision model in-process
    /// through ONNX Runtime.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure the model source and threshold.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The client is registered as a singleton because loading the model is expensive and the
    /// loaded session is safe to share. The model is downloaded lazily on first use, so
    /// registration does not block startup.
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
        services.AddSingleton<IDecisionClient>(_ => new LayaOnnxDecisionClient(options));

        return services;
    }
}
