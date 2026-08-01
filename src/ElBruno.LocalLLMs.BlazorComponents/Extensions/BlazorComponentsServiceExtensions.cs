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
    ///   <item><see cref="ModelStateService"/> (scoped) — tracks per-model download/lifecycle state.</item>
    /// </list>
    /// <para>
    /// <b>Blazor Server:</b> <see cref="ModelStateService"/> is registered as <i>scoped</i> —
    /// one instance per SignalR circuit, so each browser tab gets its own download state.
    /// </para>
    /// <para>
    /// <b>Blazor WebAssembly:</b> Scoped behaves like singleton in WASM (one app instance),
    /// which is also the correct behaviour here.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddLocalLLMsBlazorComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ModelDownloader is thread-safe and expensive to create — use singleton
        services.AddSingleton<IModelDownloader, ModelDownloader>();

        // ModelStateService is scoped: one per Blazor circuit / WASM app
        services.AddScoped<ModelStateService>();

        return services;
    }
}
