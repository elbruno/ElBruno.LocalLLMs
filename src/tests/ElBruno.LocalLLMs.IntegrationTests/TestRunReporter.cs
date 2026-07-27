using System.Collections.Concurrent;
using System.Diagnostics;
using ElBruno.LocalLLMs;

namespace ElBruno.LocalLLMs.IntegrationTests;

/// <summary>
/// Represents the outcome of a single model's lifecycle test phases.
/// </summary>
public sealed record ModelTestResult(
    string ModelId,
    string DisplayName,
    string Group,
    ResultStatus PhaseA,
    ResultStatus PhaseB,
    ResultStatus PhaseC,
    TimeSpan? PhaseADuration,
    TimeSpan? PhaseBDuration,
    TimeSpan? PhaseCDuration,
    string? ErrorMessage);

/// <summary>
/// Result status for a single test phase.
/// </summary>
public enum ResultStatus { Pass, Fail, Skip, NotApplicable }

/// <summary>
/// xUnit collection definition — all integration lifecycle test classes share this.
/// </summary>
[CollectionDefinition("IntegrationTestRun")]
public class IntegrationTestRunCollection : ICollectionFixture<TestRunReporter>;

/// <summary>
/// Shared xUnit fixture that collects per-model test results and writes a markdown report
/// to <c>docs/tests/YYYY-MM-DD-HH-run-results.md</c> (UTC hour granularity).
/// If a file for the same hour already exists it is overwritten — only the latest run per hour is kept.
/// </summary>
public sealed class TestRunReporter : IDisposable
{
    private readonly ConcurrentDictionary<string, ModelTestResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _runTimer = Stopwatch.StartNew();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private bool _disposed;

    /// <summary>Records or replaces the result for a model.</summary>
    public void RecordResult(ModelTestResult result)
        => _results[result.ModelId] = result;

    /// <summary>Records a simple skipped result for a model (used when integration tests are not enabled).</summary>
    public void RecordSkipped(ModelDefinition model, string group)
        => _results[model.Id] = new ModelTestResult(
            model.Id, model.DisplayName, group,
            ResultStatus.Skip, ResultStatus.Skip, ResultStatus.Skip,
            null, null, null, "RUN_INTEGRATION_TESTS not set");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _runTimer.Stop();
        WriteReport();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Report writing
    // ──────────────────────────────────────────────────────────────────────────

    private void WriteReport()
    {
        try
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot is null) return;

            var docsTestsDir = Path.Combine(repoRoot, "docs", "tests");
            Directory.CreateDirectory(docsTestsDir);

            var hourKey = _startTime.ToString("yyyy-MM-dd-HH");
            var filePath = Path.Combine(docsTestsDir, $"{hourKey}-run-results.md");

            var markdown = BuildMarkdown();
            File.WriteAllText(filePath, markdown);
        }
        catch
        {
            // Best-effort — never fail tests because of reporting.
        }
    }

    private string BuildMarkdown()
    {
        var totalModels = KnownModels.All.Count;
        var recorded = _results.Values.ToList();
        var passed = recorded.Count(r => r.PhaseA == ResultStatus.Pass);
        var failed = recorded.Count(r => r.PhaseA == ResultStatus.Fail || r.PhaseB == ResultStatus.Fail || r.PhaseC == ResultStatus.Fail);
        var skipped = recorded.Count(r => r.PhaseA == ResultStatus.Skip);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Integration Test Run Results");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {_startTime:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine($"**Duration:** {FormatDuration(_runTimer.Elapsed)}  ");
        sb.AppendLine($"**Models total:** {totalModels}  ");
        sb.AppendLine($"**Result:** {passed} passed, {skipped} skipped, {failed} failed");
        sb.AppendLine();

        // Group by model group
        var groups = new[] { "Text (GenAI)", "Tool-Calling", "Vision", "Non-Native ONNX" };
        foreach (var group in groups)
        {
            var groupResults = recorded
                .Where(r => r.Group == group)
                .OrderBy(r => r.ModelId)
                .ToList();

            if (groupResults.Count == 0) continue;

            sb.AppendLine($"## {group} Models");
            sb.AppendLine();
            sb.AppendLine("| Model | Phase A (download) | Phase B (cache hit) | Phase C (delete) | Notes |");
            sb.AppendLine("|-------|-------------------|---------------------|------------------|-------|");

            foreach (var r in groupResults)
            {
                var a = FormatPhase(r.PhaseA, r.PhaseADuration);
                var b = FormatPhase(r.PhaseB, r.PhaseBDuration);
                var c = FormatPhase(r.PhaseC, r.PhaseCDuration);
                var notes = r.ErrorMessage is not null
                    ? EscapeMarkdown(r.ErrorMessage)
                    : "";
                sb.AppendLine($"| `{r.ModelId}` | {a} | {b} | {c} | {notes} |");
            }

            sb.AppendLine();
        }

        // Models with no recorded result (test didn't run at all)
        var recordedIds = new HashSet<string>(_results.Keys, StringComparer.OrdinalIgnoreCase);
        var unrecorded = KnownModels.All.Where(m => !recordedIds.Contains(m.Id)).ToList();
        if (unrecorded.Count > 0)
        {
            sb.AppendLine("## Models Not Run");
            sb.AppendLine();
            sb.AppendLine("| Model | Reason |");
            sb.AppendLine("|-------|--------|");
            foreach (var m in unrecorded)
                sb.AppendLine($"| `{m.Id}` | Not executed in this run |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatPhase(ResultStatus status, TimeSpan? duration)
    {
        var icon = status switch
        {
            ResultStatus.Pass => "PASS",
            ResultStatus.Fail => "FAIL",
            ResultStatus.Skip => "SKIP",
            ResultStatus.NotApplicable => "N/A",
            _ => "?"
        };
        var time = duration.HasValue ? $" ({FormatDuration(duration.Value)})" : "";
        return $"{icon}{time}";
    }

    private static string FormatDuration(TimeSpan ts)
        => ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s"
            : ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                : $"{ts.TotalSeconds:F1}s";

    private static string EscapeMarkdown(string text)
        => text.Replace("|", "\\|").Replace("\r\n", " ").Replace("\n", " ");

    private static string? FindRepoRoot()
    {
        // Walk up from the test binary to find the directory containing ElBruno.LocalLLMs.slnx
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
