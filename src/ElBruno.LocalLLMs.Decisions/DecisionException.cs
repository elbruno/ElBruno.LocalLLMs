namespace ElBruno.LocalLLMs.Decisions;

/// <summary>
/// Thrown when a local decision model cannot be reached or returns something unusable.
/// </summary>
public sealed class DecisionException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">The error description.</param>
    public DecisionException(string message) : base(message) { }

    /// <summary>Creates an exception with a message and underlying cause.</summary>
    /// <param name="message">The error description.</param>
    /// <param name="innerException">The underlying failure.</param>
    public DecisionException(string message, Exception innerException) : base(message, innerException) { }
}
