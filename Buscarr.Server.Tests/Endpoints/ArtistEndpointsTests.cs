using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

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
                    false),
                new ArtistRelease(
                    "g2",
                    "Sliver",
                    "single",
                    "1990-09-01",
                    "https://coverartarchive.org/release/r3/front-250",
                    2,
                    false),
            ],
            releases);
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
