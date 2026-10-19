using Buscarr.Server.Deezer;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Spotify;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Lists;

public class ArtistImagesTests
{
    private const string ArtistId = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";
    private const string SpotifyPhoto = "https://i.scdn.co/image/artista";
    private const string DeezerPhoto = "https://cdn-images.dzcdn.net/images/artist/abc/500x500-000000-80-0-0.jpg";
    private const string FanartPhoto = $"https://assets.fanart.tv/fanart/music/{ArtistId}/artistthumb/a.jpg";

    private bool _spotifyHasImages = true;

    [Fact]
    public async Task FindAsync_PrefersTheSpotifyPhoto()
    {
        var artist = ArtistWith(
            "https://open.spotify.com/artist/abc",
            "https://www.deezer.com/artist/52654632");

        var image = await Images().FindAsync(artist);

        Assert.Equal($"{SpotifyPhoto}?size=640", image);
    }

    [Fact]
    public async Task FindAsync_FallsBackToDeezerWhenSpotifyHasNoPhoto()
    {
        _spotifyHasImages = false;
        var artist = ArtistWith(
            "https://open.spotify.com/artist/abc",
            "https://www.deezer.com/artist/52654632");

        var image = await Images().FindAsync(artist);

        Assert.Equal(DeezerPhoto, image);
    }

    [Fact]
    public async Task FindAsync_FallsBackToFanartWhenThereAreNoStreamingLinks()
    {
        var artist = ArtistWith();

        var image = await Images().FindAsync(artist);

        Assert.Equal(FanartPhoto, image);
    }

    private static Artist ArtistWith(params string[] urls) =>
        new(ArtistId, "AJULLIACOSTA")
        {
            Relations = [.. urls.Select(url => new UrlRelation { Url = new UrlTarget { Resource = url } })],
        };

    private ArtistImages Images()
    {
        var http = () => new HttpClient(new FakeHttpMessageHandler(Respond));

        return new ArtistImages(
            new FanartTvClient(http(), "chave-de-teste"),
            new DeezerClient(http()),
            new SpotifyClient(http(), "id-1", "segredo-1", new FakeTimeProvider()));
    }

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var host = request.RequestUri!.Host;

        if (host == "accounts.spotify.com")
        {
            return FakeHttpMessageHandler.Json(SpotifyJson.Token());
        }

        if (host == "api.spotify.com")
        {
            return FakeHttpMessageHandler.Json(_spotifyHasImages
                ? SpotifyJson.Artist("AJULLIACOSTA", SpotifyPhoto)
                : """{"name":"AJULLIACOSTA","images":[]}""");
        }

        if (host == "api.deezer.com")
        {
            return FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile("AJULLIACOSTA", DeezerPhoto));
        }

        return FakeHttpMessageHandler.Json(FanartTvJson.Artist(("artistthumb", FanartPhoto, 5)));
    }
}
