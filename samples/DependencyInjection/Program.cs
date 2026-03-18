using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
});

var app = builder.Build();

app.MapPost("/chat", async (IChatClient client, HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var message = await reader.ReadToEndAsync();

    var response = await client.GetResponseAsync([
        new ChatMessage(ChatRole.User, message)
    ]);

    return response.Text;
});

app.MapGet("/", () => "ElBruno.LocalLLMs — POST /chat with a message to chat!");

app.Run();
