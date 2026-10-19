using System.Net;
using System.Web;
using Buscarr.Server.Deezer;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Deezer;

public sealed class DeezerClientTests
{
    private const string Cover = "https://cdn-images.dzcdn.net/images/cover/abc/250x250-000000-80-0-0.jpg";

    [Fact]
    public async Task AlbumCoverAsync_ReturnsTheCoverOfTheAlbumThatMatches()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums(
            ("Project46", "Tr3S (English Edition)", "https://cdn-images.dzcdn.net/images/cover/outra/250x250.jpg"),
            ("Project46", "Tr3S", Cover))));
        var client = new DeezerClient(new HttpClient(handler));

        var cover = await client.AlbumCoverAsync("Project46", "Tr3s");

        Assert.Equal(Cover, cover);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("api.deezer.com", request.Uri.Host);
        Assert.Equal("artist:\"Project46\" album:\"Tr3s\"", HttpUtility.ParseQueryString(request.Uri.Query)["q"]);
    }

    [Fact]
    public async Task AlbumCoverAsync_PrefersTheBigCover()
    {
        var big = "https://cdn-images.dzcdn.net/images/cover/abc/500x500-000000-80-0-0.jpg";
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.AlbumsWithBigCovers(
            ("Project46", "Tr3S", Cover, big))));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Equal(big, await client.AlbumCoverAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullWithoutAnExactTitle()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums(
            ("Project46", "Tr3S (English Edition)", Cover))));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumCoverAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullForAHomonymousAlbumOfAnotherArtist()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums(
            ("Outra Banda", "Tr3s", Cover))));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumCoverAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumCoverAsync_ReturnsNullWhenDeezerFails()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumCoverAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsTheCoverOfTheExactAlbum()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(DeezerJson.Album("AJULLIACOSTA", "SALA VERMELHA #03 - Ajuliacosta", Cover)));
        var client = new DeezerClient(new HttpClient(handler));

        var cover = await client.AlbumCoverByBarcodeAsync("198748762851");

        Assert.Equal(Cover, cover);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.deezer.com/album/upc:198748762851", request.Uri.ToString());
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_PrefersTheBigCover()
    {
        var big = "https://cdn-images.dzcdn.net/images/cover/abc/500x500-000000-80-0-0.jpg";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(DeezerJson.Album("AJULLIACOSTA", "SALA VERMELHA #03 - Ajuliacosta", Cover, big)));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Equal(big, await client.AlbumCoverByBarcodeAsync("198748762851"));
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsNullForABarcodeDeezerDoesNotKnow()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.NotFound()));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumCoverByBarcodeAsync("198748762851"));
    }

    [Fact]
    public async Task AlbumCoverByBarcodeAsync_ReturnsNullWhenDeezerFails()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumCoverByBarcodeAsync("198748762851"));
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsThePictureOfTheArtistPage()
    {
        var picture = "https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile("AJULLIACOSTA", picture)));
        var client = new DeezerClient(new HttpClient(handler));

        var image = await client.ArtistImageAsync("52654632");

        Assert.Equal(picture, image);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.deezer.com/artist/52654632", request.Uri.ToString());
    }

    [Fact]
    public async Task ArtistImageAsync_PrefersTheBigPicture()
    {
        var big = "https://cdn-images.dzcdn.net/images/artist/abc/500x500-000000-80-0-0.jpg";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile("AJULLIACOSTA", "medium.jpg", big)));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Equal(big, await client.ArtistImageAsync("52654632"));
    }

    [Fact]
    public async Task ArtistImageAsync_ReturnsNullWithoutAPictureOrOnFailure()
    {
        var empty = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile("AJULLIACOSTA", null)));
        var failing = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        Assert.Null(await new DeezerClient(new HttpClient(empty)).ArtistImageAsync("52654632"));
        Assert.Null(await new DeezerClient(new HttpClient(failing)).ArtistImageAsync("52654632"));
    }

    [Fact]
    public async Task AlbumDurationsAsync_ReturnsTheTrackDurationsOfTheAlbumThatMatches()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.StartsWith("/search", StringComparison.Ordinal)
                ? FakeHttpMessageHandler.Json(DeezerJson.Albums(
                    ("Project46", "Tr3S (English Edition)", Cover),
                    ("Project46", "Tr3S", Cover)))
                : FakeHttpMessageHandler.Json(DeezerJson.AlbumTracks(264, 150, 280)));
        var client = new DeezerClient(new HttpClient(handler));

        var durations = await client.AlbumDurationsAsync("Project46", "Tr3s");

        Assert.Equal([264, 150, 280], durations);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://api.deezer.com/album/2/tracks?limit=100", handler.Requests[1].Uri.ToString());
    }

    [Fact]
    public async Task AlbumDurationsAsync_ReturnsNullWithoutAnExactMatch()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums(
            ("Project46", "Tr3S (English Edition)", Cover))));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumDurationsAsync("Project46", "Tr3s"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AlbumDurationsAsync_ReturnsNullWhenTheAlbumHasNoTracks()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.StartsWith("/search", StringComparison.Ordinal)
                ? FakeHttpMessageHandler.Json(DeezerJson.Albums(("Project46", "Tr3s", Cover)))
                : FakeHttpMessageHandler.Json(DeezerJson.AlbumTracks()));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumDurationsAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumDurationsAsync_ReturnsNullWhenDeezerFails()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));
        var client = new DeezerClient(new HttpClient(handler));

        Assert.Null(await client.AlbumDurationsAsync("Project46", "Tr3s"));
    }

    [Fact]
    public async Task AlbumCoverAsync_StripsQuotesFromTheSearch()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums(
            ("Guns \"N\" Roses", "The \"Best\" Of", Cover))));
        var client = new DeezerClient(new HttpClient(handler));

        var cover = await client.AlbumCoverAsync("Guns \"N\" Roses", "The \"Best\" Of");

        Assert.Equal(Cover, cover);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("artist:\"Guns N Roses\" album:\"The Best Of\"", HttpUtility.ParseQueryString(request.Uri.Query)["q"]);
    }
}
