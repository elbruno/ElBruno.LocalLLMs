using ElBruno.LocalLLMs.BitNet;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetServiceExtensions"/> — DI registration.
/// NOTE: Resolution tests are skipped because BitNetChatClient requires native library.
/// We test registration only (service descriptors), not resolution.
/// </summary>
public class BitNetServiceExtensionsTests
{
    // ──────────────────────────────────────────────
    // AddBitNetChatClient — no-arg overload
    // ──────────────────────────────────────────────

    [Fact]
    public void AddBitNetChatClient_Default_RegistersIChatClient()
    {
        var services = new ServiceCollection();

        services.AddBitNetChatClient();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IChatClient));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddBitNetChatClient_Default_RegistersBitNetOptions()
    {
        var services = new ServiceCollection();

        services.AddBitNetChatClient();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(BitNetOptions));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddBitNetChatClient_Default_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddBitNetChatClient();

        Assert.Same(services, result);
    }

    // ──────────────────────────────────────────────
    // AddBitNetChatClient — with configure action
    // ──────────────────────────────────────────────

    [Fact]
    public void AddBitNetChatClient_WithConfigure_RegistersIChatClient()
    {
        var services = new ServiceCollection();

        services.AddBitNetChatClient(options =>
        {
            options.Model = BitNetKnownModels.Falcon3_1B;
        });

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IChatClient));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddBitNetChatClient_WithConfigure_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddBitNetChatClient(options =>
        {
            options.MaxTokens = 1024;
        });

        Assert.Same(services, result);
    }

    [Fact]
    public void AddBitNetChatClient_WithConfigure_AppliesOptionsToRegisteredInstance()
    {
        var services = new ServiceCollection();

        services.AddBitNetChatClient(options =>
        {
            options.Model = BitNetKnownModels.BitNet07B;
            options.MaxTokens = 512;
            options.Temperature = 0.3f;
        });

        // Verify that BitNetOptions singleton has the configured values
        var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetRequiredService<BitNetOptions>();
        Assert.Equal(BitNetKnownModels.BitNet07B, registeredOptions.Model);
        Assert.Equal(512, registeredOptions.MaxTokens);
        Assert.Equal(0.3f, registeredOptions.Temperature);
    }

    // ──────────────────────────────────────────────
    // Null argument checks
    // ──────────────────────────────────────────────

    [Fact]
    public void AddBitNetChatClient_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddBitNetChatClient(_ => { }));
    }

    [Fact]
    public void AddBitNetChatClient_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddBitNetChatClient(null!));
    }

    // ──────────────────────────────────────────────
    // Registration details
    // ──────────────────────────────────────────────

    [Fact]
    public void AddBitNetChatClient_RegistersIChatClientAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddBitNetChatClient();

        var descriptor = services.First(s => s.ServiceType == typeof(IChatClient));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddBitNetChatClient_RegistersBitNetOptionsAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddBitNetChatClient();

        var descriptor = services.First(s => s.ServiceType == typeof(BitNetOptions));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    // ──────────────────────────────────────────────
    // Extension method existence
    // ──────────────────────────────────────────────

    [Fact]
    public void ExtensionMethod_NoArgs_Exists()
    {
        var method = typeof(BitNetServiceExtensions)
            .GetMethods()
            .Where(m => m.Name == "AddBitNetChatClient")
            .FirstOrDefault(m => m.GetParameters().Length == 1);

        Assert.NotNull(method);
    }

    [Fact]
    public void ExtensionMethod_WithConfigure_Exists()
    {
        var method = typeof(BitNetServiceExtensions)
            .GetMethods()
            .Where(m => m.Name == "AddBitNetChatClient")
            .FirstOrDefault(m => m.GetParameters().Length == 2);

        Assert.NotNull(method);
    }
}
