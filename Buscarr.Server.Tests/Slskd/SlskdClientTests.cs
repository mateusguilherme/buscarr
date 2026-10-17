using System.Net;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;
using static Buscarr.Server.Tests.Fakes.SlskdJson;

namespace Buscarr.Server.Tests.Slskd;

public class SlskdClientTests
{
    private const string Url = "http://slskd.local:5030";
    private const string ApiKey = "chave-de-teste";

    [Fact]
    public async Task ServerStateAsync_AsksSlskdForItsStateWithTheApiKey()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Application(true, true)));

        await client.ServerStateAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"{Url}/api/v0/application", request.Uri.ToString());
        Assert.Equal(ApiKey, request.Headers["X-API-Key"]);
        Assert.StartsWith("buscarr/", request.UserAgent);
    }

    [Fact]
    public async Task ServerStateAsync_AcceptsAUrlEndingInASlash()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Application(true, true)), $"{Url}/");

        await client.ServerStateAsync();

        Assert.Equal($"{Url}/api/v0/application", Assert.Single(handler.Requests).Uri.ToString());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ServerStateAsync_TellsWhetherSlskdIsConnectedAndLoggedInToSoulseek(bool connected, bool loggedIn)
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Application(connected, loggedIn)));

        var state = await client.ServerStateAsync();

        Assert.Equal(new SlskdServerState(connected, loggedIn), state);
    }

    [Theory]
    [InlineData(null, ApiKey)]
    [InlineData(Url, null)]
    [InlineData("", "")]
    public async Task ServerStateAsync_DoesNotAskSlskdWithoutUrlAndApiKey(string? url, string? apiKey)
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Application(true, true)), url, apiKey);

        var state = await client.ServerStateAsync();

        Assert.Null(state);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ServerStateAsync_ThrowsWhenSlskdRefusesTheApiKey()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ServerStateAsync());
    }

    private static (SlskdClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? url = Url,
        string? apiKey = ApiKey)
    {
        var handler = new FakeHttpMessageHandler(respond);

        return (new SlskdClient(new HttpClient(handler), url, apiKey), handler);
    }
}
