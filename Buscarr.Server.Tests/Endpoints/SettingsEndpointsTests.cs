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
    public async Task GetSettings_PrefersTheOriginalEditionInMp3320ByDefault()
    {
        var client = await factory.CreateLoggedInClientAsync();

        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(new UserSettings("original", "mp3-320"), settings);
    }

    [Theory]
    [InlineData("remastered")]
    [InlineData("deluxe")]
    [InlineData("original")]
    public async Task PutSettings_SavesTheEdition(string edition)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings(edition, "mp3-320"));
        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new UserSettings(edition, "mp3-320"), settings);
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("mp3-320")]
    public async Task PutSettings_SavesTheQuality(string quality)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings("deluxe", quality));
        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new UserSettings("deluxe", quality), settings);
    }

    [Fact]
    public async Task PutSettings_KeepsEachUsersOwnPreference()
    {
        var deluxeFan = await factory.CreateLoggedInClientAsync();
        var other = await factory.CreateLoggedInClientAsync();

        await deluxeFan.PutAsJsonAsync("/api/settings", new UserSettings("deluxe", "flac"));

        Assert.Equal(
            new UserSettings("original", "mp3-320"),
            await other.GetFromJsonAsync<UserSettings>("/api/settings"));
    }

    [Theory]
    [InlineData("super-deluxe")]
    [InlineData("")]
    public async Task PutSettings_WithUnknownEdition_Returns400(string edition)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings(edition, "mp3-320"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("mp3-128")]
    [InlineData("FLAC")]
    [InlineData("")]
    public async Task PutSettings_WithUnknownQuality_Returns400AndKeepsTheSettings(string quality)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings("deluxe", quality));
        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new UserSettings("original", "mp3-320"), settings);
    }
}
