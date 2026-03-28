using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Internal;
using ElBruno.LocalLLMs.ToolCalling;
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
    private readonly IToolCallParser _toolCallParser;

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
        _toolCallParser = ToolCallParserFactory.Create(options.Model.ChatTemplate);

        Metadata = new ChatClientMetadata(
            providerName: "elbruno-local-llms",
            providerUri: new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"),
            defaultModelId: options.Model.Id);
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

    /// <summary>
    /// Metadata describing this chat client provider and model.
    /// </summary>
    public ChatClientMetadata Metadata { get; }

    /// <summary>
    /// The active execution provider selected by runtime initialization.
    /// Returns configured value before initialization.
    /// </summary>
    public ExecutionProvider ActiveExecutionProvider => _model?.ActiveProvider ?? _options.ExecutionProvider;

    /// <summary>
    /// Details about provider fallback decisions during model initialization, when available.
    /// </summary>
    public string? ProviderSelectionDetails => _model?.ProviderSelectionDetails;

    /// <summary>
    /// Metadata about the loaded model (context window, name, vocab size).
    /// Populated from genai_config.json after model initialization.
    /// Returns null before the model is loaded or if config is unavailable.
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

        await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var tools = options?.Tools;
        var prompt = _formatter.FormatMessages(messageList, tools);
        var genParams = BuildGenerationParameters(options);

        var responseText = await Task.Run(
            () => _model!.Generate(prompt, genParams, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var trimmedResponse = responseText.Trim();

        // Parse for tool calls if tools are available
        var toolCalls = tools is { Count: > 0 } 
            ? _toolCallParser.Parse(trimmedResponse) 
            : [];

        var responseMessage = BuildResponseMessage(trimmedResponse, toolCalls);

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

        await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var tools = options?.Tools;
        var prompt = _formatter.FormatMessages(messageList, tools);
        var genParams = BuildGenerationParameters(options);

        var fullText = new System.Text.StringBuilder();

        await foreach (var token in _model!.GenerateStreamingAsync(prompt, genParams, cancellationToken).ConfigureAwait(false))
        {
            fullText.Append(token);
            yield return new ChatResponseUpdate(ChatRole.Assistant, token)
            {
                ModelId = _options.Model.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        // After streaming completes, check for tool calls
        if (tools is { Count: > 0 })
        {
            var toolCalls = _toolCallParser.Parse(fullText.ToString());
            if (toolCalls.Count > 0)
            {
                // Send function call updates
                foreach (var call in toolCalls)
                {
                    var funcCallContent = new FunctionCallContent(
                        callId: call.CallId,
                        name: call.FunctionName,
                        arguments: call.Arguments);

                    // ChatResponseUpdate requires role + content in constructor
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [funcCallContent])
                    {
                        ModelId = _options.Model.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                }
            }
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(LocalChatClient) || serviceType == typeof(IChatClient))
        {
            return serviceKey is null ? this : null;
        }

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
                _options.GpuDeviceId,
                _options.MaxSequenceLength);
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

    private static ChatMessage BuildResponseMessage(string responseText, IReadOnlyList<ParsedToolCall> toolCalls)
    {
        if (toolCalls.Count == 0)
        {
            return new ChatMessage(ChatRole.Assistant, responseText);
        }

        // Build message with both text and function calls
        var contents = new List<AIContent>();

        // Add text content if there's non-tool-call text
        var textWithoutToolCalls = responseText;
        foreach (var call in toolCalls)
        {
            if (call.RawText != null)
            {
                textWithoutToolCalls = textWithoutToolCalls.Replace(call.RawText, "");
            }
        }

        textWithoutToolCalls = textWithoutToolCalls.Trim();
        if (!string.IsNullOrWhiteSpace(textWithoutToolCalls))
        {
            contents.Add(new TextContent(textWithoutToolCalls));
        }

        // Add function call contents
        foreach (var call in toolCalls)
        {
            var funcCallContent = new FunctionCallContent(
                callId: call.CallId,
                name: call.FunctionName,
                arguments: call.Arguments);
            contents.Add(funcCallContent);
        }

        return new ChatMessage(ChatRole.Assistant, contents);
    }
}
