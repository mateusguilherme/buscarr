using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

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
                new ArtistCard("1", "Approaching Nirvana", null, null, null),
                new ArtistCard(
                    BuscarrApiFactory.NirvanaId,
                    "Nirvana",
                    "90s US grunge band",
                    "United States",
                    BuscarrApiFactory.NirvanaImage),
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
    public async Task GetArtist_ReturnsTheArtist()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artist = await client.GetFromJsonAsync<ArtistCard>($"/api/artists/{BuscarrApiFactory.NirvanaId}");

        Assert.Equal(
            new ArtistCard(BuscarrApiFactory.NirvanaId, "Nirvana", null, null, BuscarrApiFactory.NirvanaImage),
            artist);
    }

    [Fact]
    public async Task GetArtist_WhenFanartTvFails_ReturnsTheArtistWithoutImage()
    {
        await using var fanartTvDown = new BuscarrApiFactory { FanartTvDown = true };
        var client = await fanartTvDown.CreateLoggedInClientAsync();

        var artist = await client.GetFromJsonAsync<ArtistCard>($"/api/artists/{BuscarrApiFactory.NirvanaId}");

        Assert.Equal("Nirvana", artist!.Name);
        Assert.Null(artist.Image);
    }

    [Fact]
    public async Task GetArtist_WithInvalidId_Returns404()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/artists/not-an-mbid!");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetArtistReleases_ReturnsReleasesOfEveryTypeMarkedAsLive()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var releases = await client.GetFromJsonAsync<List<ReleaseItem>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(
            [
                new ReleaseItem("Smells Like Teen Spirit", "1991-09-10", false, "single"),
                new ReleaseItem("Nevermind", "1991-09-24", false, "album"),
                new ReleaseItem("MTV Unplugged in New York", "1994-11-01", true, "album"),
            ],
            releases);
    }

    [Fact]
    public async Task GetArtistReleases_DoesNotLookUpTheArtist()
    {
        await using var api = new BuscarrApiFactory();
        var client = await api.CreateLoggedInClientAsync();

        await client.GetFromJsonAsync<List<ReleaseItem>>($"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(["/ws/2/release"], api.MusicBrainz.Requests.Select(request => request.Uri.AbsolutePath));
    }

    [Fact]
    public async Task GetArtistReleases_WithInvalidId_Returns404()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/artists/not-an-mbid!/releases");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
