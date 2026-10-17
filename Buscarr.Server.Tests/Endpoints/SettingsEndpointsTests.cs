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
    [InlineData("remastered")]
    [InlineData("deluxe")]
    [InlineData("original")]
    public async Task PutSettings_SavesTheEdition(string edition)
    {
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new UserSettings(edition));
        var settings = await client.GetFromJsonAsync<UserSettings>("/api/settings");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new UserSettings(edition), settings);
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
