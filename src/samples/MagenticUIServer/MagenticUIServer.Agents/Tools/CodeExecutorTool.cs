using System.ComponentModel;
using System.Diagnostics;
using MagenticUIServer.Agents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagenticUIServer.Agents.Tools;

/// <summary>
/// Executes Python code via WSL2 on Windows, or python3 directly on non-Windows.
/// Phase 3B: real WSL2 bridge (Decision 9 of ADR Phase 3).
/// </summary>
public sealed class CodeExecutorTool
{
    private const int TimeoutSeconds = 30;
    private static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "python", "python3" };

    private readonly ILogger<CodeExecutorTool> _logger;

    public CodeExecutorTool(ILogger<CodeExecutorTool>? logger = null)
    {
        _logger = logger ?? NullLogger<CodeExecutorTool>.Instance;
    }

    [Description("Executes Python code via WSL2. Only 'python'/'python3' is supported.")]
    public async Task<CodeExecutionResult> ExecuteCode(
        [Description("The source code to execute")] string code,
        [Description("The programming language (only 'python' supported)")] string language = "python")
    {
        if (!AllowedLanguages.Contains(language))
        {
            return new CodeExecutionResult(false, "",
                $"Language '{language}' is not supported. Only python/python3 is allowed.");
        }

        // On non-Windows, run python3 directly
        if (!OperatingSystem.IsWindows())
        {
            return await ExecuteProcessAsync("python3", ["-c", code]);
        }

        if (!IsWslAvailable())
        {
            _logger.LogWarning("WSL2 is not available. Code execution requires WSL2 on Windows.");
            return new CodeExecutionResult(false, "",
                "WSL2 is not installed or not available. Install WSL2 to enable code execution.");
        }

        return await ExecuteProcessAsync("wsl", ["--", "python3", "-c", code]);
    }

    private static bool IsWslAvailable()
    {
        try
        {
            var info = new ProcessStartInfo("wsl", "--status")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(info);
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<CodeExecutionResult> ExecuteProcessAsync(
        string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var success = process.ExitCode == 0;
            _logger.LogInformation(
                "Code execution completed. ExitCode={ExitCode}", process.ExitCode);

            return new CodeExecutionResult(
                Success: success,
                Output: stdout,
                Error: string.IsNullOrWhiteSpace(stderr) ? null : stderr);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Code execution timed out after {Seconds}s", TimeoutSeconds);
            return new CodeExecutionResult(false, "",
                $"Execution timed out after {TimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution failed");
            return new CodeExecutionResult(false, "", $"Execution error: {ex.Message}");
        }
    }
}
