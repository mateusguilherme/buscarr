using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

public class ListEndpointsTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    private const string Nirvana = $"/api/list/{BuscarrApiFactory.NirvanaId}";

    private static readonly ListSettings Settings = new(["album", "live"]);

    [Fact]
    public async Task GetList_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/list");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_IsEmptyForANewUser()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Empty(artists!);
    }

    [Fact]
    public async Task PutList_AddsTheArtistAsMusicBrainzAndFanartTvDescribeIt()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await AddNirvanaAsync(client);
        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [new ArtistCard(BuscarrApiFactory.NirvanaId, "Nirvana", null, null, BuscarrApiFactory.NirvanaImage, true)],
            artists);
    }

    [Fact]
    public async Task PutList_Twice_KeepsTheArtistOnce()
    {
        var client = await factory.CreateLoggedInClientAsync();

        await AddNirvanaAsync(client);
        var response = await AddNirvanaAsync(client);
        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(artists!);
    }

    [Fact]
    public async Task DeleteList_RemovesTheArtist()
    {
        var client = await factory.CreateLoggedInClientAsync();
        await AddNirvanaAsync(client);

        var response = await client.DeleteAsync(Nirvana);
        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(artists!);
    }

    [Fact]
    public async Task PutList_KeepsEachUsersOwnList()
    {
        var owner = await factory.CreateLoggedInClientAsync();
        var other = await factory.CreateLoggedInClientAsync();

        await AddNirvanaAsync(owner);

        Assert.Empty((await other.GetFromJsonAsync<List<ArtistCard>>("/api/list"))!);
    }

    [Fact]
    public async Task PutList_WithInvalidId_Returns404()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/list/not-an-mbid!", Settings);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutList_WhenMusicBrainzFails_Returns502AndAddsNothing()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/list/00000000-0000-0000-0000-000000000000", Settings);
        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Empty(artists!);
    }

    [Fact]
    public async Task Search_TellsWhetherTheArtistIsInTheList()
    {
        var client = await factory.CreateLoggedInClientAsync();
        await AddNirvanaAsync(client);

        var found = await client.GetFromJsonAsync<List<ArtistCard>>("/api/artists?q=nirvana");

        Assert.Equal([false, true], found!.Select(card => card.Listed));
    }

    [Fact]
    public async Task GetListEntry_ReturnsTheSettingsTheArtistWasAddedWith()
    {
        var client = await factory.CreateLoggedInClientAsync();
        await AddNirvanaAsync(client);

        var settings = await client.GetFromJsonAsync<ListSettings>(Nirvana);

        Assert.Equal(["album", "live"], settings!.ReleaseTypes);
    }

    [Fact]
    public async Task GetListEntry_ForAnArtistNotInTheList_Returns404()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync(Nirvana);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutList_UpdatesTheSettingsOfAnArtistAlreadyInTheList()
    {
        var client = await factory.CreateLoggedInClientAsync();
        await AddNirvanaAsync(client);

        await client.PutAsJsonAsync(Nirvana, new ListSettings(["single"]));
        var settings = await client.GetFromJsonAsync<ListSettings>(Nirvana);

        Assert.Equal(["single"], settings!.ReleaseTypes);
    }

    public static TheoryData<string[]> InvalidReleaseTypes => new() { Array.Empty<string>(), new[] { "album", "bootleg" } };

    [Theory]
    [MemberData(nameof(InvalidReleaseTypes))]
    public async Task PutList_WithNoOrUnknownReleaseTypes_Returns400(string[] releaseTypes)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync(Nirvana, new ListSettings([.. releaseTypes]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ArtistCard>>("/api/list"))!);
    }

    private static Task<HttpResponseMessage> AddNirvanaAsync(HttpClient client) =>
        client.PutAsJsonAsync(Nirvana, Settings);
}
