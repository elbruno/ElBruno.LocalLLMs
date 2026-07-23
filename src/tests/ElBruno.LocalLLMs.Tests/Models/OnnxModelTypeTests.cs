namespace ElBruno.LocalLLMs.Tests.Models;

/// <summary>
/// Tests that <see cref="OnnxModelType.VisionGenAI"/> exists and is distinct
/// from existing enum values.
/// </summary>
[Trait("Category", "Fara")]
public class OnnxModelTypeTests
{
    [Fact]
    public void VisionGenAI_EnumValueExists()
    {
        Assert.True(
            Enum.IsDefined(typeof(OnnxModelType), "VisionGenAI"),
            "OnnxModelType.VisionGenAI must be defined");
    }

    [Fact]
    public void VisionGenAI_IsNotEqualTo_GenAI()
    {
        Assert.NotEqual(OnnxModelType.VisionGenAI, OnnxModelType.GenAI);
    }

    [Fact]
    public void VisionGenAI_IsNotEqualTo_CausalLM()
    {
        Assert.NotEqual(OnnxModelType.VisionGenAI, OnnxModelType.CausalLM);
    }

    [Fact]
    public void AllEnumValues_ParseableFromString()
    {
        var values = Enum.GetNames(typeof(OnnxModelType));
        foreach (var name in values)
        {
            var parsed = Enum.Parse<OnnxModelType>(name);
            Assert.True(Enum.IsDefined(typeof(OnnxModelType), parsed),
                $"OnnxModelType.{name} must be parseable and defined");
        }
    }
}
