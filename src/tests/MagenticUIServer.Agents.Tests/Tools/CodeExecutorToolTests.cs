using MagenticUIServer.Agents.Tools;

namespace MagenticUIServer.Agents.Tests.Tools;

/// <summary>
/// Phase 3B tests for CodeExecutorTool — real WSL2/python3 bridge.
/// Tests that require WSL or python3 are marked Skip to avoid CI failures.
/// </summary>
public sealed class CodeExecutorToolTests
{
    private readonly CodeExecutorTool _sut = new();

    // ── Language filtering (no process spawned) ──────────────────────────────

    [Theory]
    [InlineData("javascript")]
    [InlineData("csharp")]
    [InlineData("bash")]
    [InlineData("ruby")]
    public async Task ExecuteCode_UnsupportedLanguage_ReturnsFalseWithNotSupportedError(string language)
    {
        // Arrange & Act
        var result = await _sut.ExecuteCode("some code", language);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not supported", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteCode_UnsupportedLanguage_ErrorMentionsOnlyPythonAllowed()
    {
        // Arrange & Act
        var result = await _sut.ExecuteCode("x = 1", "javascript");

        // Assert — error should clearly explain what is allowed
        Assert.NotNull(result.Error);
        Assert.Contains("python", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Allowed language set ─────────────────────────────────────────────────

    [Theory]
    [InlineData("python")]
    [InlineData("python3")]
    [InlineData("Python")]   // case-insensitive
    [InlineData("PYTHON3")]
    public async Task ExecuteCode_AllowedLanguage_IsNotRejectedByLanguageFilter(string language)
    {
        // Arrange & Act — the language filter should pass; any failure is runtime (WSL/python3)
        var result = await _sut.ExecuteCode("x = 1", language);

        // Assert — the error, if any, must NOT say "not supported"
        if (!result.Success && result.Error is not null)
        {
            Assert.DoesNotContain("not supported", result.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteCode_DefaultLanguageIsPython_AcceptedByLanguageFilter()
    {
        // Act — omit the language argument; default is "python"
        var result = await _sut.ExecuteCode("x = 1");

        // Assert — if it fails, it should be runtime (WSL/process), NOT language filtering
        if (!result.Success && result.Error is not null)
        {
            Assert.DoesNotContain("not supported", result.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Edge cases (no process required) ────────────────────────────────────

    [Fact]
    public async Task ExecuteCode_EmptyCodeString_DoesNotThrow_ReturnsResult()
    {
        // Arrange & Act & Assert — empty code must not throw; any result is acceptable
        var ex = await Record.ExceptionAsync(() => _sut.ExecuteCode("", "python"));
        Assert.Null(ex);

        var result = await _sut.ExecuteCode("", "python");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteCode_EmptyCodeUnsupportedLanguage_StillRejectsLanguageFirst()
    {
        // Even with empty code, unsupported language is rejected before any process launch
        var result = await _sut.ExecuteCode("", "java");

        Assert.False(result.Success);
        Assert.Contains("not supported", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Windows / WSL path ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteCode_OnWindowsWithoutWsl_ReturnsFalseWithWsl2InError()
    {
        // This test only asserts on Windows when WSL is NOT available.
        // It validates the WSL-unavailable branch of the implementation.
        if (!OperatingSystem.IsWindows()) return; // non-Windows takes a different path

        var result = await _sut.ExecuteCode("print('hi')", "python");

        // Either WSL succeeded (WSL installed) or we got the WSL2 error message
        if (!result.Success && result.Error is not null)
        {
            // If failure is NOT a WSL2 message, it must be a runtime/execution error,
            // which is also acceptable. We only assert the WSL2 path when the specific message appears.
            // This is a best-effort check — the test always passes if WSL is installed.
            Assert.True(
                result.Error.Contains("WSL2", StringComparison.OrdinalIgnoreCase)
                || result.Error.Contains("Execution", StringComparison.OrdinalIgnoreCase)
                || result.Error.Contains("timed out", StringComparison.OrdinalIgnoreCase),
                $"Unexpected error on Windows: {result.Error}");
        }
    }

    // ── Tests that require actual WSL / python3 (skipped in standard CI) ────

    [Fact(Skip = "Requires WSL2 installed on Windows")]
    public async Task ExecuteCode_WithWsl_SuccessfulPythonPrint_ReturnsOutput()
    {
        var result = await _sut.ExecuteCode("print('hello from wsl')", "python");

        Assert.True(result.Success);
        Assert.Contains("hello from wsl", result.Output);
    }

    [Fact(Skip = "Requires python3 on non-Windows host")]
    public async Task ExecuteCode_OnNonWindows_RunsPython3Directly_ReturnsOutput()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await _sut.ExecuteCode("print('direct python3')", "python");

        Assert.True(result.Success);
        Assert.Contains("direct python3", result.Output);
    }
}
