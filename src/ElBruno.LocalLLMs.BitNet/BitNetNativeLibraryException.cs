namespace ElBruno.LocalLLMs.BitNet;

/// <summary>
/// Exception thrown when the BitNet native library cannot be loaded.
/// </summary>
public sealed class BitNetNativeLibraryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitNetNativeLibraryException"/> class.
    /// </summary>
    public BitNetNativeLibraryException(string message)
        : base(message)
    {
    }
}
