using Buscarr.Server.Settings;
using System.Net;
using System.Web;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Tests.Fakes;
using static Buscarr.Server.Tests.Fakes.FanartTvJson;

namespace Buscarr.Server.Tests.FanartTv;

public class FanartTvClientTests
{
    private const string ApiKey = "chave-de-teste";
    private const string NirvanaId = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";
    private const string Assets = $"http://assets.fanart.tv/fanart/music/{NirvanaId}";
    private const string SecureAssets = $"https://assets.fanart.tv/fanart/music/{NirvanaId}";

    private ServerSettings _settings = new();

    [Fact]
    public async Task ArtistImageAsync_AsksFanartTvForTheArtistWithTheApiKey()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()));

        await client.ArtistImageAsync(NirvanaId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"https://webservice.fanart.tv/v3/music/{NirvanaId}", request.Uri.GetLeftPart(UriPartial.Path));
        Assert.Equal(ApiKey, HttpUtility.ParseQueryString(request.Uri.Query)["api_key"]);
        Assert.StartsWith("buscarr/", request.UserAgent);
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsTheFullSizeOfTheMostLikedThumbOverHttps()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist(
            ("artistthumb", $"{Assets}/artistthumb/nirvana-a.jpg", 1),
            ("artistthumb", $"{Assets}/artistthumb/nirvana-b.jpg", 5),
            ("artistbackground", $"{Assets}/artistbackground/nirvana-c.jpg", 9))));

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Equal($"{SecureAssets}/artistthumb/nirvana-b.jpg", image);
    }

    [Fact]
    public async Task ArtistImageAsync_FallsBackToTheMostLikedBackground()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist(
            ("artistbackground", $"{Assets}/artistbackground/nirvana-a.jpg", 2),
            ("artistbackground", $"{Assets}/artistbackground/nirvana-b.jpg", 7),
            ("musiclogo", $"{Assets}/musiclogo/nirvana-c.png", 9))));

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Equal($"{SecureAssets}/artistbackground/nirvana-b.jpg", image);
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsNullWhenFanartTvHasNoImageOfTheArtist()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(NotFound(), HttpStatusCode.NotFound));

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Null(image);
    }

    [Fact]
    public async Task ArtistImageAsync_DoesNotAskFanartTvWithoutAnApiKey()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()), apiKey: null);

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Null(image);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ArtistImageAsync_DoesNotAskForIdsThatAreNotMusicBrainzIds()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()));

        var image = await client.ArtistImageAsync("../../v3/movies/1");

        Assert.Null(image);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ArtistImageAsync_ThrowsWhenFanartTvFails()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ArtistImageAsync(NirvanaId));
    }

    [Fact]
    public async Task ArtistImageAsync_SendsTheProjectKeyWithThePersonalKeyAsClientKey()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Artist()));
        var client = new FanartTvClient(
            new HttpClient(handler),
            () => new ServerSettings { FanartTvApiKey = "chave-pessoal" },
            "chave-de-projeto");

        await client.ArtistImageAsync(NirvanaId);

        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("chave-de-projeto", query["api_key"]);
        Assert.Equal("chave-pessoal", query["client_key"]);
    }

    [Fact]
    public async Task ArtistImageAsync_SendsNoClientKeyWithoutAPersonalKey()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Artist()));
        var client = new FanartTvClient(new HttpClient(handler), () => new ServerSettings(), "chave-de-projeto");

        await client.ArtistImageAsync(NirvanaId);

        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("chave-de-projeto", query["api_key"]);
        Assert.Null(query["client_key"]);
    }

    [Fact]
    public void IsConfigured_IsTrueWithAProjectKeyAlone()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Artist()));
        var client = new FanartTvClient(new HttpClient(handler), () => new ServerSettings(), "chave-de-projeto");

        Assert.True(client.IsConfigured);
    }

    [Fact]
    public async Task ArtistImageAsync_SendsTheProjectKeyByDefaultAndReadsTheClientKeyFromTheCurrentSettings()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Artist()));
        var client = new FanartTvClient(new HttpClient(handler), () => _settings);

        await client.ArtistImageAsync(NirvanaId);
        _settings = new ServerSettings { FanartTvApiKey = "outra-chave" };
        await client.ArtistImageAsync(NirvanaId);

        Assert.Equal(2, handler.Requests.Count);
        var first = HttpUtility.ParseQueryString(handler.Requests[0].Uri.Query);
        var second = HttpUtility.ParseQueryString(handler.Requests[1].Uri.Query);
        Assert.Equal(FanartTvClient.ProjectKey, first["api_key"]);
        Assert.Null(first["client_key"]);
        Assert.Equal(FanartTvClient.ProjectKey, second["api_key"]);
        Assert.Equal("outra-chave", second["client_key"]);
    }

    [Fact]
    public async Task PingAsync_AsksForAKnownArtistToCheckTheApiKey()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()));

        await client.PingAsync();

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("https://webservice.fanart.tv/v3/music/", request.Uri.GetLeftPart(UriPartial.Path));
        Assert.Equal(ApiKey, HttpUtility.ParseQueryString(request.Uri.Query)["api_key"]);
        Assert.StartsWith("buscarr/", request.UserAgent);
    }

    [Fact]
    public async Task PingAsync_AcceptsANotFoundBecauseTheKeyWorked()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(NotFound(), HttpStatusCode.NotFound));

        await client.PingAsync();
    }

    [Fact]
    public async Task PingAsync_ThrowsAnInvalidKeyErrorWhenFanartTvRefuses()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.Unauthorized));

        var error = await Assert.ThrowsAsync<HttpRequestException>(client.PingAsync);

        Assert.Equal("chave inválida", error.Message);
    }

    [Fact]
    public async Task PingAsync_ThrowsWhenFanartTvFails()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(client.PingAsync);
    }

    [Fact]
    public async Task PingAsync_ThrowsWhenNotConfigured()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()), apiKey: null);

        await Assert.ThrowsAsync<InvalidOperationException>(client.PingAsync);

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(ApiKey, true)]
    public void IsConfigured_NeedsAnApiKey(string? apiKey, bool configured)
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist()), apiKey);

        Assert.Equal(configured, client.IsConfigured);
    }

    private static (FanartTvClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? apiKey = ApiKey)
    {
        var handler = new FakeHttpMessageHandler(respond);

        return (new FanartTvClient(new HttpClient(handler), apiKey), handler);
    }
}
