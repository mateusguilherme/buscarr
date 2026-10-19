using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

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
            [
                new ArtistCard(
                    BuscarrApiFactory.NirvanaId,
                    "Nirvana",
                    null,
                    null,
                    $"/api/images?url={Uri.EscapeDataString(BuscarrApiFactory.NirvanaImage)}",
                    true),
            ],
            artists);
    }

    [Fact]
    public async Task PutList_FallsBackToTheStreamingPhotoWhenFanartTvHasNone()
    {
        await using var ownFactory = new BuscarrApiFactory { FanartTvDown = true };
        ownFactory.Deezer.Respond = request => request.RequestUri!.AbsolutePath == "/artist/412"
            ? FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile(
                "Nirvana",
                "https://cdn-images.dzcdn.net/images/artist/n1/250x250-000000-80-0-0.jpg"))
            : FakeHttpMessageHandler.Json(DeezerJson.NotFound());
        var client = await ownFactory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync(Nirvana, Settings);
        var artists = await client.GetFromJsonAsync<List<ArtistCard>>("/api/list");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "/api/images?url=https%3A%2F%2Fcdn-images.dzcdn.net%2Fimages%2Fartist%2Fn1%2F250x250-000000-80-0-0.jpg",
            Assert.Single(artists!).Image);
    }

    [Fact]
    public async Task PutAndDeleteList_FireTheRequestGenerator()
    {
        var client = await factory.CreateLoggedInClientAsync();
        var trigger = factory.Services.GetRequiredService<GeneratorTrigger>();
        trigger.Drain();

        await AddNirvanaAsync(client);

        Assert.True(trigger.Pending);
        trigger.Drain();

        await client.DeleteAsync(Nirvana);

        Assert.True(trigger.Pending);
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

    public static TheoryData<string[]> InvalidReleaseTypes => new([], ["album", "bootleg"]);

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
