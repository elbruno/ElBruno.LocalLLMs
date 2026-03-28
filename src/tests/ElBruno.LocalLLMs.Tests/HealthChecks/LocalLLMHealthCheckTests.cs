using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ElBruno.LocalLLMs.Tests.HealthChecks;

/// <summary>
/// Tests for <see cref="LocalLLMHealthCheck"/> construction.
/// CheckHealthAsync requires a live ONNX runtime, so we only verify constructor guards.
/// </summary>
public class LocalLLMHealthCheckTests
{
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalLLMHealthCheck(null!));
    }

    [Fact]
    public void LocalLLMHealthCheck_ImplementsIHealthCheck()
    {
        Assert.True(typeof(IHealthCheck).IsAssignableFrom(typeof(LocalLLMHealthCheck)));
    }
}
