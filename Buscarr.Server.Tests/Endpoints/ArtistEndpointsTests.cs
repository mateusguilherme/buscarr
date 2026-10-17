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
                new ArtistCard("1", "Approaching Nirvana", null, null),
                new ArtistCard(BuscarrApiFactory.NirvanaId, "Nirvana", "90s US grunge band", "United States"),
            ],
            artists);
    }

    [Fact]
    public async Task SearchArtists_WhenNothingMatches_ReturnsEmptyList()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/artists?q=zzqxjvkwplm");

        Assert.Empty(artists!);
    }

    [Fact]
    public async Task GetArtistReleases_ReturnsArtistNameAndReleasesOfEveryTypeMarkedAsLive()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var result = await client.GetFromJsonAsync<ArtistReleases>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.NotNull(result);
        Assert.Equal("Nirvana", result.Name);
        Assert.Equal(
            [
                new ReleaseItem("Smells Like Teen Spirit", "1991-09-10", false, "single"),
                new ReleaseItem("Nevermind", "1991-09-24", false, "album"),
                new ReleaseItem("MTV Unplugged in New York", "1994-11-01", true, "album"),
            ],
            result.Releases);
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
