using BenchmarkDotNet.Attributes;
using ElBruno.LocalLLMs;
using ElBruno.LocalLLMs.Internal;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalLLMs.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ChatTemplateBenchmarks
{
    private List<ChatMessage> _threeMessageConversation = null!;
    private List<ChatMessage> _tenMessageConversation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _threeMessageConversation =
        [
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What is the capital of France?"),
            new(ChatRole.Assistant, "The capital of France is Paris."),
        ];

        _tenMessageConversation =
        [
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi! How can I help you today?"),
            new(ChatRole.User, "What is 2+2?"),
            new(ChatRole.Assistant, "2+2 equals 4."),
            new(ChatRole.User, "And what about 3+3?"),
            new(ChatRole.Assistant, "3+3 equals 6."),
            new(ChatRole.User, "Can you explain quantum computing?"),
            new(ChatRole.Assistant, "Quantum computing uses quantum mechanical phenomena like superposition and entanglement to process information."),
            new(ChatRole.User, "Thanks! One more question about AI."),
        ];
    }

    [Benchmark(Description = "ChatML - 3 messages")]
    public string ChatML_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "Phi3 - 3 messages")]
    public string Phi3_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Phi3);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "Llama3 - 3 messages")]
    public string Llama3_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Llama3);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "Qwen - 3 messages")]
    public string Qwen_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Qwen);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "Mistral - 3 messages")]
    public string Mistral_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Mistral);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "Gemma - 3 messages")]
    public string Gemma_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "DeepSeek - 3 messages")]
    public string DeepSeek_3Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.DeepSeek);
        return formatter.FormatMessages(_threeMessageConversation);
    }

    [Benchmark(Description = "ChatML - 10 messages")]
    public string ChatML_10Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.ChatML);
        return formatter.FormatMessages(_tenMessageConversation);
    }

    [Benchmark(Description = "Gemma - 10 messages")]
    public string Gemma_10Messages()
    {
        var formatter = ChatTemplateFactory.Create(ChatTemplateFormat.Gemma);
        return formatter.FormatMessages(_tenMessageConversation);
    }
}
