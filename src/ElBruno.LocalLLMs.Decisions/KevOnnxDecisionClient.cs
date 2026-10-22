using System.Text.Json;
using ElBruno.LocalLLMs.Decisions.Internal;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Runs the pinned Kev 0.8B ONNX export in-process, using its trained pointer head.
/// </summary>
/// <remarks>
/// Acquire and convert the checkpoint with <c>scripts/convert_kev.py</c> before use.
/// This client never downloads models, starts a server, generates text, or requires Python at runtime.
/// Calls are serialized, and the native session is loaded lazily.
/// </remarks>
public sealed class KevOnnxDecisionClient : IDecisionClient, IDisposable
{
    private readonly string _modelPath;
    private readonly int _threads;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private KevTokenizer? _tokenizer;
    private int _disposed;

    /// <summary>Creates a client over a local export directory, without loading the model.</summary>
    /// <param name="modelPath">Directory produced by <c>convert_kev.py</c>.</param>
    /// <param name="intraOpNumThreads">CPU threads within each native operator.</param>
    public KevOnnxDecisionClient(string modelPath, int intraOpNumThreads = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intraOpNumThreads);
        _modelPath = Path.GetFullPath(modelPath);
        _threads = intraOpNumThreads;
    }

    /// <inheritdoc />
    public async Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        DecisionRequestValidation.Choice(input, instructions, options);
        var entries = options.ToArray();
        var probabilities = await EvaluateAsync(input, instructions,
            entries.Select(entry => string.IsNullOrEmpty(entry.Value) ? entry.Key : $"{entry.Key}: {entry.Value}").ToArray(),
            cancellationToken).ConfigureAwait(false);
        var best = Array.IndexOf(probabilities, probabilities.Max());
        var distribution = entries.Select((entry, index) => new KeyValuePair<string, double>(entry.Key, probabilities[index]))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var confidence = (probabilities[best] - 1.0 / probabilities.Length) / (1 - 1.0 / probabilities.Length);
        return new ChoiceDecision(entries[best].Key, distribution, Math.Clamp(confidence, 0, 1));
    }

    /// <inheritdoc />
    public async Task<ScoreDecision> ScoreAsync(string input, string instructions,
        IReadOnlyList<string> rubric, CancellationToken cancellationToken = default)
    {
        DecisionRequestValidation.Score(input, instructions, rubric);
        return new ScoreDecision(await EvaluateAsync(input, instructions, rubric.ToArray(), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken = default)
    {
        DecisionRequestValidation.Assess(input, proposition);
        var probabilities = await EvaluateAsync(input, proposition, ["no", "yes"], cancellationToken).ConfigureAwait(false);
        return new AssessmentDecision(probabilities[1]);
    }

    private async Task<double[]> EvaluateAsync(string state, string instructions, string[] options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (options.Length > 255)
            throw new ArgumentException("Kev supports at most 255 options.", nameof(options));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureLoaded();
                cancellationToken.ThrowIfCancellationRequested();
                return Run(_tokenizer!.Build(state, instructions, options), cancellationToken);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureLoaded()
    {
        if (_session is not null) return;
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_modelPath, "kev_config.json")));
            var config = manifest.RootElement;
            if (config.GetProperty("format").GetString() != "kev-0.8b-prefill-v1" ||
                config.GetProperty("checkpoint_revision").GetString() != "bf75a6a8848ea6960ff2ed108d9ed44c2941174f" ||
                config.GetProperty("base_revision").GetString() != "dc7cdfe2ee4154fa7e30f5b51ca41bfa40174e68" ||
                config.GetProperty("max_tokens").GetInt32() != 8192)
                throw new DecisionException("Unsupported Kev export. Run scripts/convert_kev.py for the pinned 0.8B checkpoint.");
            var tokenizer = KevTokenizer.Load(Path.Combine(_modelPath, "tokenizer.json"));
            using var sessionOptions = new SessionOptions { IntraOpNumThreads = _threads };
            var session = new InferenceSession(Path.Combine(_modelPath, "decision.onnx"), sessionOptions);
            try
            {
                string[] required = ["input_ids", "attention_mask", "position_ids", "option_positions", "decision_position"];
                if (!required.Order().SequenceEqual(session.InputMetadata.Keys.Order()) ||
                    !session.OutputMetadata.ContainsKey("decision_logits"))
                    throw new DecisionException("The Kev graph has an unsupported tensor contract. Re-export with convert_kev.py.");
                _tokenizer = tokenizer;
                _session = session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException or OnnxRuntimeException)
        {
            throw new DecisionException(
                $"The Kev export at '{_modelPath}' could not be loaded. Run scripts/convert_kev.py and keep decision.onnx, " +
                "its external weights, tokenizer.json and kev_config.json together. ONNX Runtime 1.31.0 or later is required.", ex);
        }
    }

    private double[] Run(KevSequence sequence, CancellationToken cancellationToken)
    {
        var length = sequence.TokenIds.Length;
        var positions = Enumerable.Range(0, 3).SelectMany(_ => Enumerable.Range(0, length).Select(index => (long)index)).ToArray();
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(sequence.TokenIds, [1, length])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, length).ToArray(), [1, length])),
            NamedOnnxValue.CreateFromTensor("position_ids", new DenseTensor<long>(positions, [3, 1, length])),
            NamedOnnxValue.CreateFromTensor("option_positions", new DenseTensor<long>(sequence.OptionPositions, [sequence.OptionPositions.Length])),
            NamedOnnxValue.CreateFromTensor("decision_position", new DenseTensor<long>(new[] { (long)length - 1 }, Array.Empty<int>())),
        };
        using var runOptions = new RunOptions();
        using var registration = cancellationToken.Register(() => runOptions.Terminate = true);
        try
        {
            using var outputs = _session!.Run(inputs, ["decision_logits"], runOptions);
            cancellationToken.ThrowIfCancellationRequested();
            var logits = outputs.Single().AsTensor<float>();
            if (logits.Rank != 1 || logits.Length != sequence.OptionPositions.Length || logits.Any(value => !float.IsFinite(value)))
                throw new DecisionException("Kev returned incomplete or non-finite option logits; no probabilities were produced.");
            return DecisionMath.Softmax(logits.ToArray(), 1);
        }
        catch (OnnxRuntimeException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Kev inference was cancelled.", ex, cancellationToken);
        }
        catch (OnnxRuntimeException ex)
        {
            throw new DecisionException("Kev native inference failed. Verify the export and ONNX Runtime version.", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _gate.Wait();
        try { _session?.Dispose(); }
        finally { _gate.Release(); }
    }
}
