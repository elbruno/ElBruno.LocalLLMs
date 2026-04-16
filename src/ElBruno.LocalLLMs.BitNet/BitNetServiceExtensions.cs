using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// DI registration for BitNetChatClient.
/// </summary>
public static class BitNetServiceExtensions
{
    /// <summary>
    /// Registers IChatClient backed by BitNetChatClient with default options.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(this IServiceCollection services)
        => services.AddBitNetChatClient(_ => { });

    /// <summary>
    /// Registers IChatClient backed by BitNetChatClient with configured options.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(
        this IServiceCollection services,
        Action<BitNetOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BitNetOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<BitNetOptions>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new BitNetChatClient(opts, loggerFactory);
        });

        return services;
    }
}
