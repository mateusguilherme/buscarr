using System.Net;
using System.Text;
using System.Web;
using Buscarr.Server.Spotify;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Spotify;

public sealed class SpotifyClientTests
{
    private const string Cover = "https://i.scdn.co/image/abc";

    private readonly FakeTimeProvider _time = new();

    private static FakeHttpMessageHandler Handler(string albums) =>
        new(request => request.RequestUri!.Host == "accounts.spotify.com"
            ? FakeHttpMessageHandler.Json(SpotifyJson.Token())
            : FakeHttpMessageHandler.Json(albums));

    private SpotifyClient Client(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), "id-1", "segredo-1", _time);

    [Fact]
    public async Task AlbumCoverAsync_ReturnsThe300PixelCoverOfTheAlbumThatMatches()
    {
        var handler = Handler(SpotifyJson.Albums(
            ("AJULLIACOSTA", "Do Corre (Ao Vivo)", "https://i.scdn.co/image/outra"),
            ("AJULLIACOSTA", "Do Corre", Cover)));

        var cover = await Client(handler).AlbumCoverAsync("AJULLIACOSTA", "Do Corre");

        Assert.Equal(Cover, cover);
        var token = handler.Requests[0];
        Assert.Equal("https://accounts.spotify.com/api/token", token.Uri.ToString());
        Assert.Equal(
            $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("id-1:segredo-1"))}",
            token.Headers["Authorization"]);
        Assert.Contains("grant_type=client_credentials", token.Body);
        var search = handler.Requests[1];
        Assert.Equal("api.spotify.com", search.Uri.Host);
        Assert.Equal("Bearer token-1", search.Headers["Authorization"]);
        Assert.Equal("artist:\"AJULLIACOSTA\" album:\"Do Corre\"", HttpUtility.ParseQueryString(search.Uri.Query)["q"]);
    }

    [Fact]
    public async Task AlbumCoverAsync_ReusesTheTokenWhileItLives()
    {
        var handler = Handler(SpotifyJson.Albums(("Nirvana", "Nevermind", Cover)));
        var client = Client(handler);

        await client.AlbumCoverAsync("Nirvana", "Nevermind");
        _time.Advance(TimeSpan.FromMinutes(30));
        await client.AlbumCoverAsync("Nirvana", "Nevermind");

        Assert.Equal(1, handler.Requests.Count(request => request.Uri.Host == "accounts.spotify.com"));
    }

    [Fact]
    public async Task AlbumCoverAsync_RenewsAnExpiredToken()
    {
        var handler = Handler(SpotifyJson.Albums(("Nirvana", "Nevermind", Cover)));
        var client = Client(handler);

        await client.AlbumCoverAsync("Nirvana", "Nevermind");
        _time.Advance(TimeSpan.FromHours(2));
        await client.AlbumCoverAsync("Nirvana", "Nevermind");

        Assert.Equal(2, handler.Requests.Count(request => request.Uri.Host == "accounts.spotify.com"));
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullWithoutCredentialsWithoutCallingSpotify()
    {
        var handler = Handler(SpotifyJson.Albums(("Nirvana", "Nevermind", Cover)));
        var client = new SpotifyClient(new HttpClient(handler), null, null, _time);

        Assert.Null(await client.AlbumCoverAsync("Nirvana", "Nevermind"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullWithoutAnExactMatch()
    {
        var handler = Handler(SpotifyJson.Albums(("Nirvana", "Nevermind (Deluxe)", Cover)));

        Assert.Null(await Client(handler).AlbumCoverAsync("Nirvana", "Nevermind"));
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullWhenSpotifyFails()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        Assert.Null(await Client(handler).AlbumCoverAsync("Nirvana", "Nevermind"));
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsThe300PixelCoverOfTheFirstMatch()
    {
        var handler = Handler(SpotifyJson.Albums(("AJULLIACOSTA", "SALA VERMELHA #03 - Ajuliacosta", Cover)));

        var cover = await Client(handler).AlbumCoverByBarcodeAsync("198748762851");

        Assert.Equal(Cover, cover);
        var search = handler.Requests[1];
        Assert.Equal("upc:198748762851", HttpUtility.ParseQueryString(search.Uri.Query)["q"]);
        Assert.Equal("album", HttpUtility.ParseQueryString(search.Uri.Query)["type"]);
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsNullWithoutAMatch()
    {
        var handler = Handler(SpotifyJson.Albums());

        Assert.Null(await Client(handler).AlbumCoverByBarcodeAsync("198748762851"));
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsNullWithoutCredentialsWithoutCallingSpotify()
    {
        var handler = Handler(SpotifyJson.Albums());
        var client = new SpotifyClient(new HttpClient(handler), null, null, _time);

        Assert.Null(await client.AlbumCoverByBarcodeAsync("198748762851"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsTheLargeImageOfTheArtistPage()
    {
        var image = "https://i.scdn.co/image/artista";
        var handler = new FakeHttpMessageHandler(request => request.RequestUri!.Host == "accounts.spotify.com"
            ? FakeHttpMessageHandler.Json(SpotifyJson.Token())
            : FakeHttpMessageHandler.Json(SpotifyJson.Artist("AJULLIACOSTA", image)));

        var result = await Client(handler).ArtistImageAsync("0eHkGhhUyp3LZdtZjaA8gd");

        Assert.Equal($"{image}?size=640", result);
        Assert.Equal(
            "https://api.spotify.com/v1/artists/0eHkGhhUyp3LZdtZjaA8gd",
            handler.Requests[1].Uri.ToString());
    }

    [Fact]
    public async Task ArtistImageAsync_TakesTheLargestImageWhenNoneIsLargeEnough()
    {
        var handler = new FakeHttpMessageHandler(request => request.RequestUri!.Host == "accounts.spotify.com"
            ? FakeHttpMessageHandler.Json(SpotifyJson.Token())
            : FakeHttpMessageHandler.Json(SpotifyJson.Artist(
                "AJULLIACOSTA",
                ("https://i.scdn.co/image/media", 320),
                ("https://i.scdn.co/image/pequena", 160))));

        var result = await Client(handler).ArtistImageAsync("0eHkGhhUyp3LZdtZjaA8gd");

        Assert.Equal("https://i.scdn.co/image/media", result);
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsNullWithoutCredentialsWithoutCallingSpotify()
    {
        var handler = Handler(SpotifyJson.Albums());
        var client = new SpotifyClient(new HttpClient(handler), null, null, _time);

        Assert.Null(await client.ArtistImageAsync("0eHkGhhUyp3LZdtZjaA8gd"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PingAsync_PassesWithGoodCredentialsAndThrowsOnBadOnes()
    {
        var good = Handler(SpotifyJson.Albums());
        var bad = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.BadRequest));

        await Client(good).PingAsync();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Client(bad).PingAsync());

        Assert.Equal("credenciais inválidas", error.Message);
    }

    [Fact]
    public async Task PingAsync_ThrowsWithoutCredentials()
    {
        var handler = Handler(SpotifyJson.Albums());
        var client = new SpotifyClient(new HttpClient(handler), null, null, _time);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PingAsync());
    }
}
