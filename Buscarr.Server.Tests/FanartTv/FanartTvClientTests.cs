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
    private const string Previews = $"https://assets.fanart.tv/preview/music/{NirvanaId}";

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
    public async Task ArtistImageAsync_ReturnsAPreviewOfTheMostLikedThumb()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist(
            ("artistthumb", $"{Assets}/artistthumb/nirvana-a.jpg", 1),
            ("artistthumb", $"{Assets}/artistthumb/nirvana-b.jpg", 5),
            ("artistbackground", $"{Assets}/artistbackground/nirvana-c.jpg", 9))));

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Equal($"{Previews}/artistthumb/nirvana-b.jpg", image);
    }

    [Fact]
    public async Task ArtistImageAsync_FallsBackToTheMostLikedBackground()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artist(
            ("artistbackground", $"{Assets}/artistbackground/nirvana-a.jpg", 2),
            ("artistbackground", $"{Assets}/artistbackground/nirvana-b.jpg", 7),
            ("musiclogo", $"{Assets}/musiclogo/nirvana-c.png", 9))));

        var image = await client.ArtistImageAsync(NirvanaId);

        Assert.Equal($"{Previews}/artistbackground/nirvana-b.jpg", image);
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
    public async Task ArtistImageAsync_ReadsTheApiKeyFromTheCurrentSettings()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(Artist()));
        var settings = new ServerSettings();
        var client = new FanartTvClient(new HttpClient(handler), () => settings);

        await client.ArtistImageAsync(NirvanaId);
        settings = new ServerSettings { FanartTvApiKey = "outra-chave" };
        await client.ArtistImageAsync(NirvanaId);

        Assert.Equal("outra-chave", HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query)["api_key"]);
    }

    private static (FanartTvClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? apiKey = ApiKey)
    {
        var handler = new FakeHttpMessageHandler(respond);

        return (new FanartTvClient(new HttpClient(handler), apiKey), handler);
    }
}
