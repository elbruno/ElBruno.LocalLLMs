using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var question = new ChatMessage(ChatRole.User, "What is machine learning? Answer in one sentence.");

// Try with Phi-3.5 mini
Console.WriteLine("=== Phi-3.5 mini ===");
using (var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Phi35MiniInstruct
}))
{
    var response = await client.GetResponseAsync([question]);
    Console.WriteLine(response.Text);
}

// Try with Qwen 2.5 0.5B (tiny model)
Console.WriteLine("\n=== Qwen 2.5 0.5B ===");
using (var client = await LocalChatClient.CreateAsync(new LocalLLMsOptions
{
    Model = KnownModels.Qwen25_05BInstruct
}))
{
    var response = await client.GetResponseAsync([question]);
    Console.WriteLine(response.Text);
}
