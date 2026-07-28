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

        // Fail fast when auto-download is requested for a model with no published ONNX artifacts.
        // This prevents silent failures and the confusing "auto-download enabled" UX that still
        // requires manual ONNX conversion by the user.
        if (options.EnsureModelDownloaded
            && string.IsNullOrEmpty(options.ModelPath)
            && !options.Model.HasNativeOnnx)
        {
            throw new InvalidOperationException(
                $"Model '{options.Model.DisplayName}' ('{options.Model.HuggingFaceRepoId}') does not have " +
                $"ONNX artifacts published for auto-download (HasNativeOnnx=false). Either:" + Environment.NewLine +
                $"  - Set ModelPath to a local directory containing the model converted to ONNX, or" + Environment.NewLine +
                $"  - Choose a model with HasNativeOnnx=true (see KnownModels for available models), or" + Environment.NewLine +
                $"  - Set EnsureModelDownloaded=false and supply ModelPath explicitly.");
        }
    }
}
