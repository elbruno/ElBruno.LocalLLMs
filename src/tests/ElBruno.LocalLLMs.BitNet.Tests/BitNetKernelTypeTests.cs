using ElBruno.LocalLLMs.BitNet;

namespace ElBruno.LocalLLMs.BitNet.Tests;

/// <summary>
/// Tests for <see cref="BitNetKernelType"/> enum.
/// </summary>
public class BitNetKernelTypeTests
{
    [Fact]
    public void I2_S_Exists()
    {
        var value = BitNetKernelType.I2_S;
        Assert.Equal(BitNetKernelType.I2_S, value);
    }

    [Fact]
    public void TL1_Exists()
    {
        var value = BitNetKernelType.TL1;
        Assert.Equal(BitNetKernelType.TL1, value);
    }

    [Fact]
    public void TL2_Exists()
    {
        var value = BitNetKernelType.TL2;
        Assert.Equal(BitNetKernelType.TL2, value);
    }

    [Fact]
    public void Enum_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<BitNetKernelType>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(BitNetKernelType.I2_S)]
    [InlineData(BitNetKernelType.TL1)]
    [InlineData(BitNetKernelType.TL2)]
    public void AllValues_AreDefined(BitNetKernelType kernel)
    {
        Assert.True(Enum.IsDefined(kernel));
    }

    [Fact]
    public void AllValues_HaveDistinctUnderlyingValues()
    {
        var values = Enum.GetValues<BitNetKernelType>().Select(v => (int)v).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}
