using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

public class SettingsEndpointsTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    [Fact]
    public async Task GetSettings_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_PrefersTheOriginalEditionByDefault()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(new UserSettings("original"), settings);
    }

    [Theory]
    [InlineData("remastered", 13)]
    [InlineData("deluxe", 40)]
    [InlineData("original", 12)]
    public async Task PutSettings_SavesTheEditionThatReleasesFollow(string edition, int tracks)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings(edition));
        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");
        var releases = await client.GetFromJsonAsync<List<ReleaseItem>>(
            $"/api/artists/{BuscarrApiFactory.NirvanaId}/releases");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new UserSettings(edition), settings);
        Assert.Equal(tracks, releases!.Single(release => release.Title == "Nevermind").Tracks);
    }

    [Fact]
    public async Task PutSettings_KeepsEachUsersOwnPreference()
    {
        var deluxeFan = await factory.CreateLoggedInClientAsync();
        var other = await factory.CreateLoggedInClientAsync();

        await deluxeFan.PutAsJsonAsync("/api/settings", new UserSettings("deluxe"));

        Assert.Equal(new UserSettings("original"), await other.GetFromJsonAsync<UserSettings>("/api/settings"));
    }

    [Theory]
    [InlineData("super-deluxe")]
    [InlineData("")]
    public async Task PutSettings_WithUnknownEdition_Returns400(string edition)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings(edition));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
