using System.Runtime.CompilerServices;
using ElBruno.LocalLLMs.Tests.TestDoubles;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ElBruno.LocalLLMs.Tests;

public class StreamingCancellationTests
{
    [Fact]
    public async Task GetStreamingResponseAsync_CancelledAfterTokenGeneration_DoesNotYieldStaleToken()
    {
        var fakeModel = new ScriptedTextGenerationModel();
        using var cts = new CancellationTokenSource();
        fakeModel.EnqueueStreamingResponse((_, _, _) => CancelBeforeSecondTokenAsync(cts));

        await using var client = CreateClient(fakeModel);
        var tokens = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "stream")],
                cancellationToken: cts.Token))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    tokens.Add(update.Text);
                }
            }
        });

        Assert.Equal(["alpha"], tokens);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CancelledRequest_LeavesClientReusableForNextCall()
    {
        var fakeModel = new ScriptedTextGenerationModel();
        using var cts = new CancellationTokenSource();
        fakeModel.EnqueueStreamingResponse((_, _, _) => CancelBeforeSecondTokenAsync(cts));
        fakeModel.EnqueueBufferedResponse("recovered");

        await using var client = CreateClient(fakeModel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "cancel this")],
                cancellationToken: cts.Token))
            {
            }
        });

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "try again")]);

        Assert.Equal("recovered", response.Text);
    }

    private static LocalChatClient CreateClient(ScriptedTextGenerationModel fakeModel)
    {
        var options = new LocalLLMsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = "fake-model"
        };

        return new LocalChatClient(
            options,
            Substitute.For<IModelDownloader>(),
            modelFactory: new ScriptedTextGenerationModelFactory(fakeModel));
    }

    private static async IAsyncEnumerable<string> CancelBeforeSecondTokenAsync(
        CancellationTokenSource cancellationTokenSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return "alpha";
        await Task.Yield();

        cancellationTokenSource.Cancel();
        yield return "beta";
    }
}
