using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Import;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.MusicBrainzJson;

namespace Buscarr.Server.Tests.Import;

public sealed class ImportPipelineTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"buscarr-pipeline-{Guid.NewGuid()}");

    private readonly string _library;
    private readonly string _downloads;
    private readonly string _rejected;
    private readonly string _databasePath;
    private readonly ServiceProvider _provider;
    private readonly FakeSlskd _slskd = new();
    private readonly FakeAudioTools _tools = new();
    private readonly FakeTimeProvider _time = new();
    private readonly DownloadRunner _runner;

    public ImportPipelineTests()
    {
        _library = Path.Combine(_root, "musicas");
        _downloads = Path.Combine(_root, "downloads", "complete");
        _rejected = Path.Combine(_root, "downloads", "rejected");
        _databasePath = Path.Combine(_root, "buscarr.db");
        Directory.CreateDirectory(_library);
        Directory.CreateDirectory(_downloads);

        var services = new ServiceCollection();
        services.AddDbContext<BuscarrDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<BuscarrDbContext>().Database.Migrate();
        }

        var slskd = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        var musicBrainz = new MusicBrainzClient(
            new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(ReleaseWithTracks(
                "r1",
                "a1",
                "g1",
                LookupMedium(
                    1,
                    LookupTrack(1, "Smells Like Teen Spirit", null, "rec1"),
                    LookupTrack(2, "In Bloom", null, "rec2"),
                    LookupTrack(3, "Territorial Pissings", null, "rec3")))))),
            _time);
        var pipeline = new ImportPipeline(
            musicBrainz,
            _tools,
            new ImportOptions(_library, _downloads, null));
        _runner = new DownloadRunner(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new AlbumDownload(slskd, _time),
            _time,
            pipeline,
            new Notifier(
                new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(System.Net.HttpStatusCode.OK))),
                (string?)null));
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, true);
    }

    [Fact]
    public void Enabled_FollowsTheCurrentSettings()
    {
        var options = new ImportOptions(null, null, null);
        var pipeline = new ImportPipeline(
            new MusicBrainzClient(new HttpClient(), _time),
            new FakeAudioTools(),
            () => options);

        Assert.False(pipeline.Enabled);

        options = new ImportOptions(_library, _downloads, null);

        Assert.True(pipeline.Enabled);
    }

    [Fact]
    public async Task Start_VerifiesImportsAndMarksTheRequestImported()
    {
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");
        _tools.GoodMp3(Path.Combine(local, "01.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "02.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "03.mp3"), 0.1);

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Imported);

        var album = Path.Combine(_library, "Nirvana", "Nevermind");
        Assert.Equal(
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3", "03 Territorial Pissings.mp3"],
            Directory.GetFiles(album).Select(Path.GetFileName).Order());
        Assert.False(Directory.Exists(local));
        Assert.Null((await RequestAsync(request.Id)).Failure);

        using var tagged = TagLib.File.Create(Path.Combine(album, "02 In Bloom.mp3"));
        Assert.Equal(("In Bloom", "r1", "rec2"), (
            tagged.Tag.Title,
            tagged.Tag.MusicBrainzReleaseId,
            tagged.Tag.MusicBrainzTrackId));
    }

    [Fact]
    public async Task Start_QuarantinesARejectedFolderAndFailsTheRequest()
    {
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.Probe(file, new AudioProbe("mp3", 320_000, 44_100, null, 0.1));
            _tools.Levels(file, new SpectralLevels(-20, -80, -95, -100));
        }

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        var failed = await RequestAsync(request.Id);
        Assert.Equal(("fake-audio", 1), (failed.Failure, failed.Attempts));
        Assert.False(Directory.Exists(local));
        var moved = Assert.Single(Directory.GetDirectories(_rejected));
        Assert.Equal(3, Directory.GetFiles(moved).Length);
        var reason = Assert.Single(Directory.GetFiles(_rejected));
        Assert.Contains("fake-audio", File.ReadAllText(reason));
        Assert.Empty(Directory.GetDirectories(_library));
    }

    [Fact]
    public async Task Start_FailsWhenTheTranscodeFails()
    {
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.flac", ".flac");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.GoodFlac(file, 0.1);
        }

        _tools.TranscodeFails = true;

        _runner.Start(request.Id, [Folder(true)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("transcode-failed", (await RequestAsync(request.Id)).Failure);
    }

    private async Task<DownloadRequest> AddRequestAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var request = DownloadRequestTests.Request("g1", trackCount: 3);
        request.State = DownloadState.Downloading;
        db.DownloadRequests.Add(request);
        await db.SaveChangesAsync();

        return request;
    }

    private string StageLocalFolder(string fixture, string extension)
    {
        var local = Path.Combine(_downloads, "Nevermind");
        Directory.CreateDirectory(local);

        foreach (var track in new[] { "01", "02", "03" })
        {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Import", "fixtures", fixture),
                Path.Combine(local, track + extension));
        }

        return local;
    }

    private static SoulseekFolder Folder(bool needsTranscode)
    {
        var extension = needsTranscode ? ".flac" : ".mp3";

        return new(
            "fulano",
            @"@@fulano\Nirvana\Nevermind",
            [.. new[] { "01", "02", "03" }.Select(track =>
                new SlskdFile($@"@@fulano\Nirvana\Nevermind\{track}{extension}", 1_000, 320, null, 200))],
            needsTranscode);
    }

    private async Task<DownloadRequest> RequestAsync(int id)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

        return await db.DownloadRequests.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 1000; i++)
        {
            if (await condition())
            {
                return;
            }

            _time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1);
        }

        Assert.Fail("a condição esperada não aconteceu");
    }
}
