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
}
