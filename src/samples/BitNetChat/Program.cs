using ElBruno.LocalLLMs.BitNet;
using Microsoft.Extensions.AI;

var nativePath = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("BITNET_NATIVE_PATH");
var modelPath = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("BITNET_MODEL_PATH");

if (string.IsNullOrWhiteSpace(modelPath))
{
    Console.WriteLine("BitNet model path not set. Set BITNET_MODEL_PATH or pass it as the second argument.");
    return;
}

if (!File.Exists(modelPath))
{
    Console.WriteLine($"BitNet model file not found: {modelPath}");
    return;
}

if (!string.IsNullOrWhiteSpace(nativePath) && !Directory.Exists(nativePath))
{
    Console.WriteLine($"BitNet native library path not found: {nativePath}");
    return;
}

var options = new BitNetOptions
{
    Model = BitNetKnownModels.BitNet2B4T,
    NativeLibraryPath = nativePath,
    ModelPath = modelPath
};

using var client = new BitNetChatClient(options);

var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Hello! Can you introduce yourself in one sentence?")
};

var greetingResponse = await client.GetResponseAsync(messages);
Console.WriteLine($"Assistant: {greetingResponse.Text}");
messages.Add(new ChatMessage(ChatRole.Assistant, greetingResponse.Text));

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        break;
    }

    messages.Add(new ChatMessage(ChatRole.User, input));

    var response = await client.GetResponseAsync(messages);
    Console.WriteLine($"Assistant: {response.Text}");
    messages.Add(new ChatMessage(ChatRole.Assistant, response.Text));
}
