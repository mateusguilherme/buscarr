using System.Net;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

[UnsupportedOSPlatform("windows")]
public sealed class ServerSettingsEndpointsTests : IClassFixture<BuscarrApiFactory>, IDisposable
{
    private readonly BuscarrApiFactory _factory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"buscarr-settings-{Guid.NewGuid()}");
    private readonly string _downloads;
    private readonly string _library;

    public ServerSettingsEndpointsTests(BuscarrApiFactory factory)
    {
        _factory = factory;
        _downloads = Directory.CreateDirectory(Path.Combine(_root, "downloads")).FullName;
        _library = Directory.CreateDirectory(Path.Combine(_root, "music")).FullName;
    }

    public void Dispose()
    {
        foreach (var directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData("/api/server-settings")]
    [InlineData("/api/server-settings/status")]
    public async Task Get_WithoutLogin_Returns401(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/server-settings")]
    [InlineData("/api/server-settings/status")]
    public async Task Get_ForAUserWhoIsNotAdmin_Returns403(string path)
    {
        var client = await _factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_ForAUserWhoIsNotAdmin_Returns403AndKeepsTheSettings()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        await admin.PutAsJsonAsync("/api/server-settings", Update() with { SubsonicUser = "antes" });
        var other = await _factory.CreateLoggedInClientAsync();

        var response = await other.PutAsJsonAsync("/api/server-settings", Update() with { SubsonicUser = "depois" });
        var settings = await admin.GetFromJsonAsync<ServerSettingsView>("/api/server-settings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("antes", settings!.SubsonicUser);
    }

    [Fact]
    public async Task Put_SavesTheSettingsAndGetShowsThemWithoutTheSecrets()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);

        var response = await admin.PutAsJsonAsync("/api/server-settings", Update());
        var body = await admin.GetStringAsync("/api/server-settings");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            new ServerSettingsView(
                "http://slskd:5030",
                true,
                _downloads,
                _library,
                "",
                "http://navidrome:4533",
                "fulano",
                true,
                true),
            JsonSerializer.Deserialize<ServerSettingsView>(body, JsonSerializerOptions.Web));
        Assert.DoesNotContain("chave-slskd", body);
        Assert.DoesNotContain("senha-navidrome", body);
        Assert.DoesNotContain("chave-fanart", body);
    }

    [Fact]
    public async Task Put_KeepsASecretSentAsNullAndClearsOneSentEmpty()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        await admin.PutAsJsonAsync("/api/server-settings", Update());

        await admin.PutAsJsonAsync(
            "/api/server-settings",
            Update() with { SlskdApiKey = null, SubsonicPassword = "", FanartTvApiKey = null });
        var settings = await admin.GetFromJsonAsync<ServerSettingsView>("/api/server-settings");

        Assert.True(settings!.HasSlskdApiKey);
        Assert.False(settings.HasSubsonicPassword);
        Assert.True(settings.HasFanartTvApiKey);
    }

    [Fact]
    public async Task Put_TrimsTheValuesAndClearsTheBlankOnes()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);

        await admin.PutAsJsonAsync(
            "/api/server-settings",
            Update() with { SubsonicUser = "  fulano  ", SubsonicUrl = "   ", RejectedPath = null });
        var settings = await admin.GetFromJsonAsync<ServerSettingsView>("/api/server-settings");

        Assert.Equal("fulano", settings!.SubsonicUser);
        Assert.Equal("", settings.SubsonicUrl);
        Assert.Equal("", settings.RejectedPath);
    }

    [Theory]
    [InlineData("slskdUrl")]
    [InlineData("subsonicUrl")]
    public async Task Put_RefusesAnAddressThatIsNotHttp(string field)
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        var update = field switch
        {
            "slskdUrl" => Update() with { SlskdUrl = "slskd:5030" },
            _ => Update() with { SubsonicUrl = "ftp://navidrome" },
        };

        var errors = await PutExpectingErrorsAsync(admin, update);

        Assert.Equal(new Dictionary<string, string> { [field] = "invalid-url" }, errors);
    }

    [Fact]
    public async Task Put_RefusesPathsThatAreRelativeMissingOrReadOnly()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        var readOnly = Directory.CreateDirectory(Path.Combine(_root, "somente-leitura")).FullName;
        File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var errors = await PutExpectingErrorsAsync(admin, Update() with
        {
            DownloadsPath = "downloads",
            LibraryPath = Path.Combine(_root, "nao-existe"),
            RejectedPath = readOnly,
        });

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["downloadsPath"] = "not-absolute",
                ["libraryPath"] = "not-found",
                ["rejectedPath"] = "not-writable",
            },
            errors);
    }

    [Fact]
    public async Task Put_AcceptsARejectedFolderThatDoesNotExistYet()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);

        var response = await admin.PutAsJsonAsync(
            "/api/server-settings",
            Update() with { RejectedPath = Path.Combine(_root, "rejected") });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Put_NeedsTheDownloadsFolderWhenThereIsALibrary()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);

        var errors = await PutExpectingErrorsAsync(admin, Update() with { DownloadsPath = "" });

        Assert.Equal(new Dictionary<string, string> { ["downloadsPath"] = "required" }, errors);
    }

    [Fact]
    public async Task Put_WithErrorsKeepsTheSettings()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        await admin.PutAsJsonAsync("/api/server-settings", Update());

        await admin.PutAsJsonAsync("/api/server-settings", Update() with { SubsonicUser = "outro", SlskdUrl = "ruim" });
        var settings = await admin.GetFromJsonAsync<ServerSettingsView>("/api/server-settings");

        Assert.Equal("fulano", settings!.SubsonicUser);
    }

    [Fact]
    public async Task Status_TellsHowEachConnectionIsDoing()
    {
        var admin = await _factory.CreateLoggedInClientAsync(admin: true);
        await admin.PutAsJsonAsync("/api/server-settings", Update() with { SubsonicUrl = "" });

        var notConfigured = await admin.GetFromJsonAsync<ServerStatus>("/api/server-settings/status");
        await admin.PutAsJsonAsync("/api/server-settings", Update());
        var configured = await admin.GetFromJsonAsync<ServerStatus>("/api/server-settings/status");

        Assert.Equal(new ConnectionStatus("ok", null), notConfigured!.Slskd);
        Assert.Equal(new ConnectionStatus("not-configured", null), notConfigured.Subsonic);
        Assert.Equal(new ConnectionStatus("ok", null), configured!.Subsonic);
        Assert.Equal(new ConnectionStatus("ok", null), configured.FanartTv);
    }

    [Fact]
    public async Task Status_TellsWhenTheFanartTvKeyIsInvalid()
    {
        using var factory = new BuscarrApiFactory();
        factory.FanartTv.Respond = _ => FakeHttpMessageHandler.Status(HttpStatusCode.Unauthorized);
        var admin = await factory.CreateLoggedInClientAsync(admin: true);

        var status = await admin.GetFromJsonAsync<ServerStatus>("/api/server-settings/status");

        Assert.Equal(new ConnectionStatus("error", "chave inválida"), status!.FanartTv);
    }

    [Fact]
    public async Task Status_TellsWhenSlskdIsOfflineFromSoulseek()
    {
        using var factory = new BuscarrApiFactory();
        factory.Slskd.Connected = false;
        var admin = await factory.CreateLoggedInClientAsync(admin: true);

        var status = await admin.GetFromJsonAsync<ServerStatus>("/api/server-settings/status");

        Assert.Equal(new ConnectionStatus("offline", null), status!.Slskd);
    }

    [Fact]
    public async Task Status_CarriesTheErrorOfAConnectionThatFails()
    {
        using var factory = new BuscarrApiFactory { SlskdDown = true };
        factory.Subsonic.Respond = _ => FakeHttpMessageHandler.Json(SubsonicJson.Failed("Wrong username or password"));
        var admin = await factory.CreateLoggedInClientAsync(admin: true);
        await admin.PutAsJsonAsync("/api/server-settings", Update());

        var status = await admin.GetFromJsonAsync<ServerStatus>("/api/server-settings/status");

        Assert.Equal(new ConnectionStatus("error", "fora do ar"), status!.Slskd);
        Assert.Equal("error", status.Subsonic.State);
        Assert.Contains("Wrong username or password", status.Subsonic.Message);
    }

    private ServerSettingsUpdate Update() =>
        new(
            "http://slskd:5030",
            "chave-slskd",
            _downloads,
            _library,
            null,
            "http://navidrome:4533",
            "fulano",
            "senha-navidrome",
            "chave-fanart");

    private static async Task<Dictionary<string, string>> PutExpectingErrorsAsync(HttpClient client, ServerSettingsUpdate update)
    {
        var response = await client.PutAsJsonAsync("/api/server-settings", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ServerSettingsErrors>())!.Errors;
    }
}
