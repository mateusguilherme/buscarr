using Buscarr.Server.Settings;
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

    private ServerSettings _settings = new() { SlskdUrl = Url, SlskdApiKey = ApiKey };

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

    [Fact]
    public async Task EnqueueDownloadsAsync_AsksThePeerForEachFileWithItsSize()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.Created));

        await client.EnqueueDownloadsAsync(
            "fulano de tal",
            [
                new SlskdFile(@"@@musica\Nevermind\01.mp3", 1_000, 320, null, 301),
                new SlskdFile(@"@@musica\Nevermind\02.mp3", 2_000, 320, null, 303),
            ]);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{Url}/api/v0/transfers/downloads/fulano%20de%20tal", request.Uri.AbsoluteUri);
        Assert.Equal(
            [(@"@@musica\Nevermind\01.mp3", 1_000L), (@"@@musica\Nevermind\02.mp3", 2_000L)],
            body.RootElement.EnumerateArray().Select(file =>
                (file.GetProperty("filename").GetString(), file.GetProperty("size").GetInt64())));
    }

    [Fact]
    public async Task EnqueueDownloadsAsync_ThrowsWhenSlskdCannotAskThePeer()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.EnqueueDownloadsAsync("fulano", [new SlskdFile("01.mp3", 1_000, 320, null, 301)]));
    }

    [Fact]
    public async Task DownloadsAsync_ReadsEveryTransferFromThePeer()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(UserTransfers(
            "fulano",
            Transfer(id, @"@@musica\Nevermind\01.mp3", "Queued, Remotely", 1_000, 0, 7),
            Transfer(Guid.NewGuid(), @"@@musica\Nevermind\02.mp3", "InProgress", 2_000, 500))));

        var transfers = await client.DownloadsAsync("fulano");

        Assert.Equal($"{Url}/api/v0/transfers/downloads/fulano", Assert.Single(handler.Requests).Uri.ToString());
        Assert.Equal(new SlskdTransfer(id, @"@@musica\Nevermind\01.mp3", 1_000, "Queued, Remotely", 0, 7), transfers[0]);
        Assert.Equal(
            ("InProgress", 500L, (int?)null),
            (transfers[1].State, transfers[1].BytesTransferred, transfers[1].PlaceInQueue));
    }

    [Fact]
    public async Task DownloadsAsync_IsEmptyWhenNothingComesFromThePeer()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound));

        Assert.Empty(await client.DownloadsAsync("fulano"));
    }

    [Fact]
    public async Task CancelDownloadAsync_CancelsTheTransfer()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.NoContent));

        await client.CancelDownloadAsync("fulano", id);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"{Url}/api/v0/transfers/downloads/fulano/{id}", request.Uri.ToString());
    }

    [Fact]
    public async Task CancelDownloadAsync_IgnoresATransferThatIsAlreadyGone()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound));

        await client.CancelDownloadAsync("fulano", Guid.NewGuid());
    }

    [Fact]
    public async Task ReadsTheSettingsOnEveryCall()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Application(true, true)));
        var client = new SlskdClient(new HttpClient(handler), () => _settings);

        await client.ServerStateAsync();
        _settings = new ServerSettings { SlskdUrl = "http://outro:5030", SlskdApiKey = "outra-chave" };
        await client.ServerStateAsync();

        Assert.Equal("http://outro:5030/api/v0/application", handler.Requests[1].Uri.ToString());
        Assert.Equal("outra-chave", handler.Requests[1].Headers["X-API-Key"]);
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
