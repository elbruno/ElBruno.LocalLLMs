namespace ElBruno.LocalLLMs;

/// <summary>
/// Model size tier for documentation/filtering.
/// </summary>
public enum ModelTier
{
    /// <summary>≤2B params — edge, IoT, fast prototyping.</summary>
    Tiny,

    /// <summary>3-4B params — best quality/size ratio, recommended starting point.</summary>
    Small,

    /// <summary>7-24B params — production quality local inference.</summary>
    Medium,

    /// <summary>32B+ params — heavy workloads, multi-GPU.</summary>
    Large
}
