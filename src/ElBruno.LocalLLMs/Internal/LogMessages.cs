using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// High-performance log message definitions for ElBruno.LocalLLMs.
/// Uses LoggerMessage.Define for zero-allocation logging when the log level is disabled.
/// </summary>
internal static class LogMessages
{
    // ── Model Loading ──

    private static readonly Action<ILogger, string, ExecutionProvider, Exception?> s_modelLoadingStart =
        LoggerMessage.Define<string, ExecutionProvider>(
            LogLevel.Information,
            new EventId(1, nameof(ModelLoadingStart)),
            "Loading model from '{ModelPath}' with provider {Provider}");

    internal static void ModelLoadingStart(ILogger logger, string modelPath, ExecutionProvider provider)
        => s_modelLoadingStart(logger, modelPath, provider, null);

    private static readonly Action<ILogger, string, ExecutionProvider, double, Exception?> s_modelLoadingComplete =
        LoggerMessage.Define<string, ExecutionProvider, double>(
            LogLevel.Information,
            new EventId(2, nameof(ModelLoadingComplete)),
            "Model loaded from '{ModelPath}' using {Provider} in {ElapsedMs:F0}ms");

    internal static void ModelLoadingComplete(ILogger logger, string modelPath, ExecutionProvider provider, double elapsedMs)
        => s_modelLoadingComplete(logger, modelPath, provider, elapsedMs, null);

    // ── Provider Selection ──

    private static readonly Action<ILogger, ExecutionProvider, Exception?> s_providerAttempt =
        LoggerMessage.Define<ExecutionProvider>(
            LogLevel.Debug,
            new EventId(10, nameof(ProviderAttempt)),
            "Attempting execution provider: {Provider}");

    internal static void ProviderAttempt(ILogger logger, ExecutionProvider provider)
        => s_providerAttempt(logger, provider, null);

    private static readonly Action<ILogger, ExecutionProvider, ExecutionProvider, string, Exception?> s_providerFallback =
        LoggerMessage.Define<ExecutionProvider, ExecutionProvider, string>(
            LogLevel.Warning,
            new EventId(11, nameof(ProviderFallback)),
            "Provider {FailedProvider} unavailable, falling back to {NextProvider}. Reason: {Reason}");

    internal static void ProviderFallback(ILogger logger, ExecutionProvider failedProvider, ExecutionProvider nextProvider, string reason)
        => s_providerFallback(logger, failedProvider, nextProvider, reason, null);

    // ── Model Download ──

    private static readonly Action<ILogger, string, Exception?> s_modelDownloadStart =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(20, nameof(ModelDownloadStart)),
            "Downloading model '{ModelId}'");

    internal static void ModelDownloadStart(ILogger logger, string modelId)
        => s_modelDownloadStart(logger, modelId, null);

    private static readonly Action<ILogger, string, string, Exception?> s_modelDownloadComplete =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(21, nameof(ModelDownloadComplete)),
            "Model '{ModelId}' downloaded to '{Path}'");

    internal static void ModelDownloadComplete(ILogger logger, string modelId, string path)
        => s_modelDownloadComplete(logger, modelId, path, null);

    // ── Inference ──

    private static readonly Action<ILogger, string, bool, Exception?> s_inferenceStart =
        LoggerMessage.Define<string, bool>(
            LogLevel.Debug,
            new EventId(30, nameof(InferenceStart)),
            "Starting inference for model '{ModelId}', streaming={Streaming}");

    internal static void InferenceStart(ILogger logger, string modelId, bool streaming)
        => s_inferenceStart(logger, modelId, streaming, null);

    // ── Errors ──

    private static readonly Action<ILogger, string, Exception?> s_modelInitError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(100, nameof(ModelInitError)),
            "Failed to initialize model: {Reason}");

    internal static void ModelInitError(ILogger logger, string reason, Exception? ex = null)
        => s_modelInitError(logger, reason, ex);

    // ── Options Validation ──

    private static readonly Action<ILogger, Exception?> s_optionsValidated =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(40, nameof(OptionsValidated)),
            "Options validated successfully");

    internal static void OptionsValidated(ILogger logger)
        => s_optionsValidated(logger, null);
}
