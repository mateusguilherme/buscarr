using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task StartSearchAsync_PostsTheSearchTextUnderANewId()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Search(Guid.NewGuid(), "InProgress")));

        var id = await client.StartSearchAsync("Nirvana Nevermind");

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{Url}/api/v0/searches", request.Uri.ToString());
        Assert.Equal(ApiKey, request.Headers["X-API-Key"]);
        Assert.Equal("Nirvana Nevermind", body.RootElement.GetProperty("searchText").GetString());
        Assert.Equal(id, body.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task StartSearchAsync_ReturnsNullWhenSlskdIsBusyWithAnotherSearch()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        Assert.Null(await client.StartSearchAsync("Nirvana Nevermind"));
    }

    [Fact]
    public async Task StartSearchAsync_ThrowsWhenSlskdFails()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.StartSearchAsync("Nirvana Nevermind"));
    }

    [Fact]
    public async Task StartSearchAsync_ThrowsWithoutUrlAndApiKey()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.OK), url: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartSearchAsync("Nirvana Nevermind"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_ReadsWhetherTheSearchHasEnded()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Search(id, "Completed, TimedOut", 7)));

        var search = await client.SearchAsync(id);

        Assert.Equal($"{Url}/api/v0/searches/{id}", Assert.Single(handler.Requests).Uri.ToString());
        Assert.Equal(new SlskdSearch(id, "Completed, TimedOut", true, 7), search);
    }

    [Fact]
    public async Task SearchResponsesAsync_ReadsTheFilesEachPeerHas()
    {
        const string smells = @"@@musica\Nirvana\Nevermind\01 - Smells Like Teen Spirit.mp3";
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Responses(Response(
            "fulano",
            [AudioFile(smells, 320, 12_000_000, 301)],
            hasFreeUploadSlot: false,
            queueLength: 4,
            uploadSpeed: 250_000))));

        var responses = await client.SearchResponsesAsync(id);

        Assert.Equal($"{Url}/api/v0/searches/{id}/responses", Assert.Single(handler.Requests).Uri.ToString());
        var response = Assert.Single(responses);
        Assert.Equal(
            ("fulano", false, 4, 250_000),
            (response.Username, response.HasFreeUploadSlot, response.QueueLength, response.UploadSpeed));
        Assert.Equal(new SlskdFile(smells, 12_000_000, 320, null, 301), Assert.Single(response.Files));
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
