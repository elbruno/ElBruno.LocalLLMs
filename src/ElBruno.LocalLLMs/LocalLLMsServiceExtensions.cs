using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Extension methods for registering LocalChatClient with DI.
/// </summary>
public static class LocalLLMsServiceExtensions
{
    /// <summary>
    /// Registers IChatClient as a singleton using default options (Phi-3.5-mini).
    /// </summary>
    public static IServiceCollection AddLocalLLMs(this IServiceCollection services)
    {
        return services.AddLocalLLMs(_ => { });
    }

    /// <summary>
    /// Registers IChatClient as a singleton with configured options.
    /// </summary>
    public static IServiceCollection AddLocalLLMs(
        this IServiceCollection services,
        Action<LocalLLMsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LocalLLMsOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IModelDownloader, ModelDownloader>();
        services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<LocalLLMsOptions>();
            var downloader = sp.GetRequiredService<IModelDownloader>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new LocalChatClient(opts, downloader, loggerFactory);
        });

        return services;
    }
}
