namespace MagenticUIServer.Agents.Tests.TestHelpers;

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public TestHttpMessageHandler(HttpResponseMessage response) => _response = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(_response);
}
