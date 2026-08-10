using ElBruno.LocalLLMs.BlazorComponents.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.BlazorComponents.Extensions;

/// <summary>
/// Extension methods for registering ElBruno.LocalLLMs Blazor components with the DI container.
/// </summary>
public static class BlazorComponentsServiceExtensions
{
    /// <summary>
    /// Registers the services required by ElBruno.LocalLLMs Blazor components.
    /// Call this in your Blazor app's <c>Program.cs</c>:
    /// <code>builder.Services.AddLocalLLMsBlazorComponents();</code>
    /// </summary>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="IModelDownloader"/> (singleton) — downloads and caches models.</item>
    ///   <item><see cref="ModelStateService"/> (singleton) — tracks application-wide per-model download/lifecycle state.</item>
    /// </list>
    /// <para>
    /// <b>Blazor Server:</b> <see cref="ModelStateService"/> is registered as
    /// <i>singleton</i> so SignalR circuit teardown cannot cancel an active download.
    /// All circuits observe the same model state and can use explicit model-global
    /// cancellation.
    /// </para>
    /// <para>
    /// <b>Blazor WebAssembly:</b> singleton provides the same one-app-instance
    /// behaviour.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddLocalLLMsBlazorComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ModelDownloader is thread-safe and expensive to create — use singleton
        services.AddSingleton<IModelDownloader, ModelDownloader>();

        // ModelStateService is application-wide so circuit teardown does not cancel downloads.
        services.AddSingleton<ModelStateService>();

        return services;
    }
}
