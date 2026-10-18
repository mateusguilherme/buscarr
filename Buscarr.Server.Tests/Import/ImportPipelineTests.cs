using System.Net;
using Buscarr.Server.Data;
using Buscarr.Server.Deezer;
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

    private static readonly byte[] CoverBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x01];

    private readonly string _library;
    private readonly string _downloads;
    private readonly string _rejected;
    private readonly ServiceProvider _provider;
    private readonly FakeSlskd _slskd = new();
    private readonly FakeAudioTools _tools = new();
    private readonly FakeTimeProvider _time = new();
    private readonly DownloadRunner _runner;

    private readonly FakeHttpMessageHandler _covers =
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CoverBytes) });

    private readonly FakeHttpMessageHandler _musicBrainz;
    private readonly FakeHttpMessageHandler _deezer;

    private ImportOptions _options = new(null, null, null);

    public ImportPipelineTests()
    {
        _library = Path.Combine(_root, "musicas");
        _downloads = Path.Combine(_root, "downloads", "complete");
        _rejected = Path.Combine(_root, "downloads", "rejected");
        var databasePath = Path.Combine(_root, "buscarr.db");
        Directory.CreateDirectory(_library);
        Directory.CreateDirectory(_downloads);

        var services = new ServiceCollection();
        services.AddDbContext<BuscarrDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<BuscarrDbContext>().Database.Migrate();
        }

        var slskd = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        _musicBrainz = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(ReleaseWithTracks(
            "r1",
            "a1",
            "g1",
            LookupMedium(
                1,
                LookupTrack(1, "Smells Like Teen Spirit", null, "rec1"),
                LookupTrack(2, "In Bloom", null, "rec2"),
                LookupTrack(3, "Territorial Pissings", null, "rec3")))));
        _deezer = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums()));
        var musicBrainz = new MusicBrainzClient(new HttpClient(_musicBrainz), _time);
        var pipeline = new ImportPipeline(
            musicBrainz,
            _tools,
            new CoverArt(new HttpClient(_covers)),
            new DeezerClient(new HttpClient(_deezer)),
            new ImportOptions(_library, _downloads, null));
        _runner = new DownloadRunner(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new AlbumDownload(slskd, _time),
            _time,
            pipeline);
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
        var pipeline = new ImportPipeline(
            new MusicBrainzClient(new HttpClient(), _time),
            new FakeAudioTools(),
            new CoverArt(new HttpClient()),
            new DeezerClient(new HttpClient()),
            () => _options);

        Assert.False(pipeline.Enabled);

        _options = new ImportOptions(_library, _downloads, null);

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
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3", "03 Territorial Pissings.mp3", "cover.jpg"],
            Directory.GetFiles(album).Select(Path.GetFileName).Order());
        Assert.Equal(CoverBytes, await File.ReadAllBytesAsync(Path.Combine(album, "cover.jpg")));
        Assert.False(Directory.Exists(local));
        Assert.Null((await RequestAsync(request.Id)).Failure);

        using var tagged = TagLib.File.Create(Path.Combine(album, "02 In Bloom.mp3"));
        Assert.Equal(("In Bloom", "r1", "rec2"), (
            tagged.Tag.Title,
            tagged.Tag.MusicBrainzReleaseId,
            tagged.Tag.MusicBrainzTrackId));
        Assert.Equal(CoverBytes, Assert.Single(tagged.Tag.Pictures).Data.Data);
    }

    [Fact]
    public async Task Start_ImportsEvenWhenTheCoverDownloadFails()
    {
        _covers.Respond = _ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");
        _tools.GoodMp3(Path.Combine(local, "01.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "02.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "03.mp3"), 0.1);

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Imported);

        var album = Path.Combine(_library, "Nirvana", "Nevermind");
        Assert.DoesNotContain("cover.jpg", Directory.GetFiles(album).Select(Path.GetFileName));
        Assert.Null((await RequestAsync(request.Id)).Failure);
    }

    [Fact]
    public async Task Start_AcceptsDurationsThatOnlyDeezerConfirms()
    {
        _musicBrainz.Respond = _ => FakeHttpMessageHandler.Json(ReleaseWithTracks(
            "r1",
            "a1",
            "g1",
            LookupMedium(
                1,
                LookupTrack(1, "Smells Like Teen Spirit", 301_000, "rec1"),
                LookupTrack(2, "In Bloom", 255_000, "rec2"),
                LookupTrack(3, "Territorial Pissings", 143_000, "rec3"))));
        _deezer.Respond = request => request.RequestUri!.AbsolutePath.StartsWith("/search", StringComparison.Ordinal)
            ? FakeHttpMessageHandler.Json(DeezerJson.Albums(
                ("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/images/cover/abc/250x250.jpg")))
            : FakeHttpMessageHandler.Json(DeezerJson.AlbumTracks(301, 255, 100));
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");
        _tools.GoodMp3(Path.Combine(local, "01.mp3"), 301);
        _tools.GoodMp3(Path.Combine(local, "02.mp3"), 255);
        _tools.GoodMp3(Path.Combine(local, "03.mp3"), 100);

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Imported);

        Assert.Null((await RequestAsync(request.Id)).Failure);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_library, "Nirvana", "Nevermind")));
    }

    [Fact]
    public async Task Start_QuarantinesARejectedFolderAndFailsTheRequest()
    {
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.Probe(file, new AudioProbe("mp3", 320_000, 44_100, null, 0.1));
            _tools.Levels(file, new SpectralLevels(-10, -100, -112, -118));
        }

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        var failed = await RequestAsync(request.Id);
        Assert.Equal(("fake-audio", 1), (failed.Failure, failed.Attempts));
        Assert.False(Directory.Exists(local));
        var moved = Assert.Single(Directory.GetDirectories(_rejected));
        Assert.Equal(3, Directory.GetFiles(moved).Length);
        var reason = Assert.Single(Directory.GetFiles(_rejected));
        Assert.Contains("fake-audio", await File.ReadAllTextAsync(reason));
        Assert.Empty(Directory.GetDirectories(_library));
    }

    [Fact]
    public async Task Start_RecordsTheRejectedFolderForTheRetriesAndTheQuarantine()
    {
        var request = await AddRequestAsync();
        var local = StageLocalFolder("silence.mp3", ".mp3");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.Probe(file, new AudioProbe("mp3", 320_000, 44_100, null, 0.1));
            _tools.Levels(file, new SpectralLevels(-10, -100, -112, -118));
        }

        _runner.Start(request.Id, [Folder(false)]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        var source = Assert.Single(await FailedSourcesAsync());
        Assert.Equal(
            (request.Id, "fulano", @"@@fulano\Nirvana\Nevermind"),
            (source.RequestId, source.Username, source.Directory));
        var quarantined = Assert.Single(await QuarantinedRowsAsync());
        Assert.Equal(
            (request.Id, "Nirvana", "Nevermind", "fake-audio", "fulano"),
            (quarantined.RequestId, quarantined.ArtistName, quarantined.Title, quarantined.Reason, quarantined.Username));
        Assert.True(Directory.Exists(quarantined.Path));
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

    [Fact]
    public async Task StartImport_ImportsARequestThatStayedDownloaded()
    {
        var request = await AddRequestAsync(DownloadState.Downloaded, @"@@fulano\Nirvana\Nevermind");
        var local = StageLocalFolder("silence.mp3", ".mp3");
        _tools.GoodMp3(Path.Combine(local, "01.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "02.mp3"), 0.1);
        _tools.GoodMp3(Path.Combine(local, "03.mp3"), 0.1);

        _runner.StartImport(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Imported);

        var album = Path.Combine(_library, "Nirvana", "Nevermind");
        Assert.Equal(
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3", "03 Territorial Pissings.mp3", "cover.jpg"],
            Directory.GetFiles(album).Select(Path.GetFileName).Order());
        Assert.False(Directory.Exists(local));
        Assert.Null((await RequestAsync(request.Id)).Failure);
    }

    [Fact]
    public async Task StartImport_VerifiesTheAudioAgainAndQuarantinesWhatFails()
    {
        var request = await AddRequestAsync(DownloadState.Downloaded, @"@@fulano\Nirvana\Nevermind");
        var local = StageLocalFolder("silence.mp3", ".mp3");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.Probe(file, new AudioProbe("mp3", 320_000, 44_100, null, 0.1));
            _tools.Levels(file, new SpectralLevels(-10, -100, -112, -118));
        }

        _runner.StartImport(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("fake-audio", (await RequestAsync(request.Id)).Failure);
        Assert.False(Directory.Exists(local));
        Assert.Single(Directory.GetDirectories(_rejected));
        Assert.Empty(Directory.GetDirectories(_library));
    }

    [Fact]
    public async Task StartImport_SeesThatALosslessFolderNeedsTranscoding()
    {
        var request = await AddRequestAsync(DownloadState.Downloaded, @"@@fulano\Nirvana\Nevermind");
        var local = StageLocalFolder("silence.flac", ".flac");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.GoodFlac(file, 0.1);
        }

        _tools.TranscodeFails = true;

        _runner.StartImport(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("transcode-failed", (await RequestAsync(request.Id)).Failure);
        Assert.NotEmpty(_tools.Transcoded);
    }

    [Fact]
    public async Task StartImport_FailsWhenTheDownloadedFolderIsGone()
    {
        var request = await AddRequestAsync(DownloadState.Downloaded, @"@@fulano\Nirvana\Nevermind");

        _runner.StartImport(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("missing-files", (await RequestAsync(request.Id)).Failure);
    }

    [Fact]
    public async Task StartImport_RecordsTheRejectedSourceOfAReimport()
    {
        var request = await AddRequestAsync(DownloadState.Downloaded, @"@@fulano\Nirvana\Nevermind", "fulano");
        var local = StageLocalFolder("silence.mp3", ".mp3");

        foreach (var file in Directory.GetFiles(local))
        {
            _tools.Probe(file, new AudioProbe("mp3", 320_000, 44_100, null, 0.1));
            _tools.Levels(file, new SpectralLevels(-10, -100, -112, -118));
        }

        _runner.StartImport(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        var source = Assert.Single(await FailedSourcesAsync());
        Assert.Equal(("fulano", @"@@fulano\Nirvana\Nevermind"), (source.Username, source.Directory));
        Assert.Single(await QuarantinedRowsAsync());
    }

    [Fact]
    public async Task StartQuarantineImport_ImportsTheFolderWithoutVerifyingAgain()
    {
        var request = await AddRequestAsync(DownloadState.Failed);
        var quarantined = await AddQuarantinedAsync(request.Id, StageQuarantinedFolder("silence.mp3", ".mp3"));

        _runner.StartQuarantineImport(request.Id, quarantined.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Imported);

        var album = Path.Combine(_library, "Nirvana", "Nevermind");
        Assert.Equal(
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3", "03 Territorial Pissings.mp3", "cover.jpg"],
            Directory.GetFiles(album).Select(Path.GetFileName).Order());
        Assert.False(Directory.Exists(quarantined.Path));
        Assert.False(File.Exists($"{quarantined.Path}.reason.txt"));
        Assert.Empty(await QuarantinedRowsAsync());
    }

    [Fact]
    public async Task StartQuarantineImport_FailsWhenTheFolderIsGone()
    {
        var request = await AddRequestAsync(DownloadState.Failed);
        var quarantined = await AddQuarantinedAsync(request.Id, Path.Combine(_rejected, "sumiu"));

        _runner.StartQuarantineImport(request.Id, quarantined.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("missing-files", (await RequestAsync(request.Id)).Failure);
        Assert.Single(await QuarantinedRowsAsync());
    }

    [Fact]
    public async Task StartQuarantineImport_RefusesAFolderWithTheWrongTrackCount()
    {
        var request = await AddRequestAsync(DownloadState.Failed);
        var quarantined = await AddQuarantinedAsync(request.Id, StageQuarantinedFolder("silence.mp3", ".mp3", 2));

        _runner.StartQuarantineImport(request.Id, quarantined.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("wrong-tracks", (await RequestAsync(request.Id)).Failure);
        Assert.True(Directory.Exists(quarantined.Path));
        Assert.Single(await QuarantinedRowsAsync());
    }

    [Fact]
    public async Task StartQuarantineImport_SeesThatALosslessFolderNeedsTranscoding()
    {
        var request = await AddRequestAsync(DownloadState.Failed);
        var quarantined = await AddQuarantinedAsync(request.Id, StageQuarantinedFolder("silence.flac", ".flac"));
        _tools.TranscodeFails = true;

        _runner.StartQuarantineImport(request.Id, quarantined.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        Assert.Equal("transcode-failed", (await RequestAsync(request.Id)).Failure);
        Assert.NotEmpty(_tools.Transcoded);
        Assert.Single(await QuarantinedRowsAsync());
    }

    private async Task<DownloadRequest> AddRequestAsync(
        DownloadState state = DownloadState.Downloading,
        string? directory = null,
        string? username = null)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var request = DownloadRequestTests.Request(trackCount: 3);
        request.State = state;
        request.Directory = directory;
        request.Username = username;
        request.CoverUrl = "https://coverartarchive.org/release/r1/front-250";
        db.DownloadRequests.Add(request);
        await db.SaveChangesAsync();

        return request;
    }

    private async Task<QuarantinedAlbum> AddQuarantinedAsync(int requestId, string path)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var quarantined = new QuarantinedAlbum
        {
            RequestId = requestId,
            ArtistName = "Nirvana",
            Title = "Nevermind",
            Reason = "fake-audio",
            Path = path,
            Username = "fulano",
            At = _time.GetUtcNow(),
        };
        db.QuarantinedAlbums.Add(quarantined);
        await db.SaveChangesAsync();

        return quarantined;
    }

    private async Task<List<FailedSource>> FailedSourcesAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

        return await db.FailedSources.AsNoTracking().ToListAsync();
    }

    private async Task<List<QuarantinedAlbum>> QuarantinedRowsAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

        return await db.QuarantinedAlbums.AsNoTracking().ToListAsync();
    }

    private string StageQuarantinedFolder(string fixture, string extension, int count = 3)
    {
        var target = Path.Combine(_rejected, $"Nevermind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);

        for (var track = 1; track <= count; track++)
        {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Import", "fixtures", fixture),
                Path.Combine(target, $"{track:00}{extension}"));
        }

        File.WriteAllText($"{target}.reason.txt", "Nirvana - Nevermind: fake-audio\n");

        return target;
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
