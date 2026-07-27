using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Local VLM chat client for vision-language models using ONNX Runtime GenAI.
/// Implements IChatClient for integration with Microsoft.Extensions.AI.
/// Use <see cref="VisionChatOptions"/> to supply image paths per call.
/// Set <see cref="LocalLLMsOptions.EnsureModelDownloaded"/> to <see langword="true"/> to auto-download
/// the model on first use (requires <see cref="ModelDefinition.HasNativeOnnx"/> to be <see langword="true"/>),
/// or set <see cref="LocalLLMsOptions.ModelPath"/> to use a local directory directly.
/// </summary>
public sealed class LocalVisionChatClient : IChatClient, IAsyncDisposable
{
    private readonly LocalLLMsOptions _options;
    private readonly IModelDownloader _downloader;
    private readonly IVisionGenerationModelFactory _modelFactory;
    private readonly IChatTemplateFormatter _formatter;
    private readonly ILogger _logger;

    private IVisionGenerationModel? _model;
    private string? _resolvedModelPath;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // --- Construction ---

    /// <summary>
    /// Creates a LocalVisionChatClient with the specified options.
    /// Set <see cref="LocalLLMsOptions.EnsureModelDownloaded"/> to auto-download, or
    /// <see cref="LocalLLMsOptions.ModelPath"/> to use a local directory directly.
    /// </summary>
    public LocalVisionChatClient(LocalLLMsOptions options)
        : this(options, loggerFactory: null)
    {
    }

    /// <summary>
    /// Creates a LocalVisionChatClient with the specified options and logger factory.
    /// </summary>
    public LocalVisionChatClient(LocalLLMsOptions options, ILoggerFactory? loggerFactory)
        : this(options, new OnnxVisionModelFactory(), new ModelDownloader(), loggerFactory)
    {
    }

    internal LocalVisionChatClient(
        LocalLLMsOptions options,
        IVisionGenerationModelFactory modelFactory,
        IModelDownloader? downloader = null,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _modelFactory = modelFactory ?? throw new ArgumentNullException(nameof(modelFactory));
        _downloader = downloader ?? new ModelDownloader();
        _formatter = ChatTemplateFactory.Create(options.Model.ChatTemplate);
        _logger = loggerFactory?.CreateLogger<LocalVisionChatClient>() ?? NullLogger<LocalVisionChatClient>.Instance;

        Metadata = new ChatClientMetadata(
            providerName: "elbruno-local-llms-vision",
            providerUri: new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"),
            defaultModelId: options.Model.Id);
    }

    // --- Async Factory ---

    /// <summary>
    /// Async factory — preferred in async contexts to avoid sync-over-async during model download.
    /// Initializes the model (downloading if needed) before returning.
    /// </summary>
    public static async Task<LocalVisionChatClient> CreateAsync(
        LocalLLMsOptions options,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var client = new LocalVisionChatClient(options);
        await client.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// Removes all cached files for the specified model from the local cache.
    /// No-op if the model is not currently cached.
    /// </summary>
    public static Task DeleteModelFromCacheAsync(
        ModelDefinition model,
        string? cacheDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return new ModelDownloader().DeleteModelAsync(model, cacheDirectory, cancellationToken);
    }

    // --- IChatClient ---

    /// <summary>
    /// Metadata describing this chat client provider and model.
    /// </summary>
    public ChatClientMetadata Metadata { get; }

    /// <summary>
    /// The active execution provider selected by runtime initialization.
    /// </summary>
    public ExecutionProvider ActiveExecutionProvider => _model?.ActiveProvider ?? _options.ExecutionProvider;

    /// <summary>
    /// Metadata about the loaded model. Returns null before initialization.
    /// </summary>
    public ModelMetadata? ModelInfo => _model?.Metadata;

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var visionOpts = options as VisionChatOptions;
        var imagePaths = visionOpts?.ImagePaths ?? [];

        var prompt = BuildPrompt(messageList, imagePaths);
        var genParams = BuildGenerationParameters(options);

        var result = await Task.Run(
            () => _model!.GenerateWithImages(prompt, imagePaths, genParams, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var responseMessage = new ChatMessage(ChatRole.Assistant, result.Text.Trim());

        return new ChatResponse(responseMessage)
        {
            ModelId = _options.Model.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var visionOpts = options as VisionChatOptions;
        var imagePaths = visionOpts?.ImagePaths ?? [];

        var prompt = BuildPrompt(messageList, imagePaths);
        var genParams = BuildGenerationParameters(options);

        var enumerator = _model!.GenerateWithImagesStreamingAsync(prompt, imagePaths, genParams, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                if (!hasNext) break;

                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, enumerator.Current)
                {
                    ModelId = _options.Model.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(LocalVisionChatClient) || serviceType == typeof(IChatClient))
            return serviceKey is null ? this : null;
        return null;
    }

    // --- Lifecycle ---

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _model?.Dispose();
        _initLock.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _model?.Dispose();
        _initLock.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    // --- Private helpers ---

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_model is not null) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_model is not null) return;

            if (_options.ModelPath is not null)
            {
                _resolvedModelPath = _options.ModelPath;
            }
            else if (_options.EnsureModelDownloaded)
            {
                _resolvedModelPath = await _downloader.EnsureModelAsync(
                    _options.Model,
                    _options.CacheDirectory,
                    progress: null,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    $"VLM '{_options.Model.Id}' requires either a local model path (set LocalLLMsOptions.ModelPath) " +
                    "or auto-download enabled (set EnsureModelDownloaded = true, requires HasNativeOnnx = true). " +
                    "See docs/onnx-conversion-fara.md for details.");
            }

            _model = _modelFactory.Create(
                _resolvedModelPath,
                _options.ExecutionProvider,
                _options.GpuDeviceId,
                _options.MaxSequenceLength,
                _logger);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string BuildPrompt(IList<ChatMessage> messages, string[] imagePaths)
    {
        // Use FaraFormatter's image-aware method when available, so vision tokens
        // are injected only when images are actually present.
        if (_formatter is FaraFormatter faraFormatter)
            return faraFormatter.FormatMessagesWithImages(messages, hasImages: imagePaths.Length > 0);

        return _formatter.FormatMessages(messages);
    }

    private GenerationParameters BuildGenerationParameters(ChatOptions? options)
    {
        var maxLength = _options.MaxSequenceLength;
        var temperature = _options.Temperature;
        var topP = _options.TopP;
        int? topK = null;
        var repetitionPenalty = 1.0f;

        if (options is not null)
        {
            if (options.MaxOutputTokens.HasValue)
                maxLength = options.MaxOutputTokens.Value;
            if (options.Temperature.HasValue)
                temperature = options.Temperature.Value;
            if (options.TopP.HasValue)
                topP = options.TopP.Value;
            if (options.TopK.HasValue)
                topK = options.TopK.Value;
            if (options.FrequencyPenalty.HasValue)
                repetitionPenalty = options.FrequencyPenalty.Value;
        }

        return new GenerationParameters(
            MaxLength: maxLength,
            Temperature: temperature,
            TopP: topP,
            TopK: topK,
            RepetitionPenalty: repetitionPenalty);
    }
}
