# SKILL: Microsoft.Extensions.AI Version Compatibility

## When to Use
When upgrading or pinning `Microsoft.Extensions.AI.Abstractions` package versions in .NET projects that implement `IChatClient`.

## Key Facts (v10.4.0)

### IChatClient Interface Changes
| Old (≤10.3.x) | New (10.4.0+) |
|----------------|---------------|
| `CompleteAsync(IList<ChatMessage>, ...)` | `GetResponseAsync(IEnumerable<ChatMessage>, ...)` |
| `CompleteStreamingAsync(IList<ChatMessage>, ...)` | `GetStreamingResponseAsync(IEnumerable<ChatMessage>, ...)` |
| Returns `Task<ChatCompletion>` | Returns `Task<ChatResponse>` |
| Returns `IAsyncEnumerable<StreamingChatCompletionUpdate>` | Returns `IAsyncEnumerable<ChatResponseUpdate>` |
| `GetService<TService>(object? key)` | `GetService(Type serviceType, object? serviceKey)` |

### ChatClientMetadata Constructor
| Old | New |
|-----|-----|
| `new ChatClientMetadata(providerName, providerUri, modelId)` | `new ChatClientMetadata(providerName, providerUri, defaultModelId)` |

### ChatResponseUpdate
- `Text` property is now **read-only** — use the constructor: `new ChatResponseUpdate(ChatRole.Assistant, tokenText)`
- `Role` and `Contents` are settable via constructor or property

### OnnxRuntimeGenAI (0.8.x)
- `Generator.GetNextTokens()` **removed**
- Use `generator.GetSequence(0)[^1]` to get the latest generated token

## Pattern
```csharp
// 10.4.0 IChatClient implementation
public async Task<ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
{
    var messageList = messages as IList<ChatMessage> ?? messages.ToList();
    // ... generate response ...
    return new ChatResponse(responseMessage) { ModelId = "model-id" };
}
```
