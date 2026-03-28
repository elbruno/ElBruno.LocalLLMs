using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;

namespace ElBruno.LocalLLMs.Tests.Validation;

/// <summary>
/// Tests for <see cref="OptionsValidator"/> — guards on LocalLLMsOptions values.
/// </summary>
public class OptionsValidatorTests
{
    // ──────────────────────────────────────────────
    // Valid options — no exception
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        var options = new LocalLLMsOptions();

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ValidCustomOptions_DoesNotThrow()
    {
        var options = new LocalLLMsOptions
        {
            MaxSequenceLength = 4096,
            GpuDeviceId = 0,
            Temperature = 0.5f,
        };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────
    // Null options
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => OptionsValidator.Validate(null!));
    }

    // ──────────────────────────────────────────────
    // MaxSequenceLength
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_MaxSequenceLength_Zero_ThrowsArgumentOutOfRange()
    {
        var options = new LocalLLMsOptions { MaxSequenceLength = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_MaxSequenceLength_Negative_ThrowsArgumentOutOfRange()
    {
        var options = new LocalLLMsOptions { MaxSequenceLength = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_MaxSequenceLength_One_IsValid()
    {
        var options = new LocalLLMsOptions { MaxSequenceLength = 1 };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────
    // GpuDeviceId
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_GpuDeviceId_Negative_ThrowsArgumentOutOfRange()
    {
        var options = new LocalLLMsOptions { GpuDeviceId = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_GpuDeviceId_Zero_IsValid()
    {
        var options = new LocalLLMsOptions { GpuDeviceId = 0 };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────
    // Temperature
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_Temperature_Negative_ThrowsArgumentOutOfRange()
    {
        var options = new LocalLLMsOptions { Temperature = -0.1f };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_Temperature_Zero_IsValid()
    {
        var options = new LocalLLMsOptions { Temperature = 0f };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────
    // ModelPath
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_ModelPath_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        var options = new LocalLLMsOptions
        {
            ModelPath = @"C:\this\path\does\not\exist\at\all"
        };

        Assert.Throws<DirectoryNotFoundException>(
            () => OptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_ModelPath_Null_IsValid()
    {
        var options = new LocalLLMsOptions { ModelPath = null };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ModelPath_Empty_IsValid()
    {
        var options = new LocalLLMsOptions { ModelPath = "" };

        var exception = Record.Exception(() => OptionsValidator.Validate(options));

        Assert.Null(exception);
    }
}
