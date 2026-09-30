using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// Wraps the exported Laya graph and runs a batch of questions through it in one forward pass.
/// </summary>
/// <remarks>
/// The graph takes <c>input_ids</c>, <c>attention_mask</c>, <c>marker_pos</c>, <c>marker_mask</c>
/// and <c>qtype</c>, and returns raw <c>logits</c> plus <c>act_logits</c>. Rows are independent,
/// so several questions about the same text share a single pass.
/// </remarks>
internal sealed class LayaModel : IDisposable
{
    private readonly InferenceSession _session;
    private readonly int _padTokenId;

    private LayaModel(InferenceSession session, int padTokenId)
    {
        _session = session;
        _padTokenId = padTokenId;
    }

    /// <summary>
    /// Opens an ONNX session over the exported graph.
    /// </summary>
    /// <param name="modelPath">Full path to the <c>.onnx</c> file.</param>
    /// <param name="padTokenId">The token id used to square off a batch.</param>
    /// <param name="intraOpNumThreads">Optional thread count for the CPU execution provider.</param>
    /// <returns>A ready session.</returns>
    public static LayaModel Load(string modelPath, int padTokenId, int? intraOpNumThreads)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        if (intraOpNumThreads is > 0)
        {
            options.IntraOpNumThreads = intraOpNumThreads.Value;
        }

        try
        {
            return new LayaModel(new InferenceSession(modelPath, options), padTokenId);
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or FileNotFoundException)
        {
            options.Dispose();
            throw new DecisionException(
                $"The decision model at '{modelPath}' could not be loaded. " +
                "Check that the download completed and that any companion '.onnx.data' file sits beside it.",
                ex);
        }
    }

    /// <summary>
    /// Runs every sequence in one pass and returns the marker logits for each.
    /// </summary>
    /// <param name="sequences">The tokenized questions.</param>
    /// <returns>One logit array per sequence, trimmed to that sequence's option count.</returns>
    public float[][] Run(IReadOnlyList<LayaSequence> sequences)
    {
        var batch = sequences.Count;
        var length = sequences.Max(sequence => sequence.TokenIds.Count);
        var markers = sequences.Max(sequence => sequence.MarkerPositions.Count);

        var inputIds = new long[batch * length];
        var attentionMask = new long[batch * length];
        var markerPositions = new long[batch * markers];
        var markerMask = new bool[batch * markers];
        var questionTypes = new long[batch];

        // Padded rows must still be valid indices: the head gathers marker positions before it
        // masks them, so an out-of-range position would fault rather than be ignored.
        Array.Fill(inputIds, _padTokenId);

        for (var row = 0; row < batch; row++)
        {
            var sequence = sequences[row];
            var tokenOffset = row * length;
            for (var i = 0; i < sequence.TokenIds.Count; i++)
            {
                inputIds[tokenOffset + i] = sequence.TokenIds[i];
                attentionMask[tokenOffset + i] = 1;
            }

            var markerOffset = row * markers;
            for (var i = 0; i < sequence.MarkerPositions.Count; i++)
            {
                markerPositions[markerOffset + i] = sequence.MarkerPositions[i];
                markerMask[markerOffset + i] = true;
            }

            questionTypes[row] = sequence.QuestionType;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(new Memory<long>(inputIds), new[] { batch, length })),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(new Memory<long>(attentionMask), new[] { batch, length })),
            NamedOnnxValue.CreateFromTensor("marker_pos",
                new DenseTensor<long>(new Memory<long>(markerPositions), new[] { batch, markers })),
            NamedOnnxValue.CreateFromTensor("marker_mask",
                new DenseTensor<bool>(new Memory<bool>(markerMask), new[] { batch, markers })),
            NamedOnnxValue.CreateFromTensor("qtype",
                new DenseTensor<long>(new Memory<long>(questionTypes), new[] { batch })),
        };

        using var outputs = _session.Run(inputs);
        var logits = outputs.First(output => output.Name == "logits").AsTensor<float>();

        var results = new float[batch][];
        for (var row = 0; row < batch; row++)
        {
            var count = sequences[row].MarkerPositions.Count;
            var row_ = new float[count];
            for (var i = 0; i < count; i++)
            {
                row_[i] = logits[row, i];
            }

            results[row] = row_;
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();
}
