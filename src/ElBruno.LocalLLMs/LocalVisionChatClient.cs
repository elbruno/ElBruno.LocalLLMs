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
/// </summary>
public sealed class LocalVisionChatClient : IChatClient, IAsyncDisposable
{
    private readonly LocalLLMsOptions _options;
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
    /// Model path must be set via <see cref="LocalLLMsOptions.ModelPath"/> — VLMs require
    /// community ONNX conversion (see docs/onnx-conversion-fara.md).
    /// </summary>
    public LocalVisionChatClient(LocalLLMsOptions options)
        : this(options, loggerFactory: null)
    {
    }

    /// <summary>
    /// Creates a LocalVisionChatClient with the specified options and logger factory.
    /// </summary>
    public LocalVisionChatClient(LocalLLMsOptions options, ILoggerFactory? loggerFactory)
        : this(options, new OnnxVisionModelFactory(), loggerFactory)
    {
    }

    internal LocalVisionChatClient(
        LocalLLMsOptions options,
        IVisionGenerationModelFactory modelFactory,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _modelFactory = modelFactory ?? throw new ArgumentNullException(nameof(modelFactory));
        _formatter = ChatTemplateFactory.Create(options.Model.ChatTemplate);
        _logger = loggerFactory?.CreateLogger<LocalVisionChatClient>() ?? NullLogger<LocalVisionChatClient>.Instance;

        Metadata = new ChatClientMetadata(
            providerName: "elbruno-local-llms-vision",
            providerUri: new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"),
            defaultModelId: options.Model.Id);
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

            _resolvedModelPath = _options.ModelPath
                ?? throw new InvalidOperationException(
                    $"VLM '{_options.Model.Id}' requires a local model path. " +
                    "Set LocalLLMsOptions.ModelPath to the ONNX conversion output directory. " +
                    "See docs/onnx-conversion-fara.md for conversion instructions.");

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
