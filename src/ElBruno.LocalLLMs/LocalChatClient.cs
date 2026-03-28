using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.Diagnostics;
using ElBruno.LocalLLMs.Internal;
using ElBruno.LocalLLMs.ToolCalling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger _logger;

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
        : this(options, new ModelDownloader(), loggerFactory: null)
    {
    }

    /// <summary>
    /// Creates a LocalChatClient with the specified options and logger factory.
    /// </summary>
    public LocalChatClient(LocalLLMsOptions options, ILoggerFactory? loggerFactory)
        : this(options, new ModelDownloader(), loggerFactory)
    {
    }

    internal LocalChatClient(LocalLLMsOptions options, IModelDownloader downloader, ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _formatter = ChatTemplateFactory.Create(options.Model.ChatTemplate);
        _toolCallParser = ToolCallParserFactory.Create(options.Model.ChatTemplate);
        _logger = loggerFactory?.CreateLogger<LocalChatClient>() ?? NullLogger<LocalChatClient>.Instance;

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
        return await CreateAsync(new LocalLLMsOptions(), progress: null, loggerFactory: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Async factory with options and progress reporting.
    /// </summary>
    public static async Task<LocalChatClient> CreateAsync(
        LocalLLMsOptions options,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(options, progress, loggerFactory: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Async factory with options, progress reporting, and logger factory.
    /// </summary>
    public static async Task<LocalChatClient> CreateAsync(
        LocalLLMsOptions options,
        IProgress<ModelDownloadProgress>? progress,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken = default)
    {
        OptionsValidator.Validate(options);
        var client = new LocalChatClient(options, loggerFactory);
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

    // --- Environment Diagnostics ---

    /// <summary>
    /// Diagnoses the current environment for local LLM execution capabilities.
    /// </summary>
    public static EnvironmentDiagnostics DiagnoseEnvironment()
    {
        return new EnvironmentDiagnostics
        {
            CpuAvailable = true,
            CudaAvailable = CheckProviderAvailability(ExecutionProvider.Cuda),
            DirectMLAvailable = CheckProviderAvailability(ExecutionProvider.DirectML),
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            OSDescription = RuntimeInformation.OSDescription,
            CacheDirectory = GetDefaultCacheDirectory(),
            CacheSizeBytes = GetCacheSize(GetDefaultCacheDirectory())
        };
    }

    // --- Model Warmup ---

    /// <summary>
    /// Warms up the model by running a short inference pass. Returns elapsed time.
    /// Call after CreateAsync() to pre-warm the inference pipeline and reduce first-query latency.
    /// </summary>
    public async Task<TimeSpan> WarmupAsync(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var warmupMessages = new[] { new ChatMessage(ChatRole.User, "hi") };

        await GetResponseAsync(warmupMessages, new ChatOptions { MaxOutputTokens = 1 }, cancellationToken).ConfigureAwait(false);

        sw.Stop();
        _logger.LogInformation("Model warmup completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        return sw.Elapsed;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        await EnsureInitializedAsync(progress: null, cancellationToken).ConfigureAwait(false);

        LogMessages.InferenceStart(_logger, _options.Model.Id, streaming: false);
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

        LogMessages.InferenceStart(_logger, _options.Model.Id, streaming: true);
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

                LogMessages.ModelDownloadStart(_logger, _options.Model.Id);
                _resolvedModelPath = await _downloader.EnsureModelAsync(
                    _options.Model,
                    _options.CacheDirectory,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                LogMessages.ModelDownloadComplete(_logger, _options.Model.Id, _resolvedModelPath);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            LogMessages.ModelLoadingStart(_logger, _resolvedModelPath, _options.ExecutionProvider);
            _model = new OnnxGenAIModel(
                _resolvedModelPath,
                _options.ExecutionProvider,
                _options.GpuDeviceId,
                _options.MaxSequenceLength,
                _logger);
            sw.Stop();
            LogMessages.ModelLoadingComplete(_logger, _resolvedModelPath, _model.ActiveProvider, sw.Elapsed.TotalMilliseconds);
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

    private static bool CheckProviderAvailability(ExecutionProvider provider)
    {
        try
        {
            return provider switch
            {
                ExecutionProvider.Cuda => OperatingSystem.IsWindows() || OperatingSystem.IsLinux(),
                ExecutionProvider.DirectML => OperatingSystem.IsWindows(),
                _ => true
            };
        }
        catch
        {
            return false;
        }
    }

    private static string GetDefaultCacheDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "elbruno-local-llms");
    }

    private static long GetCacheSize(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
        try
        {
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
