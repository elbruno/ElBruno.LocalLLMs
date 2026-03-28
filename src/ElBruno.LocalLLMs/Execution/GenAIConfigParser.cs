using System.Text.Json;

namespace ElBruno.LocalLLMs.Internal;

/// <summary>
/// Parses genai_config.json from an ONNX GenAI model directory to extract model metadata.
/// </summary>
internal static class GenAIConfigParser
{
    private const string ConfigFileName = "genai_config.json";

    /// <summary>
    /// Attempts to parse model metadata from the genai_config.json in the given model directory.
    /// Returns null if the file does not exist or cannot be parsed.
    /// </summary>
    internal static ModelMetadata? TryParse(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return null;

        var configPath = Path.Combine(modelPath, ConfigFileName);
        if (!File.Exists(configPath))
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var maxSequenceLength = ResolveMaxSequenceLength(root);
            var modelName = ResolveModelName(root, modelPath);
            var vocabSize = ResolveVocabSize(root);

            return new ModelMetadata
            {
                MaxSequenceLength = maxSequenceLength,
                ModelName = modelName,
                VocabSize = vocabSize
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves max sequence length from search.max_length, model.context_length, or model.max_length.
    /// Falls back to 0 if none found.
    /// </summary>
    private static int ResolveMaxSequenceLength(JsonElement root)
    {
        // search.max_length — used by ONNX Runtime GenAI as the generation limit
        if (root.TryGetProperty("search", out var search) &&
            search.TryGetProperty("max_length", out var searchMaxLen) &&
            searchMaxLen.TryGetInt32(out var sml))
        {
            return sml;
        }

        // model.context_length — common in newer configs
        if (root.TryGetProperty("model", out var model))
        {
            if (model.TryGetProperty("context_length", out var ctxLen) &&
                ctxLen.TryGetInt32(out var cl))
            {
                return cl;
            }

            if (model.TryGetProperty("max_length", out var modelMaxLen) &&
                modelMaxLen.TryGetInt32(out var mml))
            {
                return mml;
            }
        }

        return 0;
    }

    /// <summary>
    /// Resolves model name from model.type in config, falling back to the directory name.
    /// </summary>
    private static string? ResolveModelName(JsonElement root, string modelPath)
    {
        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("type", out var modelType) &&
            modelType.ValueKind == JsonValueKind.String)
        {
            var name = modelType.GetString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        // Fall back to directory name
        var dirName = Path.GetFileName(modelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(dirName) ? null : dirName;
    }

    /// <summary>
    /// Resolves vocabulary size from model.vocab_size.
    /// </summary>
    private static int? ResolveVocabSize(JsonElement root)
    {
        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("vocab_size", out var vocabSize) &&
            vocabSize.TryGetInt32(out var vs))
        {
            return vs;
        }

        return null;
    }
}
