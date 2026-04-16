using System.Runtime.InteropServices;
using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Native;

/// <summary>
/// Applies sampling strategies over logits to select the next token.
/// </summary>
internal static class LlamaSampler
{
    internal static int SampleToken(
        IntPtr logitsPtr,
        int vocabSize,
        IReadOnlyList<int> recentTokens,
        float temperature,
        float topP,
        int topK,
        float repetitionPenalty)
    {
        if (logitsPtr == IntPtr.Zero)
        {
            throw new BitNetInferenceException("Failed to retrieve logits from BitNet runtime.");
        }

        var logits = new float[vocabSize];
        Marshal.Copy(logitsPtr, logits, 0, vocabSize);

        if (repetitionPenalty > 1.0f && recentTokens.Count > 0)
        {
            var uniqueTokens = new HashSet<int>(recentTokens);
            foreach (var token in uniqueTokens)
            {
                if ((uint)token >= (uint)vocabSize)
                {
                    continue;
                }

                var value = logits[token];
                logits[token] = value < 0 ? value * repetitionPenalty : value / repetitionPenalty;
            }
        }

        if (temperature <= 0f)
        {
            return ArgMax(logits);
        }

        var scaled = new List<(int Token, float Logit)>(vocabSize);
        for (var i = 0; i < vocabSize; i++)
        {
            scaled.Add((i, logits[i] / temperature));
        }

        scaled.Sort((a, b) => b.Logit.CompareTo(a.Logit));

        if (topK > 0 && topK < scaled.Count)
        {
            scaled = scaled.GetRange(0, topK);
        }

        topP = Math.Clamp(topP, 0f, 1f);

        var maxLogit = scaled[0].Logit;
        var expValues = new float[scaled.Count];
        var total = 0f;

        for (var i = 0; i < scaled.Count; i++)
        {
            var exp = MathF.Exp(scaled[i].Logit - maxLogit);
            expValues[i] = exp;
            total += exp;
        }

        var candidates = new List<(int Token, float Prob)>(scaled.Count);
        var cumulative = 0f;

        for (var i = 0; i < scaled.Count; i++)
        {
            var prob = expValues[i] / total;
            candidates.Add((scaled[i].Token, prob));
            cumulative += prob;

            if (topP > 0f && cumulative >= topP)
            {
                break;
            }
        }

        var sampleTarget = (float)Random.Shared.NextDouble();
        var running = 0f;
        foreach (var candidate in candidates)
        {
            running += candidate.Prob;
            if (sampleTarget <= running)
            {
                return candidate.Token;
            }
        }

        return candidates.Count > 0 ? candidates[^1].Token : ArgMax(logits);
    }

    private static int ArgMax(float[] values)
    {
        var maxIndex = 0;
        var maxValue = values[0];

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > maxValue)
            {
                maxValue = values[i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }
}
