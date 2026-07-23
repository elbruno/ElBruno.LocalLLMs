using MagenticUIServer.Agents.Models;
using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Agents;

/// <summary>
/// Agent wrapper for CodeExecutorTool.
///
/// Phase 3A stub: immediately returns the stub result from CodeExecutorTool.
/// Phase 3B: will integrate a QEMU/WSL2 sandbox bridge (Decision 9 of ADR Phase 3).
/// </summary>
public sealed class CoderAgentStub
{
    private readonly CodeExecutorTool _tool;
    private const string AgentName = "Coder";

    public CoderAgentStub(CodeExecutorTool tool)
    {
        _tool = tool;
    }

    /// <summary>
    /// Attempts to execute code described in the instruction.
    /// Phase 3A: always returns a stub warning result.
    /// </summary>
    public async Task<CodeExecutionResult> ExecuteAsync(
        string code,
        string language,
        IProgress<AgentMessage> progress,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Report(progress, "tool",
            $"[Phase 3A stub] Code execution requested for language '{language}'.", round: 0);

        var result = await _tool.ExecuteCode(code, language);

        Report(progress, "tool", result.Output, round: 0);
        return result;
    }

    private static void Report(IProgress<AgentMessage> progress, string role, string text, int round) =>
        progress.Report(new AgentMessage(AgentName, role, text, round, DateTimeOffset.UtcNow));
}
