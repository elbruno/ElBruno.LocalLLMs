using ElBruno.AI.Decisions;
using ElBruno.LocalLLMs.Decisions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

// Uses the local Laya model through the provider-neutral ElBruno.AI.Decisions API.
//
// The app below only knows IDecisionClient. Swap AddLocalDecisions for the Foundry,
// Ollama or Jev provider and nothing else changes. The same client also drives a
// DecisionRoutingChatClient that picks which IChatClient answers each message.
//
// Pass a local model directory as the first argument to skip the download.

var services = new ServiceCollection();
services.AddLocalDecisions(options => options.ModelPath = args.Length > 0 ? args[0] : null);
using var provider = services.BuildServiceProvider();

IDecisionClient decisions = provider.GetRequiredService<IDecisionClient>();

var teams = new Dictionary<string, string?>
{
    ["billing"] = "Payment, invoice and refund problems",
    ["technical"] = "Bugs, outages, API errors and anything with a stack trace",
    ["sales"] = "Pricing, plan comparisons, upgrades and new purchases"
};

const string ticket = "My invoice charged me twice for the same subscription month and I want my money back.";

ChoiceDecision team = await decisions.ChooseAsync(ticket, "Which team should handle this ticket?", teams);
Console.WriteLine($"Team: {team.Choice} (confidence {team.Confidence:P1})");

AssessmentDecision angry = await decisions.AssessAsync(ticket, "The customer is frustrated.");
Console.WriteLine($"Frustrated: {angry.Probability:P1}");

// Routing: the decision model picks the chat client, so only one generative model is invoked.
var routes = new Dictionary<string, IChatClient>
{
    ["billing"] = new CannedChatClient("[billing agent] I'll look into the duplicate charge."),
    ["technical"] = new CannedChatClient("[technical agent] Let's look at that stack trace."),
    ["sales"] = new CannedChatClient("[sales agent] Happy to compare the plans.")
};

using var router = new DecisionRoutingChatClient(
    decisions,
    routes,
    new DecisionRoutingOptions { Instructions = "Which team should handle this message?", Descriptions = teams });

ChatResponse reply = await router.GetResponseAsync(ticket);
Console.WriteLine(reply.Text);

internal sealed class CannedChatClient(string text) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}