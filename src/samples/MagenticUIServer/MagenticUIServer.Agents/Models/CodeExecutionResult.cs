namespace MagenticUIServer.Agents.Models;

/// <summary>
/// Result from a code execution attempt.
/// Phase 3A: always returns a stub result; Phase 3B bridges via WSL2/QEMU.
/// </summary>
public sealed record CodeExecutionResult(
    bool Success,
    string Output,
    string? Error = null);
