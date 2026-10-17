using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

public class ArtistEndpointsTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    [Fact]
    public async Task SearchArtist_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/artists?q=nirvana");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchArtist_ReturnsFirstArtistWhoseNameContainsQuery()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artist = await client.GetFromJsonAsync<ArtistCard>("/api/artists?q=nirvana");

        Assert.Equal(new ArtistCard("1", "Approaching Nirvana"), artist);
    }

    [Fact]
    public async Task SearchArtist_WhenNothingMatches_Returns404()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/artists?q=zzqxjvkwplm");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetArtistReleases_ReturnsArtistNameAndReleasesMarkedAsLive()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var result = await client.GetFromJsonAsync<ArtistReleases>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases?type=album");

        Assert.NotNull(result);
        Assert.Equal("Nirvana", result.Name);
        Assert.Equal(
            [
                new ReleaseItem("Nevermind", "1991-09-24", false),
                new ReleaseItem("MTV Unplugged in New York", "1994-11-01", true),
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
    public async Task GetArtistReleases_WithInvalidType_Returns400()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync($"/api/artists/{BuscarrApiFactory.NirvanaId}/releases?type=bootleg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
