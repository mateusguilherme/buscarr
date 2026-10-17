using Buscarr.Server.Data;
using Buscarr.Server.Import;
using Buscarr.Server.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Buscarr.Server.Tests.Settings;

public sealed class ServerSettingsStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;

    public ServerSettingsStoreTests()
    {
        _connection.Open();
        _db = NewContext();
        _db.Database.Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task LoadAsync_SeedsTheSettingsFromTheConfigurationOnFirstStart()
    {
        var store = new ServerSettingsStore();

        await store.LoadAsync(_db, Configuration(new()
        {
            ["Slskd:Url"] = "http://slskd:5030",
            ["Slskd:ApiKey"] = "chave",
            ["Slskd:DownloadsPath"] = "/downloads/complete",
            ["Library:Path"] = "/music",
            ["Library:RejectedPath"] = "/rejected",
            ["Subsonic:Url"] = "http://navidrome:4533",
            ["Subsonic:User"] = "fulano",
            ["Subsonic:Password"] = "senha",
            ["Ntfy:Url"] = "https://ntfy.sh/buscarr",
            ["FanartTv:ApiKey"] = "fanart",
        }));

        var expected = new ServerSettings
        {
            SlskdUrl = "http://slskd:5030",
            SlskdApiKey = "chave",
            DownloadsPath = "/downloads/complete",
            LibraryPath = "/music",
            RejectedPath = "/rejected",
            SubsonicUrl = "http://navidrome:4533",
            SubsonicUser = "fulano",
            SubsonicPassword = "senha",
            NtfyUrl = "https://ntfy.sh/buscarr",
            FanartTvApiKey = "fanart",
        };
        Assert.Equal(expected, store.Current);
        Assert.Equal(expected, await NewContext().ServerSettings.SingleAsync());
    }

    [Fact]
    public async Task LoadAsync_TreatsBlankConfigurationAsUnset()
    {
        var store = new ServerSettingsStore();

        await store.LoadAsync(_db, Configuration(new() { ["Slskd:Url"] = "", ["Library:Path"] = "  " }));

        Assert.Equal(new ServerSettings(), store.Current);
    }

    [Fact]
    public async Task LoadAsync_KeepsWhatWasSavedOverTheConfiguration()
    {
        await new ServerSettingsStore().SaveAsync(_db, new ServerSettings { LibraryPath = "/saved" });
        var store = new ServerSettingsStore();

        await store.LoadAsync(NewContext(), Configuration(new() { ["Library:Path"] = "/configured", ["Slskd:Url"] = "http://slskd:5030" }));

        Assert.Equal(new ServerSettings { LibraryPath = "/saved" }, store.Current);
    }

    [Fact]
    public async Task SaveAsync_ChangesTheCurrentSettingsAndKeepsThem()
    {
        var store = new ServerSettingsStore();
        await store.LoadAsync(_db, Configuration(new() { ["Slskd:Url"] = "http://slskd:5030" }));

        await store.SaveAsync(NewContext(), new ServerSettings { SlskdUrl = "http://outro:5030" });
        var reloaded = new ServerSettingsStore();
        await reloaded.LoadAsync(NewContext(), Configuration(new()));

        Assert.Equal("http://outro:5030", store.Current.SlskdUrl);
        Assert.Equal("http://outro:5030", reloaded.Current.SlskdUrl);
    }

    [Fact]
    public async Task SaveAsync_SavesTwiceWithTheSameContext()
    {
        var store = new ServerSettingsStore();
        await store.LoadAsync(_db, Configuration(new()));

        await store.SaveAsync(_db, new ServerSettings { NtfyUrl = "https://ntfy.sh/a" });
        await store.SaveAsync(_db, new ServerSettings { NtfyUrl = "https://ntfy.sh/b" });

        Assert.Equal("https://ntfy.sh/b", (await NewContext().ServerSettings.SingleAsync()).NtfyUrl);
    }

    [Fact]
    public void Import_IsBuiltFromThePaths()
    {
        var settings = new ServerSettings { LibraryPath = "/music", DownloadsPath = "/downloads", RejectedPath = "/rejected" };

        Assert.Equal(new ImportOptions("/music", "/downloads", "/rejected"), settings.Import);
    }

    private BuscarrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
