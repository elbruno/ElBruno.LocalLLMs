using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class DecisionOptionsTests
{
    [Fact]
    public void Defaults_TargetLocalLayaServer()
    {
        var options = new DecisionOptions();

        Assert.Equal(new Uri("http://127.0.0.1:8000"), options.Endpoint);
        Assert.Equal(string.Empty, options.ApiKey);
        Assert.Null(options.Model);
        Assert.Equal(0.5, options.DecisionThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8000")]
    [InlineData("http://localhost:9000")]
    [InlineData("https://127.0.0.1:8443")]
    [InlineData("http://[::1]:8000")]
    public void Validate_AcceptsLoopbackEndpoints(string endpoint)
    {
        var options = new DecisionOptions { Endpoint = new Uri(endpoint) };

        options.Validate();
    }

    [Theory]
    [InlineData("http://laya.example.com")]
    [InlineData("https://api.typesafe.ai")]
    [InlineData("http://10.0.0.5:8000")]
    public void Validate_RejectsRemoteEndpoints(string endpoint)
    {
        var options = new DecisionOptions { Endpoint = new Uri(endpoint) };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("loopback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Validate_RejectsThresholdOutsideUnitInterval(double threshold)
    {
        var options = new DecisionOptions { DecisionThreshold = threshold };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveTimeout()
    {
        var options = new DecisionOptions { Timeout = TimeSpan.Zero };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void AddLocalDecisions_RegistersSingletonClient()
    {
        var services = new ServiceCollection();
        var stub = new StubJevClient(StubJevClient.LayaLike);

        services.AddLocalDecisions(stub, options => options.DecisionThreshold = 0.75);

        using ServiceProvider provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IDecisionClient>();
        var second = provider.GetRequiredService<IDecisionClient>();

        Assert.Same(first, second);
        Assert.Equal(0.75, provider.GetRequiredService<DecisionOptions>().DecisionThreshold);
    }

    [Fact]
    public async Task AddLocalDecisions_ResolvedClientUsesConfiguredThreshold()
    {
        var services = new ServiceCollection();
        services.AddLocalDecisions(
            new StubJevClient(StubJevClient.LayaLike),
            options => options.DecisionThreshold = 0.9);

        using ServiceProvider provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IDecisionClient>();

        ProbabilityResult result = await client.AskAsync("text", "proposition");

        Assert.Equal(0.9, result.Threshold);
        Assert.False(result.IsTrue);
    }

    [Fact]
    public void AddLocalDecisions_RejectsInvalidOptionsEagerly()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddLocalDecisions(options => options.Endpoint = new Uri("https://api.typesafe.ai")));
    }
}
