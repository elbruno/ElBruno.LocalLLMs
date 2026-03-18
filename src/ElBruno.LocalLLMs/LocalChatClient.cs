using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs;

/// <summary>
/// Local LLM chat client using ONNX Runtime GenAI.
/// Implements IChatClient for seamless integration with Microsoft.Extensions.AI.
/// </summary>
public sealed class LocalChatClient : IChatClient, IAsyncDisposable
{
    private readonly LocalLLMsOptions _options;
    private readonly IModelDownloader _downloader;
    private readonly IChatTemplateFormatter _formatter;

    private OnnxGenAIModel? _model;
    private string? _resolvedModelPath;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // --- Construction ---

    /// <summary>
    /// Creates a LocalChatClient with default options (Phi-3.5-mini-instruct).
    /// Model is downloaded automatically on first use.
    /// </summary>
    public LocalChatClient()
        : this(new LocalLLMsOptions())
    {
    }

    /// <summary>
    /// Creates a LocalChatClient with the specified options.
    /// </summary>
    public LocalChatClient(LocalLLMsOptions options)
        : this(options, new ModelDownloader())
    {
    }

    internal LocalChatClient(LocalLLMsOptions options, IModelDownloader downloader)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _formatter = ChatTemplateFactory.Create(options.Model.ChatTemplate);

        Metadata = new ChatClientMetadata(
            providerName: "elbruno-local-llms",
            providerUri: new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"),
            modelId: options.Model.Id);
    }

    // --- Async Factory ---

    /// <summary>
    /// Async factory — preferred in async contexts to avoid sync-over-async during model download.
    /// </summary>
    public static async Task<LocalChatClient> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new LocalLLMsOptions(), progress: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Async factory with options and progress reporting.
    /// </summary>
    public static async Task<LocalChatClient> CreateAsync(
        LocalLLMsOptions options,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var client = new LocalChatClient(options);
        await client.EnsureInitializedAsync(progress, cancellationToken).ConfigureAwait(false);
        return client;
    }

    // --- IChatClient Implementation ---

    /// <inheritdoc />
    public ChatClientMetadata Metadata { get; }

    /// <inheritdoc />
    public async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(chatMessages);

        await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

        var prompt = _formatter.FormatMessages(chatMessages);
        var genParams = BuildGenerationParameters(options);

        var responseText = await Task.Run(
            () => _model!.Generate(prompt, genParams, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var responseMessage = new ChatMessage(ChatRole.Assistant, responseText.Trim());

        return new ChatCompletion(responseMessage)
        {
            ModelId = _options.Model.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(chatMessages);

        await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

        var prompt = _formatter.FormatMessages(chatMessages);
        var genParams = BuildGenerationParameters(options);

        await foreach (var token in _model!.GenerateStreamingAsync(prompt, genParams, cancellationToken).ConfigureAwait(false))
        {
            yield return new StreamingChatCompletionUpdate
            {
                Role = ChatRole.Assistant,
                Text = token,
                ModelId = _options.Model.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class
    {
        if (this is TService service)
        {
            return service;
        }

        return null;
    }

    // --- Lifecycle ---

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _model?.Dispose();
        _initLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _model?.Dispose();
        _initLock.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    // --- Private helpers ---

    private async Task EnsureInitializedAsync(
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_model is not null) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_model is not null) return;

            _resolvedModelPath = _options.ModelPath;

            if (_resolvedModelPath is null)
            {
                if (!_options.EnsureModelDownloaded)
                {
                    throw new InvalidOperationException(
                        $"Model '{_options.Model.Id}' is not available locally and EnsureModelDownloaded is false. " +
                        "Set ModelPath to a local model directory or enable EnsureModelDownloaded.");
                }

                _resolvedModelPath = await _downloader.EnsureModelAsync(
                    _options.Model,
                    _options.CacheDirectory,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            _model = new OnnxGenAIModel(
                _resolvedModelPath,
                _options.ExecutionProvider,
                _options.GpuDeviceId);
        }
        finally
        {
            _initLock.Release();
        }
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
