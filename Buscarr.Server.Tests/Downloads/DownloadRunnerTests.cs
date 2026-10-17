using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Downloads;

public sealed class DownloadRunnerTests : IDisposable
{
    private const string Succeeded = "Completed, Succeeded";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"buscarr-runner-{Guid.NewGuid()}.db");

    private readonly ServiceProvider _provider;
    private readonly FakeSlskd _slskd = new();
    private readonly FakeTimeProvider _time = new();
    private readonly DownloadRunner _runner;

    public DownloadRunnerTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<BuscarrDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<BuscarrDbContext>().Database.Migrate();
        }

        var client = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        _runner = new DownloadRunner(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new AlbumDownload(client, _time),
            _time);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task Start_DownloadsTheFolderAndMarksTheRequestDownloaded()
    {
        var request = await AddRequestAsync("g1");
        _slskd.Peer("fulano", (poll, _) => poll < 3 ? new("InProgress", 500) : new(Succeeded));

        _runner.Start(request.Id, [Folder("fulano")]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Downloaded);

        var done = await RequestAsync(request.Id);
        Assert.Equal(("fulano", Folder("fulano").Directory, 3), (done.Username, done.Directory, done.TracksDone));
        Assert.Equal((3_000L, 3_000L, (int?)null, null), (
            done.BytesTransferred,
            done.BytesTotal,
            done.PlaceInQueue,
            done.Failure));
    }

    [Fact]
    public async Task Start_RecordsTheProgressWhileTheDownloadRuns()
    {
        var request = await AddRequestAsync("g1");
        _slskd.Peer("fulano", (_, file) => file.EndsWith("01.mp3") ? new(Succeeded) : new("InProgress", 250));

        _runner.Start(request.Id, [Folder("fulano")]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).TracksDone == 1);

        var downloading = await RequestAsync(request.Id);
        Assert.Equal(DownloadState.Downloading, downloading.State);
        Assert.Equal(("fulano", 1_500L, 3_000L), (
            downloading.Username,
            downloading.BytesTransferred,
            downloading.BytesTotal));
    }

    [Fact]
    public async Task Cancel_StopsTheTransfersAndMarksTheRequestCancelled()
    {
        var request = await AddRequestAsync("g1");
        _slskd.Peer("fulano", (_, _) => new("InProgress", 100));

        _runner.Start(request.Id, [Folder("fulano")]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).Username == "fulano");

        _runner.Cancel(request.Id);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Cancelled);

        Assert.NotEmpty(_slskd.Cancelled);
    }

    [Fact]
    public async Task Start_FailsWithTheLastFoldersReasonAndSchedulesARetry()
    {
        var request = await AddRequestAsync("g1");
        _slskd.RefuseDownloadsFrom("fulano");
        _slskd.Peer("beltrano", (_, _) => new("Completed, Errored"));

        _runner.Start(request.Id, [Folder("fulano"), Folder("beltrano")]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Failed);

        var failed = await RequestAsync(request.Id);
        Assert.Equal(("transfer-failed", 1), (failed.Failure, failed.Attempts));
        Assert.Equal(_time.GetUtcNow().AddDays(1), failed.NextAttemptAt, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Start_PutsTheRequestBackWhenSlskdCannotBeReached()
    {
        var client = new SlskdClient(
            new HttpClient(new FakeHttpMessageHandler(_ => throw new HttpRequestException("fora do ar"))),
            "http://slskd.local:5030",
            "chave-de-teste");
        var runner = new DownloadRunner(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new AlbumDownload(client, _time),
            _time);
        var request = await AddRequestAsync("g1");

        runner.Start(request.Id, [Folder("fulano")]);
        await WaitUntilAsync(async () => (await RequestAsync(request.Id)).State == DownloadState.Waiting);

        var waiting = await RequestAsync(request.Id);
        Assert.Equal(0, waiting.Attempts);
        Assert.Equal(_time.GetUtcNow().AddMinutes(5), waiting.NextAttemptAt, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Start_RunsAtMostThreeAlbumsAtOnce()
    {
        var requests = new List<DownloadRequest>();

        foreach (var (group, username) in new[] { ("g1", "a"), ("g2", "b"), ("g3", "c"), ("g4", "d") })
        {
            requests.Add(await AddRequestAsync(group));
            _slskd.Peer(username, (poll, _) => poll < 10 ? new("InProgress", poll) : new(Succeeded));
        }

        _runner.Start(requests[0].Id, [Folder("a")]);
        _runner.Start(requests[1].Id, [Folder("b")]);
        _runner.Start(requests[2].Id, [Folder("c")]);
        _runner.Start(requests[3].Id, [Folder("d")]);

        Assert.Equal(["a", "b", "c"], _slskd.Enqueued.Select(file => file.Username).Distinct());

        await WaitUntilAsync(async () => (await RequestAsync(requests[3].Id)).State == DownloadState.Downloaded);

        foreach (var request in requests)
        {
            Assert.Equal(DownloadState.Downloaded, (await RequestAsync(request.Id)).State);
        }
    }

    private async Task<DownloadRequest> AddRequestAsync(string releaseGroupId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var request = DownloadRequestTests.Request(releaseGroupId, trackCount: 3);
        request.State = DownloadState.Downloading;
        db.DownloadRequests.Add(request);
        await db.SaveChangesAsync();

        return request;
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

    private static SoulseekFolder Folder(string username) =>
        new(
            username,
            $@"@@{username}\Nirvana\Nevermind",
            [.. Enumerable.Range(1, 3).Select(track =>
                new SlskdFile($@"@@{username}\Nirvana\Nevermind\{track:00}.mp3", 1_000, 320, null, 200))],
            false);
}
