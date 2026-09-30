using System.Globalization;
using System.Text.Json;

namespace ElBruno.LocalLLMs.Decisions.Internal;

/// <summary>
/// The checkpoint settings Laya keeps outside the ONNX graph: the token budgets that shape the
/// input sequence, and the fitted temperatures applied to the logits afterwards.
/// </summary>
internal sealed class LayaRuntimeConfig
{
    /// <summary>The lowest temperature that will be applied, matching Laya's <c>TEMP_MIN</c>.</summary>
    public const double MinimumTemperature = 0.5;

    /// <summary>The highest temperature that will be applied, matching Laya's <c>TEMP_MAX</c>.</summary>
    public const double MaximumTemperature = 5.0;

    /// <summary>The per-option token cap Laya applies when rendering options.</summary>
    public const int MaxOptionTokens = 48;

    private readonly double[] _temperatureByType;
    private readonly Dictionary<string, double> _temperatureByOptions;

    private LayaRuntimeConfig(
        int maxLength,
        int headMaxLength,
        double[] temperatureByType,
        Dictionary<string, double> temperatureByOptions,
        bool temperaturesWereClamped)
    {
        MaxLength = maxLength;
        HeadMaxLength = headMaxLength;
        _temperatureByType = temperatureByType;
        _temperatureByOptions = temperatureByOptions;
        TemperaturesWereClamped = temperaturesWereClamped;
    }

    /// <summary>Gets the total token budget for one sequence.</summary>
    public int MaxLength { get; }

    /// <summary>Gets the token budget shared by the instructions and the rendered options.</summary>
    public int HeadMaxLength { get; }

    /// <summary>
    /// Gets a value indicating whether the checkpoint shipped a temperature outside
    /// <see cref="MinimumTemperature"/>..<see cref="MaximumTemperature"/> that had to be clamped.
    /// </summary>
    /// <remarks>
    /// Laya's public English checkpoint ships <c>choice:11+ = 0.1006</c>, which sharpens the
    /// logits roughly tenfold and turns a 24% top probability into a reported 99%. Clamping keeps
    /// the reported confidence honest, at the cost of diverging from the raw checkpoint value.
    /// </remarks>
    public bool TemperaturesWereClamped { get; }

    /// <summary>
    /// Reads <c>rl_agent_config.json</c> from a checkpoint directory.
    /// </summary>
    /// <param name="configPath">Full path to the configuration file.</param>
    /// <returns>The parsed configuration.</returns>
    public static LayaRuntimeConfig Load(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = document.RootElement;

        // The onnx-community exports nest the same values under a "laya" object; the
        // convaiinnovations-derived exports put them at the root.
        var source = root.TryGetProperty("laya", out var nested) ? nested : root;

        var maxLength = source.TryGetProperty("max_len", out var maxLen) ? maxLen.GetInt32() : 512;
        var headMaxLength = source.TryGetProperty("head_max_len", out var headMax) ? headMax.GetInt32() : 192;

        var clamped = false;
        var byType = new double[3];
        Array.Fill(byType, 1.0);

        if (source.TryGetProperty("temperature", out var temperatures)
            && temperatures.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var value in temperatures.EnumerateArray())
            {
                if (index >= byType.Length)
                {
                    break;
                }

                byType[index++] = ClampTemperature(value, ref clamped);
            }
        }

        var byOptions = new Dictionary<string, double>(StringComparer.Ordinal);
        if (source.TryGetProperty("temperature_by_options", out var buckets)
            && buckets.ValueKind == JsonValueKind.Object)
        {
            foreach (var bucket in buckets.EnumerateObject())
            {
                byOptions[bucket.Name] = ClampTemperature(bucket.Value, ref clamped);
            }
        }

        return new LayaRuntimeConfig(maxLength, headMaxLength, byType, byOptions, clamped);
    }

    /// <summary>
    /// Returns the temperature to divide the logits by for a question of this kind and size.
    /// </summary>
    /// <param name="questionType">The question type index: 0 choice, 1 score, 2 noul.</param>
    /// <param name="optionCount">The number of options the caller supplied.</param>
    /// <returns>A temperature within the permitted range.</returns>
    public double TemperatureFor(int questionType, int optionCount)
    {
        var bucket = TemperatureBucket(questionType, optionCount);
        if (_temperatureByOptions.TryGetValue(bucket, out var fitted))
        {
            return fitted;
        }

        return questionType >= 0 && questionType < _temperatureByType.Length
            ? _temperatureByType[questionType]
            : 1.0;
    }

    private static string TemperatureBucket(int questionType, int optionCount)
    {
        var size = optionCount <= 2 ? "2"
            : optionCount <= 5 ? "3-5"
            : optionCount <= 10 ? "6-10"
            : "11+";

        var name = questionType switch
        {
            0 => "choice",
            1 => "score",
            _ => "noul",
        };

        return string.Create(CultureInfo.InvariantCulture, $"{name}:{size}");
    }

    private static double ClampTemperature(JsonElement element, ref bool clamped)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value))
        {
            clamped = true;
            return 1.0;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            clamped = true;
            return 1.0;
        }

        var bounded = Math.Clamp(value, MinimumTemperature, MaximumTemperature);
        if (bounded != value)
        {
            clamped = true;
        }

        return bounded;
    }
}
