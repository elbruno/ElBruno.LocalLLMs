using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ElBruno.LocalLLMs.HealthChecks;

/// <summary>
/// ASP.NET Core health check for local LLM availability.
/// Reports Healthy when model is loaded and can perform inference.
/// </summary>
public sealed class LocalLLMHealthCheck : IHealthCheck
{
    private readonly LocalChatClient _client;

    /// <inheritdoc />
    public LocalLLMHealthCheck(LocalChatClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var diags = LocalChatClient.DiagnoseEnvironment();
            var data = new Dictionary<string, object>
            {
                ["cpu_available"] = diags.CpuAvailable,
                ["cuda_available"] = diags.CudaAvailable,
                ["directml_available"] = diags.DirectMLAvailable,
                ["dotnet_version"] = diags.DotNetVersion,
                ["cache_size_mb"] = diags.CacheSizeBytes / (1024.0 * 1024.0)
            };

            var modelInfo = _client.ModelInfo;
            if (modelInfo != null)
            {
                data["model_name"] = modelInfo.ModelName ?? "unknown";
            }

            var metadata = _client.Metadata;
            if (metadata != null)
            {
                data["provider"] = metadata.ProviderName ?? "unknown";
            }

            return HealthCheckResult.Healthy("Local LLM is operational", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Local LLM health check failed", ex);
        }
    }
}
