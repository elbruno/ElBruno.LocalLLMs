namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Configuration for a local decision client backed by a <c>laya-serve</c> instance.
/// </summary>
public sealed class DecisionOptions
{
    /// <summary>
    /// Gets or sets the address of the local Laya server. Defaults to <c>http://127.0.0.1:8000</c>,
    /// which is where <c>python -m laya.serve</c> listens out of the box.
    /// </summary>
    /// <remarks>
    /// The endpoint must be a loopback address. Plain HTTP is accepted because the traffic never
    /// leaves the machine; a remote address is rejected so that prompts cannot be sent off-box
    /// by a configuration mistake.
    /// </remarks>
    public Uri Endpoint { get; set; } = new("http://127.0.0.1:8000");

    /// <summary>
    /// Gets or sets the bearer token expected by the server. Leave empty unless the server was
    /// started with <c>LAYA_API_KEY</c> set.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the checkpoint to use. Leave <c>null</c> to let Laya's router pick per request,
    /// which is the recommended default.
    /// </summary>
    /// <remarks>
    /// Laya silently falls back to routing when given an unknown identifier, so a typo here
    /// degrades quietly rather than failing loudly. Prefer <c>null</c>.
    /// </remarks>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the per-request timeout. Defaults to 30 seconds, which is generous for CPU
    /// inference on a warm checkpoint but allows for a cold first call.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the probability at or above which <see cref="ProbabilityResult.IsTrue"/> reports true.
    /// Defaults to 0.5.
    /// </summary>
    /// <remarks>
    /// This is a convenience default, not a calibrated boundary. Laya's public checkpoints report
    /// confidence values that are not reliably calibrated, and some ship temperature settings that
    /// Laya itself flags as invalid at startup. Fit this threshold against your own labelled
    /// examples before depending on the boolean.
    /// </remarks>
    public double DecisionThreshold { get; set; } = 0.5;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Endpoint);

        if (!Endpoint.IsLoopback)
        {
            throw new InvalidOperationException(
                $"DecisionOptions.Endpoint must be a loopback address, but was '{Endpoint}'. " +
                "This package talks to a Laya server running on the same machine.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("DecisionOptions.Timeout must be greater than zero.");
        }

        if (DecisionThreshold is < 0 or > 1 || double.IsNaN(DecisionThreshold))
        {
            throw new InvalidOperationException(
                $"DecisionOptions.DecisionThreshold must be between 0 and 1, but was {DecisionThreshold}.");
        }
    }
}
