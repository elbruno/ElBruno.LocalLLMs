using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.Tests;

public class ConsoleDownloadProgressRendererTests
{
    [Fact]
    public void InteractiveRenderer_ThrottlesFrequentUpdates()
    {
        var renderer = new ConsoleDownloadProgressRenderer(isInteractive: true, minimumInterval: TimeSpan.FromMilliseconds(200));
        var now = DateTimeOffset.UtcNow;

        var first = renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 10, 100, 0.10), now);
        var second = renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 11, 100, 0.101), now.AddMilliseconds(50));

        Assert.True(first.HasValue);
        Assert.False(second.HasValue);
    }

    [Fact]
    public void InteractiveRenderer_EmitsWhenFileChangesEvenWithinThrottleWindow()
    {
        var renderer = new ConsoleDownloadProgressRenderer(isInteractive: true, minimumInterval: TimeSpan.FromMilliseconds(200));
        var now = DateTimeOffset.UtcNow;

        var first = renderer.BuildUpdate(new ModelDownloadProgress("part1.bin", 10, 100, 0.10), now);
        var second = renderer.BuildUpdate(new ModelDownloadProgress("part2.bin", 20, 100, 0.11), now.AddMilliseconds(50));

        Assert.True(first.HasValue);
        Assert.True(second.HasValue);
        Assert.True(second.Value.InPlace);
        Assert.Contains("part2.bin", second.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RedirectedRenderer_EmitsConcisePeriodicLines()
    {
        var renderer = new ConsoleDownloadProgressRenderer(isInteractive: false);
        var now = DateTimeOffset.UtcNow;

        var first = renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 1, 100, 0.01), now);
        var second = renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 5, 100, 0.05), now.AddMilliseconds(20));
        var third = renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 11, 100, 0.11), now.AddMilliseconds(40));

        Assert.True(first.HasValue);
        Assert.False(first.Value.InPlace);
        Assert.False(second.HasValue);
        Assert.True(third.HasValue);
        Assert.Contains("11.0%", third.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void InteractiveRenderer_NeedsFinalNewLine_AfterAnyOutput()
    {
        var renderer = new ConsoleDownloadProgressRenderer(isInteractive: true);
        var now = DateTimeOffset.UtcNow;

        renderer.BuildUpdate(new ModelDownloadProgress("weights.bin", 10, 100, 0.10), now);

        Assert.True(renderer.NeedsFinalNewLine);
    }
}