using ElBruno.LocalLLMs.ToolCalling;

namespace ElBruno.LocalLLMs.Tests.ToolCalling;

/// <summary>
/// Regression tests using verbatim GPT-OSS 20B output captured from the real ONNX model
/// (onnxruntime/gpt-oss-20b-onnx, CPU INT4) on 2026-08-12.
/// </summary>
public class HarmonyRealModelOutputTests
{
    private const string RealToolCall =
        "<|channel|>analysis<|message|>The user asks: \"What is the weather in Paris?\" " +
        "We need to call the get_weather function with city=\"Paris\".<|end|>" +
        "<|start|>assistant<|channel|>commentary to=functions.get_weather <|constrain|>json" +
        "<|message|>{\"city\":\"Paris\"}<|call|>";

    private const string RealAnswer =
        "<|channel|>analysis<|message|>Need answer: Paris.<|end|>" +
        "<|start|>assistant<|channel|>final<|message|>The capital of France is Paris.<|return|>";

    [Fact]
    public void RealToolCall_ParsesNameAndArguments()
    {
        var call = Assert.Single(new HarmonyToolCallParser().Parse(RealToolCall));

        Assert.Equal("get_weather", call.FunctionName);
        Assert.Equal("Paris", call.Arguments["city"]);
    }

    [Fact]
    public void RealAnswer_YieldsOnlyFinalChannel()
    {
        Assert.Equal(
            "The capital of France is Paris.",
            ElBruno.LocalLLMs.Internal.HarmonyChannelFilter.ExtractFinal(RealAnswer));
    }
}
