using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Settings;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using static Buscarr.Server.Tests.Fakes.MusicBrainzJson;

namespace Buscarr.Server.Tests.Endpoints;

public class ArtistEndpointsTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    [Fact]
    public async Task SearchArtists_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/artists?q=nirvana");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchArtists_ReturnsArtistsWhoseNameContainsQuery()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/artists?q=nirvana");

        Assert.Equal(
            [
                new ArtistCard("1", "Approaching Nirvana", null, null, null, false),
                new ArtistCard(
                    BuscarrApiFactory.NirvanaId,
                    "Nirvana",
                    "90s US grunge band",
                    "United States",
                    BuscarrApiFactory.NirvanaImage,
                    false),
            ],
            artists);
    }

    [Fact]
    public async Task SearchArtists_WhenFanartTvFails_ReturnsArtistsWithoutImages()
    {
        await using var fanartTvDown = new BuscarrApiFactory { FanartTvDown = true };
        var client = await fanartTvDown.CreateLoggedInClientAsync();

        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/artists?q=nirvana");

        Assert.Equal(["1", BuscarrApiFactory.NirvanaId], artists!.Select(a => a.Id));
        Assert.All(artists!, artist => Assert.Null(artist.Image));
    }

    [Fact]
    public async Task SearchArtists_WhenNothingMatches_ReturnsEmptyList()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/artists?q=zzqxjvkwplm");

        Assert.Empty(artists!);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReleases_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient()
            .GetAsync($"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReleases_ListsTheArtistsReleasesNewestFirst()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(
            [
                new ArtistRelease(
                    "g1",
                    "Nevermind",
                    "album",
                    "1991-09-24",
                    "https://coverartarchive.org/release/r1/front-250",
                    12,
                    false,
                    false),
                new ArtistRelease(
                    "g2",
                    "Sliver",
                    "single",
                    "1990-09-01",
                    "https://coverartarchive.org/release/r3/front-250",
                    2,
                    false,
                    false),
            ],
            releases);
    }

    [Fact]
    public async Task GetReleases_FallsBackToTheDeezerCoverWhenTheArchiveHasNone()
    {
        await using var own = new BuscarrApiFactory();
        own.MusicBrainz.Respond = request =>
            request.RequestUri!.AbsolutePath == "/ws/2/release"
                ? FakeHttpMessageHandler.Json(Releases(
                    1,
                    WithArtistCredit(
                        WithMedia(
                            WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false),
                            ("CD", 12)),
                        "Nirvana")))
                : FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
        own.Deezer.Respond = _ => FakeHttpMessageHandler.Json(
            DeezerJson.Albums(("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/nevermind.jpg")));
        var client = await own.CreateLoggedInClientAsync();

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal("https://cdn-images.dzcdn.net/nevermind.jpg", Assert.Single(releases!).CoverUrl);
    }

    [Fact]
    public async Task GetReleases_KeepsTheArchiveFallbackWhenDeezerAndSpotifyHaveNothing()
    {
        await using var own = new BuscarrApiFactory();
        own.MusicBrainz.Respond = request =>
            request.RequestUri!.AbsolutePath == "/ws/2/release"
                ? FakeHttpMessageHandler.Json(Releases(
                    1,
                    WithArtistCredit(
                        WithMedia(
                            WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false),
                            ("CD", 12)),
                        "Nirvana")))
                : FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
        var client = await own.CreateLoggedInClientAsync();

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(
            "https://coverartarchive.org/release-group/g1/front-250",
            Assert.Single(releases!).CoverUrl);
    }

    [Fact]
    public async Task GetReleases_MarksTheAlbumsAlreadyInTheLibrary()
    {
        await using var own = new BuscarrApiFactory();
        own.Subsonic.Respond = _ => FakeHttpMessageHandler.Json(SubsonicJson.AlbumList(
            SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 12, musicBrainzId: "r1")));
        var client = await own.CreateLoggedInClientAsync();
        await ConfigureSubsonicAsync(own);

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(
            [("g1", true), ("g2", false)],
            releases!.Select(release => (release.ReleaseGroupId, release.InLibrary)));
    }

    [Fact]
    public async Task GetReleases_WhenTheLibraryIsOffline_StillListsTheReleases()
    {
        await using var own = new BuscarrApiFactory();
        own.Subsonic.Respond = _ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        var client = await own.CreateLoggedInClientAsync();
        await ConfigureSubsonicAsync(own);

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(["g1", "g2"], releases!.Select(release => release.ReleaseGroupId));
        Assert.All(releases!, release => Assert.False(release.InLibrary));
    }

    private static async Task ConfigureSubsonicAsync(BuscarrApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var store = factory.Services.GetRequiredService<ServerSettingsStore>();

        await store.SaveAsync(db, store.Current with
        {
            SubsonicUrl = "http://navidrome.local:4533",
            SubsonicUser = "fulano",
            SubsonicPassword = "segredo123",
        });
    }

    [Fact]
    public async Task GetReleases_MarksTheAlbumsAlreadyRequested()
    {
        await using var own = new BuscarrApiFactory();
        var client = await own.CreateLoggedInClientAsync();

        using (var scope = own.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
            db.DownloadRequests.Add(DownloadRequestTests.Request("g1"));
            await db.SaveChangesAsync();
        }

        var releases = await client.GetFromJsonAsync<List<ArtistRelease>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal([("g1", true), ("g2", false)], releases!.Select(release => (release.ReleaseGroupId, release.Requested)));
    }
}
