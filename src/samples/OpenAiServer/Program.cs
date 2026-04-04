using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
});

var app = builder.Build();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// ── GET / — welcome ──
app.MapGet("/", () => "ElBruno.LocalLLMs — OpenAI-compatible local LLM server. Use /v1/models and /v1/chat/completions.");

// ── GET /v1/models — list available models ──
app.MapGet("/v1/models", () =>
{
    var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var data = KnownModels.All.Select(m => new ModelObject
    {
        Id = m.Id,
        Object = "model",
        Created = epoch,
        OwnedBy = "local"
    }).ToList();

    return Results.Json(new ModelListResponse { Object = "list", Data = data }, jsonOptions);
});

// ── POST /v1/chat/completions ──
app.MapPost("/v1/chat/completions", async (HttpContext ctx, IChatClient client) =>
{
    var request = await JsonSerializer.DeserializeAsync<ChatCompletionRequest>(
        ctx.Request.Body, jsonOptions, ctx.RequestAborted);

    if (request is null)
        return Results.BadRequest(new { error = "Invalid request body." });

    // Build MEAI messages
    var messages = request.Messages.Select(m =>
        new ChatMessage(new ChatRole(m.Role), m.Content)).ToList();

    var chatOptions = new ChatOptions
    {
        MaxOutputTokens = request.MaxTokens ?? 2048,
        Temperature = request.Temperature ?? 0.7f
    };

    var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var completionId = $"chatcmpl-{Guid.NewGuid():N}";
    var modelId = request.Model ?? "phi-3.5-mini-instruct";

    if (request.Stream == true)
    {
        // SSE streaming
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["Connection"] = "keep-alive";

        await foreach (var update in client.GetStreamingResponseAsync(
            messages, chatOptions, ctx.RequestAborted))
        {
            var chunk = new ChatCompletionChunk
            {
                Id = completionId,
                Object = "chat.completion.chunk",
                Created = epoch,
                Model = modelId,
                Choices =
                [
                    new ChunkChoice
                    {
                        Index = 0,
                        Delta = new DeltaMessage { Role = "assistant", Content = update.Text },
                        FinishReason = null
                    }
                ]
            };

            var json = JsonSerializer.Serialize(chunk, jsonOptions);
            await ctx.Response.WriteAsync($"data: {json}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }

        // Final chunk with finish_reason
        var doneChunk = new ChatCompletionChunk
        {
            Id = completionId,
            Object = "chat.completion.chunk",
            Created = epoch,
            Model = modelId,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new DeltaMessage(),
                    FinishReason = "stop"
                }
            ]
        };
        var doneJson = JsonSerializer.Serialize(doneChunk, jsonOptions);
        await ctx.Response.WriteAsync($"data: {doneJson}\n\n", ctx.RequestAborted);
        await ctx.Response.WriteAsync("data: [DONE]\n\n", ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

        return Results.Empty;
    }
    else
    {
        // Non-streaming
        var response = await client.GetResponseAsync(messages, chatOptions, ctx.RequestAborted);
        var text = response.Text ?? string.Empty;

        var result = new ChatCompletionResponse
        {
            Id = completionId,
            Object = "chat.completion",
            Created = epoch,
            Model = modelId,
            Choices =
            [
                new CompletionChoice
                {
                    Index = 0,
                    Message = new ResponseMessage { Role = "assistant", Content = text },
                    FinishReason = "stop"
                }
            ],
            Usage = new UsageInfo
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0
            }
        };

        return Results.Json(result, jsonOptions);
    }
});

app.Run();

// ── OpenAI-compatible DTOs ──

record ModelObject
{
    public required string Id { get; init; }
    public required string Object { get; init; }
    public required long Created { get; init; }
    public required string OwnedBy { get; init; }
}

record ModelListResponse
{
    public required string Object { get; init; }
    public required List<ModelObject> Data { get; init; }
}

record ChatCompletionRequest
{
    public string? Model { get; init; }
    public List<RequestMessage> Messages { get; init; } = [];
    public bool? Stream { get; init; }
    public int? MaxTokens { get; init; }
    public float? Temperature { get; init; }
}

record RequestMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
}

record ChatCompletionResponse
{
    public required string Id { get; init; }
    public required string Object { get; init; }
    public required long Created { get; init; }
    public required string Model { get; init; }
    public required List<CompletionChoice> Choices { get; init; }
    public UsageInfo? Usage { get; init; }
}

record CompletionChoice
{
    public int Index { get; init; }
    public required ResponseMessage Message { get; init; }
    public string? FinishReason { get; init; }
}

record ResponseMessage
{
    public string Role { get; init; } = "assistant";
    public string Content { get; init; } = string.Empty;
}

record ChatCompletionChunk
{
    public required string Id { get; init; }
    public required string Object { get; init; }
    public required long Created { get; init; }
    public required string Model { get; init; }
    public required List<ChunkChoice> Choices { get; init; }
}

record ChunkChoice
{
    public int Index { get; init; }
    public required DeltaMessage Delta { get; init; }
    public string? FinishReason { get; init; }
}

record DeltaMessage
{
    public string? Role { get; init; }
    public string? Content { get; init; }
}

record UsageInfo
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
}
