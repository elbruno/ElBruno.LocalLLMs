using System.Security.Cryptography;
using System.Text;
using ElBruno.HuggingFace;

namespace ElBruno.LocalLLMs.Decisions;

/// <summary>Configures first-use acquisition and native CPU execution of Kev 0.8B.</summary>
public sealed class KevDecisionOptions
{
    /// <summary>Gets or sets the repository containing a preconverted Kev export.</summary>
    public string ModelRepository { get; set; } = "elbruno/kev-0.8b-onnx";

    /// <summary>Gets or sets the immutable 40-character commit of the published ONNX export, not the original checkpoint.</summary>
    public string ModelRevision { get; set; } = "deec2b78a30db0c4d00ee36386e87be5382dafd7";

    /// <summary>Gets or sets an explicit local export directory. When set, no download or cache repair occurs.</summary>
    public string? ModelPath { get; set; }

    /// <summary>Gets or sets the cache root, defaulting to local application data under ElBruno/LocalLLMs/decisions.</summary>
    public string? CacheDirectory { get; set; }

    /// <summary>Gets or sets CPU intra-operator threads. Defaults to four.</summary>
    public int IntraOpNumThreads { get; set; } = 4;

    /// <summary>Gets or sets optional progress for the first-use model transfer.</summary>
    public IProgress<DownloadProgress>? DownloadProgress { get; set; }

    internal KevDecisionOptions Snapshot() => new()
    {
        ModelRepository = ModelRepository,
        ModelRevision = ModelRevision,
        ModelPath = ModelPath,
        CacheDirectory = CacheDirectory,
        IntraOpNumThreads = IntraOpNumThreads,
        DownloadProgress = DownloadProgress,
    };

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(IntraOpNumThreads);
        if (ModelPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ModelPath);
            return;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelRepository);
        var segments = ModelRepository.Split('/');
        if (segments.Length != 2 || segments.Any(segment => segment.Length == 0 ||
            segment is "." or ".." || segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
            throw new ArgumentException("Kev ModelRepository must have the form 'owner/model'.", nameof(ModelRepository));
        if (ModelRevision is null || ModelRevision.Length != 40 || ModelRevision.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Kev ModelRevision must be an immutable 40-character Hugging Face commit SHA.", nameof(ModelRevision));
        if (CacheDirectory is not null) ArgumentException.ThrowIfNullOrWhiteSpace(CacheDirectory);
    }

    internal string ResolveModelDirectory()
    {
        if (ModelPath is not null) return Path.GetFullPath(ModelPath);
        var root = CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElBruno", "LocalLLMs", "decisions");
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ModelRepository + "@" + ModelRevision))).ToLowerInvariant();
        return Path.Combine(Path.GetFullPath(root), "kev-" + identity);
    }
}
