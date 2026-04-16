namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// BitNet kernel types for ternary weight computation.
/// </summary>
public enum BitNetKernelType
{
    /// <summary>Integer 2-bit signed — universal, works on all platforms.</summary>
    I2_S,
    /// <summary>Table Lookup 1 — optimized for ARM (Apple Silicon, Snapdragon).</summary>
    TL1,
    /// <summary>Table Lookup 2 — optimized for x86 (Intel, AMD).</summary>
    TL2
}
