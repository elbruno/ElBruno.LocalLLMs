using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ElBruno.LocalLLMs.BitNet.Native;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Local BitNet chat client using bitnet.cpp (llama.cpp fork).
/// Implements IChatClient for seamless MEAI integration.
/// </summary>
public sealed class BitNetChatClient : IChatClient, IAsyncDisposable
{
    private readonly BitNetOptions _options;
    private readonly IChatTemplateFormatter _formatter;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    private IntPtr _model;
    private IntPtr _context;
    private IntPtr _vocab;
    private int _vocabSize;
    private int _eosToken;
    private bool _disposed;

    /// <summary>
    /// Creates a BitNetChatClient with the specified options.
    /// Loads the native library and model immediately.
    /// </summary>
    public BitNetChatClient(BitNetOptions options)
        : this(options, loggerFactory: null)
    {
    }

    /// <summary>
    /// Creates a BitNetChatClient with options and a logger factory.
    /// Loads the native library and model immediately.
    /// </summary>
    public BitNetChatClient(BitNetOptions options, ILoggerFactory? loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _formatter = ChatTemplateFactory.Create(options.ChatTemplateOverride ?? options.Model.ChatTemplate);
        _logger = loggerFactory?.CreateLogger<BitNetChatClient>() ?? NullLogger<BitNetChatClient>.Instance;

        Metadata = new ChatClientMetadata(
            providerName: "elbruno-local-llms-bitnet",
            providerUri: new Uri("https://github.com/elbruno/ElBruno.LocalLLMs"),
            defaultModelId: options.Model.Id);

        EnsureInitializedAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async factory for DI/hosted scenarios.
    /// </summary>
    public static Task<BitNetChatClient> CreateAsync(
        BitNetOptions options,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => new BitNetChatClient(options, loggerFactory), cancellationToken);
    }

    /// <summary>
    /// Async factory that auto-downloads the GGUF model from HuggingFace before creating the client.
    /// Uses the default model (<see cref="BitNetKnownModels.BitNet2B4T"/>) unless overridden in options.
    /// </summary>
    public static async Task<BitNetChatClient> CreateAsync(
        BitNetOptions options,
        IProgress<ModelDownloadProgress>? progress,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ModelPath) && options.EnsureModelDownloaded)
        {
            var downloader = new BitNetModelDownloader();
            options.ModelPath = await downloader.EnsureModelAsync(
                options.Model,
                options.CacheDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);
        }

        return await Task.Run(() => new BitNetChatClient(options, loggerFactory), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Metadata describing this chat client provider and model.
    /// </summary>
    public ChatClientMetadata Metadata { get; }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _inferenceLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var responseText = await Task.Run(
                () => GenerateResponse(messages, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                ModelId = _options.Model.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            _inferenceLock.Release();
        }
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
        await _inferenceLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var update in GenerateStreamingResponse(messages, options, cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(BitNetChatClient) || serviceType == typeof(IChatClient))
        {
            return serviceKey is null ? this : null;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeCore();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeCore();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void DisposeCore()
    {
        if (_context != IntPtr.Zero)
        {
            LlamaNative.llama_free(_context);
            _context = IntPtr.Zero;
        }

        if (_model != IntPtr.Zero)
        {
            LlamaNative.llama_model_free(_model);
            _model = IntPtr.Zero;
        }

        LlamaNative.llama_backend_free();

        _initLock.Dispose();
        _inferenceLock.Dispose();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_context != IntPtr.Zero)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_context != IntPtr.Zero)
            {
                return;
            }

            NativeLibraryLoader.EnsureLoaded(_options.NativeLibraryPath);
            LlamaNative.llama_backend_init();

            var modelPath = _options.ModelPath;

            // Auto-download if ModelPath is not set and EnsureModelDownloaded is enabled
            if (string.IsNullOrWhiteSpace(modelPath) && _options.EnsureModelDownloaded)
            {
                _logger.LogInformation(
                    "ModelPath not set — downloading '{ModelId}' from HuggingFace...",
                    _options.Model.Id);

                var downloader = new BitNetModelDownloader();
                modelPath = await downloader.EnsureModelAsync(
                    _options.Model,
                    _options.CacheDirectory,
                    progress: null,
                    cancellationToken).ConfigureAwait(false);

                _options.ModelPath = modelPath;

                _logger.LogInformation("Model downloaded to '{ModelPath}'.", modelPath);
            }

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new BitNetInferenceException(
                    "BitNetOptions.ModelPath must be set to a GGUF model file, or set EnsureModelDownloaded = true to download automatically.");
            }

            var modelParams = LlamaNative.llama_model_default_params();
            modelParams.n_gpu_layers = 0;

            _model = LlamaNative.llama_model_load_from_file(modelPath, modelParams);
            if (_model == IntPtr.Zero)
            {
                throw new BitNetInferenceException($"Failed to load BitNet model from '{modelPath}'.");
            }

            var contextParams = LlamaNative.llama_context_default_params();
            contextParams.n_ctx = (uint)Math.Max(1, _options.ContextSize);
            contextParams.n_batch = (uint)Math.Min(_options.ContextSize, 512);
            contextParams.n_ubatch = contextParams.n_batch;
            contextParams.n_seq_max = 1;
            contextParams.n_threads = _options.ThreadCount;
            contextParams.n_threads_batch = _options.ThreadCount;

            _context = LlamaNative.llama_new_context_with_model(_model, contextParams);
            if (_context == IntPtr.Zero)
            {
                LlamaNative.llama_model_free(_model);
                _model = IntPtr.Zero;
                throw new BitNetInferenceException("Failed to initialize BitNet context.");
            }

            _vocab = LlamaNative.llama_model_get_vocab(_model);
            if (_vocab == IntPtr.Zero)
            {
                throw new BitNetInferenceException("Failed to resolve BitNet vocabulary.");
            }

            _vocabSize = LlamaNative.llama_n_vocab(_vocab);
            _eosToken = LlamaNative.llama_token_eos(_vocab);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string GenerateResponse(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(options);
        var prompt = BuildPrompt(messages, options?.Tools);
        var promptTokens = TokenizePrompt(prompt);

        ValidateTokenBudget(promptTokens.Length, settings.MaxTokens);
        LlamaNative.llama_kv_cache_clear(_context);

        DecodeTokens(promptTokens, 0, requestLogits: true);

        var outputBuilder = new StringBuilder();
        var allTokens = new List<int>(promptTokens);
        var position = promptTokens.Length;

        for (var i = 0; i < settings.MaxTokens; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var logits = LlamaNative.llama_get_logits_ith(_context, -1);
            var token = LlamaSampler.SampleToken(
                logits,
                _vocabSize,
                allTokens,
                settings.Temperature,
                settings.TopP,
                settings.TopK,
                settings.RepetitionPenalty);

            if (token == _eosToken)
            {
                break;
            }

            allTokens.Add(token);
            var tokenText = DecodeToken(token);
            outputBuilder.Append(tokenText);

            DecodeTokens([token], position, requestLogits: true);
            position++;
        }

        return outputBuilder.ToString().Trim();
    }

    private IEnumerable<ChatResponseUpdate> GenerateStreamingResponse(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(options);
        var prompt = BuildPrompt(messages, options?.Tools);
        var promptTokens = TokenizePrompt(prompt);

        ValidateTokenBudget(promptTokens.Length, settings.MaxTokens);
        LlamaNative.llama_kv_cache_clear(_context);

        DecodeTokens(promptTokens, 0, requestLogits: true);

        var allTokens = new List<int>(promptTokens);
        var position = promptTokens.Length;

        for (var i = 0; i < settings.MaxTokens; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var logits = LlamaNative.llama_get_logits_ith(_context, -1);
            var token = LlamaSampler.SampleToken(
                logits,
                _vocabSize,
                allTokens,
                settings.Temperature,
                settings.TopP,
                settings.TopK,
                settings.RepetitionPenalty);

            if (token == _eosToken)
            {
                break;
            }

            allTokens.Add(token);
            var tokenText = DecodeToken(token);

            DecodeTokens([token], position, requestLogits: true);
            position++;

            yield return new ChatResponseUpdate(ChatRole.Assistant, tokenText)
            {
                ModelId = _options.Model.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private string BuildPrompt(IEnumerable<ChatMessage> messages, IEnumerable<AITool>? tools)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();

        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            var withSystem = new List<ChatMessage>(messageList.Count + 1)
            {
                new(ChatRole.System, _options.SystemPrompt)
            };
            withSystem.AddRange(messageList);
            messageList = withSystem;
        }

        return _formatter.FormatMessages(messageList, tools);
    }

    private int[] TokenizePrompt(string prompt)
    {
        var byteCount = Encoding.UTF8.GetByteCount(prompt);
        var maxTokens = Math.Max(16, byteCount + 8);
        maxTokens = Math.Min(maxTokens, _options.ContextSize);

        var tokens = new int[maxTokens];
        var count = LlamaNative.llama_tokenize(
            _vocab,
            prompt,
            byteCount,
            tokens,
            maxTokens,
            add_special: true,
            parse_special: true);

        if (count < 0)
        {
            var needed = Math.Min(_options.ContextSize, -count);
            tokens = new int[needed];
            count = LlamaNative.llama_tokenize(
                _vocab,
                prompt,
                byteCount,
                tokens,
                needed,
                add_special: true,
                parse_special: true);
        }

        if (count < 0)
        {
            throw new BitNetInferenceException("BitNet tokenization failed.");
        }

        Array.Resize(ref tokens, count);
        return tokens;
    }

    private void DecodeTokens(int[] tokens, int startPosition, bool requestLogits)
    {
        var batch = LlamaNative.llama_batch_init(tokens.Length, 0, 1);
        try
        {
            batch.n_tokens = tokens.Length;
            for (var i = 0; i < tokens.Length; i++)
            {
                Marshal.WriteInt32(batch.token, i * sizeof(int), tokens[i]);
                Marshal.WriteInt32(batch.pos, i * sizeof(int), startPosition + i);
                Marshal.WriteInt32(batch.n_seq_id, i * sizeof(int), 1);
                if (batch.seq_id != IntPtr.Zero)
                {
                    var seqIdPtr = Marshal.ReadIntPtr(batch.seq_id, i * IntPtr.Size);
                    Marshal.WriteInt32(seqIdPtr, 0, 0);
                }
                if (batch.logits != IntPtr.Zero)
                {
                    Marshal.WriteByte(batch.logits, i, (byte)(requestLogits && i == tokens.Length - 1 ? 1 : 0));
                }
            }

            var status = LlamaNative.llama_decode(_context, batch);
            if (status < 0)
            {
                throw new BitNetInferenceException($"BitNet decode failed with status {status}.");
            }
        }
        finally
        {
            LlamaNative.llama_batch_free(batch);
        }
    }

    private string DecodeToken(int token)
    {
        var buffer = new byte[256];
        var length = LlamaNative.llama_token_to_piece(_vocab, token, buffer, buffer.Length, 0, special: false);

        if (length < 0)
        {
            buffer = new byte[-length];
            length = LlamaNative.llama_token_to_piece(_vocab, token, buffer, buffer.Length, 0, special: false);
        }

        return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : string.Empty;
    }

    private void ValidateTokenBudget(int promptTokens, int maxTokens)
    {
        if (promptTokens + maxTokens > _options.ContextSize)
        {
            throw new BitNetInferenceException(
                $"Prompt tokens ({promptTokens}) plus max output tokens ({maxTokens}) exceed context size {_options.ContextSize}.");
        }
    }

    private GenerationSettings ResolveSettings(ChatOptions? options)
    {
        var maxTokens = options?.MaxOutputTokens ?? _options.MaxTokens;
        var temperature = options?.Temperature ?? _options.Temperature;
        var topP = options?.TopP ?? _options.TopP;
        var topK = options?.TopK ?? _options.TopK;
        var repetitionPenalty = options?.FrequencyPenalty ?? _options.RepetitionPenalty;

        return new GenerationSettings(maxTokens, temperature, topP, topK, repetitionPenalty);
    }

    private sealed record GenerationSettings(
        int MaxTokens,
        float Temperature,
        float TopP,
        int TopK,
        float RepetitionPenalty);
}
