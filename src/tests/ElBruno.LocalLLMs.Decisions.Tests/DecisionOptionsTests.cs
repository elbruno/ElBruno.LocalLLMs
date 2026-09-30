using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalLLMs.Decisions.Tests;

public class DecisionOptionsTests
{
    [Fact]
    public void Defaults_RunTheModelLocallyFromAKnownRepository()
    {
        var options = new DecisionOptions();

        Assert.Equal("elbruno/laya-onnx", options.ModelRepository);
        Assert.Null(options.ModelPath);
        Assert.Null(options.CacheDirectory);
        Assert.Null(options.IntraOpNumThreads);
        Assert.Equal(0.5, options.DecisionThreshold);
    }

    [Fact]
    public void Validate_AcceptsTheDefaults()
    {
        new DecisionOptions().Validate();
    }

    [Fact]
    public void Validate_AcceptsALocalModelPathWithoutARepository()
    {
        var options = new DecisionOptions { ModelRepository = string.Empty, ModelPath = @"C:\models\laya" };

        options.Validate();
    }

    [Fact]
    public void Validate_RejectsHavingNeitherASourceNorAPath()
    {
        var options = new DecisionOptions { ModelRepository = "  " };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("ModelPath", exception.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Validate_RejectsNonPositiveThreadCounts(int threads)
    {
        var options = new DecisionOptions { IntraOpNumThreads = threads };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ResolveCacheDirectory_PrefersAnExplicitDirectory()
    {
        var options = new DecisionOptions { CacheDirectory = @"D:\cache" };

        Assert.Equal(@"D:\cache", options.ResolveCacheDirectory());
    }

    [Fact]
    public void ResolveCacheDirectory_FallsBackToLocalApplicationData()
    {
        var resolved = new DecisionOptions().ResolveCacheDirectory();

        Assert.Contains("ElBruno", resolved, StringComparison.Ordinal);
        Assert.Contains("decisions", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void AddLocalDecisions_RegistersASingletonClient()
    {
        var services = new ServiceCollection();

        services.AddLocalDecisions(options => options.DecisionThreshold = 0.75);

        using ServiceProvider provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IDecisionClient>();
        var second = provider.GetRequiredService<IDecisionClient>();

        Assert.Same(first, second);
        Assert.IsType<LayaOnnxDecisionClient>(first);
        Assert.Equal(0.75, provider.GetRequiredService<DecisionOptions>().DecisionThreshold);
    }

    [Fact]
    public void AddLocalDecisions_DoesNotLoadTheModelDuringRegistration()
    {
        // Registration must stay cheap: the model is several hundred megabytes and is fetched
        // lazily on first use, so adding the service cannot block startup or touch the network.
        var services = new ServiceCollection();
        services.AddLocalDecisions(options => options.ModelPath = @"C:\does\not\exist");

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IDecisionClient>());
    }

    [Fact]
    public void AddLocalDecisions_RejectsInvalidOptionsEagerly()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddLocalDecisions(options => options.DecisionThreshold = 2.0));
    }
}
