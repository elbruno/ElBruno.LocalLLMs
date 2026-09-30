using ElBruno.AI.Jev;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Extension methods for registering local decision services in dependency injection.
/// </summary>
public static class DecisionServiceExtensions
{
    /// <summary>
    /// Registers an <see cref="IDecisionClient"/> backed by a local Laya server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure connection and threshold settings.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddLocalDecisions(options =>
    /// {
    ///     options.Endpoint = new Uri("http://127.0.0.1:8000");
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
        services.AddSingleton<IDecisionClient>(_ => new LayaDecisionClient(options));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IDecisionClient"/> over a caller-supplied Jev client.
    /// Useful for tests and for hosts that own the transport.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="jevClient">The Jev decision client to delegate to.</param>
    /// <param name="configureOptions">Optional action to configure threshold settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalDecisions(
        this IServiceCollection services,
        IJevDecisionClient jevClient,
        Action<DecisionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jevClient);

        var options = new DecisionOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IDecisionClient>(_ => new LayaDecisionClient(jevClient, options));

        return services;
    }
}
