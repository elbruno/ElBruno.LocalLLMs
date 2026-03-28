namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Validates <see cref="LocalLLMsOptions"/> values before model initialization.
/// </summary>
internal static class OptionsValidator
{
    internal static void Validate(LocalLLMsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxSequenceLength < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxSequenceLength), options.MaxSequenceLength, "Must be >= 1");

        if (options.GpuDeviceId < 0)
            throw new ArgumentOutOfRangeException(nameof(options.GpuDeviceId), options.GpuDeviceId, "Must be >= 0");

        if (options.Temperature < 0)
            throw new ArgumentOutOfRangeException(nameof(options.Temperature), options.Temperature, "Must be >= 0");

        if (!string.IsNullOrEmpty(options.ModelPath) && !Directory.Exists(options.ModelPath))
            throw new DirectoryNotFoundException($"ModelPath '{options.ModelPath}' does not exist");
    }
}
