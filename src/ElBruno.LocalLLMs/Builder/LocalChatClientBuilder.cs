using Microsoft.Extensions.Logging;

namespace ElBruno.LocalLLMs.Builder;

/// <summary>
/// Fluent builder for creating LocalChatClient instances.
/// </summary>
public sealed class LocalChatClientBuilder
{
    private readonly LocalLLMsOptions _options = new();
    private ILoggerFactory? _loggerFactory;

    /// <summary>Selects a known model by its ID string.</summary>
    public LocalChatClientBuilder WithModel(string modelName)
    {
        var model = KnownModels.FindById(modelName);
        if (model is not null)
        {
            _options.Model = model;
        }
        else
        {
            throw new ArgumentException($"Unknown model '{modelName}'. Use KnownModels.All to see available models.", nameof(modelName));
        }

        return this;
    }

    /// <summary>Selects a model using a <see cref="ModelDefinition"/> instance.</summary>
    public LocalChatClientBuilder WithModel(ModelDefinition model)
    {
        _options.Model = model ?? throw new ArgumentNullException(nameof(model));
        return this;
    }

    /// <summary>Sets the local model directory path (skips download).</summary>
    public LocalChatClientBuilder WithModelPath(string modelPath)
    {
        _options.ModelPath = modelPath;
        return this;
    }

    /// <summary>Sets the hardware execution provider.</summary>
    public LocalChatClientBuilder WithExecutionProvider(ExecutionProvider provider)
    {
        _options.ExecutionProvider = provider;
        return this;
    }

    /// <summary>Sets the GPU device ID for CUDA/DirectML.</summary>
    public LocalChatClientBuilder WithGpuDeviceId(int deviceId)
    {
        _options.GpuDeviceId = deviceId;
        return this;
    }

    /// <summary>Sets the maximum sequence length for generation.</summary>
    public LocalChatClientBuilder WithMaxSequenceLength(int maxLength)
    {
        _options.MaxSequenceLength = maxLength;
        return this;
    }

    /// <summary>Sets the default temperature for generation.</summary>
    public LocalChatClientBuilder WithTemperature(float temperature)
    {
        _options.Temperature = temperature;
        return this;
    }

    /// <summary>Sets the default top-p for generation.</summary>
    public LocalChatClientBuilder WithTopP(float topP)
    {
        _options.TopP = topP;
        return this;
    }

    /// <summary>Sets the cache directory for downloaded models.</summary>
    public LocalChatClientBuilder WithCacheDirectory(string cacheDirectory)
    {
        _options.CacheDirectory = cacheDirectory;
        return this;
    }

    /// <summary>Sets a system prompt prepended to conversations.</summary>
    public LocalChatClientBuilder WithSystemPrompt(string systemPrompt)
    {
        _options.SystemPrompt = systemPrompt;
        return this;
    }

    /// <summary>Sets the logger factory for diagnostics.</summary>
    public LocalChatClientBuilder WithLogger(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>Controls whether the model is auto-downloaded if not cached.</summary>
    public LocalChatClientBuilder EnsureModelDownloaded(bool ensure = true)
    {
        _options.EnsureModelDownloaded = ensure;
        return this;
    }

    /// <summary>
    /// Builds and initializes a new LocalChatClient with the configured options.
    /// </summary>
    public async Task<LocalChatClient> BuildAsync(CancellationToken cancellationToken = default)
    {
        return await LocalChatClient.CreateAsync(_options, progress: null, _loggerFactory, cancellationToken).ConfigureAwait(false);
    }
}
