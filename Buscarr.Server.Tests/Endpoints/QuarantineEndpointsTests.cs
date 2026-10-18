using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Import;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarr.Server.Tests.Endpoints;

public sealed class QuarantineEndpointsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"buscarr-quarentena-{Guid.NewGuid()}");
    private readonly string _downloads;
    private readonly string _library;
    private readonly string _rejected;

    public QuarantineEndpointsTests()
    {
        _downloads = Directory.CreateDirectory(Path.Combine(_root, "downloads")).FullName;
        _library = Directory.CreateDirectory(Path.Combine(_root, "music")).FullName;
        _rejected = Directory.CreateDirectory(Path.Combine(_root, "rejected")).FullName;
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task GetQuarantine_WithoutLogin_Returns401()
    {
        await using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/quarantine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetQuarantine_ListsTheNewestFirst()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await SeedQuarantinedAsync(factory, Quarantined(null, Path.Combine(_rejected, "a")));
        await SeedQuarantinedAsync(
            factory,
            Quarantined(null, Path.Combine(_rejected, "b"), "In Utero", DateTimeOffset.UnixEpoch.AddDays(1)));

        var list = await client.GetFromJsonAsync<List<QuarantineItem>>("/api/quarantine");

        Assert.Equal(["In Utero", "Nevermind"], list!.Select(item => item.Title));
        var item = list![^1];
        Assert.Equal(
            ("Nirvana", "fake-audio", "fulano", DateTimeOffset.UnixEpoch, false),
            (item.ArtistName, item.Reason, item.Username, item.At, item.CanImport));
    }

    [Fact]
    public async Task GetQuarantine_SaysWhichAlbumsCanBeImported()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync(admin: true);
        await ConfigureImportAsync(client);
        var request = await SeedRequestAsync(factory, Request(DownloadState.Failed));
        await SeedQuarantinedAsync(factory, Quarantined(request.Id, StageQuarantinedFolder()));
        await SeedQuarantinedAsync(factory, Quarantined(null, Path.Combine(_rejected, "sumiu"), "In Utero"));

        var list = await client.GetFromJsonAsync<List<QuarantineItem>>("/api/quarantine");

        Assert.Equal([false, true], list!.Select(item => item.CanImport));
    }

    [Fact]
    public async Task ImportQuarantined_OfAMissingRow_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync("/api/quarantine/123/import", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportQuarantined_WithoutTheImportConfigured_Returns409()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedRequestAsync(factory, Request(DownloadState.Failed));
        var quarantined = await SeedQuarantinedAsync(factory, Quarantined(request.Id, StageQuarantinedFolder()));

        var response = await client.PostAsync($"/api/quarantine/{quarantined.Id}/import", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImportQuarantined_WhenTheRequestIsGone_Returns409()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync(admin: true);
        await ConfigureImportAsync(client);
        var quarantined = await SeedQuarantinedAsync(factory, Quarantined(null, StageQuarantinedFolder()));

        var response = await client.PostAsync($"/api/quarantine/{quarantined.Id}/import", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImportQuarantined_WhileTheRequestIsBusy_Returns409()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync(admin: true);
        await ConfigureImportAsync(client);
        var request = await SeedRequestAsync(factory, Request(DownloadState.Downloading));
        var quarantined = await SeedQuarantinedAsync(factory, Quarantined(request.Id, StageQuarantinedFolder()));

        var response = await client.PostAsync($"/api/quarantine/{quarantined.Id}/import", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImportQuarantined_ImportsTheFolderAgain()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync(admin: true);
        await ConfigureImportAsync(client);
        var request = await SeedRequestAsync(factory, Request(DownloadState.Failed));
        var folder = StageQuarantinedFolder();
        var quarantined = await SeedQuarantinedAsync(factory, Quarantined(request.Id, folder));

        var response = await client.PostAsync($"/api/quarantine/{quarantined.Id}/import", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await WaitForStateAsync(client, request.Id, "imported");
        Assert.True(File.Exists(Path.Combine(_library, "Nirvana", "Nevermind", "01 Smells Like Teen Spirit.mp3")));
        Assert.False(Directory.Exists(folder));
        Assert.False(File.Exists($"{folder}.reason.txt"));
        Assert.Empty((await client.GetFromJsonAsync<List<QuarantineItem>>("/api/quarantine"))!);
    }

    [Fact]
    public async Task DiscardQuarantined_OfAMissingRow_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.DeleteAsync("/api/quarantine/123");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiscardQuarantined_RemovesTheFolderAndTheRow()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var folder = StageQuarantinedFolder();
        var quarantined = await SeedQuarantinedAsync(factory, Quarantined(null, folder));

        var response = await client.DeleteAsync($"/api/quarantine/{quarantined.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(folder));
        Assert.False(File.Exists($"{folder}.reason.txt"));
        Assert.Empty((await client.GetFromJsonAsync<List<QuarantineItem>>("/api/quarantine"))!);
    }

    private async Task ConfigureImportAsync(HttpClient admin)
    {
        var response = await admin.PutAsJsonAsync(
            "/api/server-settings",
            new ServerSettingsUpdate(
                "http://slskd.local:5030",
                "chave-de-teste",
                _downloads,
                _library,
                _rejected,
                "",
                "",
                null,
                null,
                "",
                null));
        response.EnsureSuccessStatusCode();
    }

    private string StageQuarantinedFolder()
    {
        var target = Path.Combine(_rejected, $"Nevermind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);

        foreach (var track in new[] { "01", "02", "03" })
        {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Import", "fixtures", "silence.mp3"),
                Path.Combine(target, $"{track}.mp3"));
        }

        File.WriteAllText($"{target}.reason.txt", "Nirvana - Nevermind: fake-audio\n");

        return target;
    }

    private static QuarantinedAlbum Quarantined(
        int? requestId,
        string path,
        string title = "Nevermind",
        DateTimeOffset? at = null) =>
        new()
        {
            RequestId = requestId,
            ArtistName = "Nirvana",
            Title = title,
            Reason = "fake-audio",
            Path = path,
            Username = "fulano",
            At = at ?? DateTimeOffset.UnixEpoch,
        };

    private static DownloadRequest Request(DownloadState state)
    {
        var request = DownloadRequestTests.Request(trackCount: 3);
        request.State = state;

        return request;
    }

    private static async Task<QuarantinedAlbum> SeedQuarantinedAsync(
        BuscarrApiFactory factory,
        QuarantinedAlbum quarantined)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        db.QuarantinedAlbums.Add(quarantined);
        await db.SaveChangesAsync();

        return quarantined;
    }

    private static async Task<DownloadRequest> SeedRequestAsync(BuscarrApiFactory factory, DownloadRequest request)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        db.DownloadRequests.Add(request);
        await db.SaveChangesAsync();

        return request;
    }

    private static async Task WaitForStateAsync(HttpClient client, int id, string state)
    {
        for (var i = 0; i < 1000; i++)
        {
            var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

            if (downloads!.Single(download => download.Id == id).State == state)
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail($"o pedido {id} não chegou ao estado {state}");
    }
}
