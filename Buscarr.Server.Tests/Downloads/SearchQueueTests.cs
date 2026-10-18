using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.SlskdJson;

namespace Buscarr.Server.Tests.Downloads;

public sealed class SearchQueueTests : IDisposable
{
    private const string Nevermind = @"@@musica\Nirvana\1991 - Nevermind";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;
    private readonly FakeSlskd _slskd = new();
    private readonly FakeTimeProvider _time = new();
    private readonly SearchQueue _queue;

    public SearchQueueTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        var client = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        _queue = new SearchQueue(_db, new AlbumSearch(client, _time), _time);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessNextAsync_ReturnsNullWhenNoRequestIsDue()
    {
        var request = await AddRequestAsync(nextAttemptIn: TimeSpan.FromDays(1));

        var searched = await ProcessAsync();

        Assert.Null(searched);
        Assert.Equal(DownloadState.Waiting, request.State);
        Assert.Empty(_slskd.SearchTexts);
    }

    [Fact]
    public async Task ProcessNextAsync_FindsTheFoldersAndMarksTheRequestDownloading()
    {
        var request = await AddRequestAsync();
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var searched = await ProcessAsync();

        Assert.Equal(request.Id, searched!.RequestId);
        Assert.Equal("fulano", Assert.Single(searched.Folders).Username);
        Assert.Equal((DownloadState.Downloading, null, 0), (request.State, request.Failure, request.Attempts));
    }

    [Fact]
    public async Task ProcessNextAsync_SpreadsTheRetriesOfANotFoundAlbumOverDays()
    {
        var request = await AddRequestAsync();
        var before = _time.GetUtcNow();

        await ProcessAsync();

        Assert.Equal((DownloadState.NotFound, "nothing-found", 1), (request.State, request.Failure, request.Attempts));
        AssertScheduledIn(request, TimeSpan.FromDays(1), before);

        _time.Advance(TimeSpan.FromDays(1));
        before = _time.GetUtcNow();
        await ProcessAsync();

        Assert.Equal(2, request.Attempts);
        AssertScheduledIn(request, TimeSpan.FromDays(2), before);
    }

    [Fact]
    public async Task ProcessNextAsync_SkipsTheFoldersOfSourcesThatFailedBefore()
    {
        var request = await AddRequestAsync();
        await AddFailedSourceAsync(request.Id, "fulano", Nevermind);
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var searched = await ProcessAsync();

        Assert.Empty(searched!.Folders);
        Assert.Equal((DownloadState.NotFound, "no-matching-folder", 1), (request.State, request.Failure, request.Attempts));
    }

    [Fact]
    public async Task ProcessNextAsync_StillUsesTheOtherSourcesWhenOneFailedBefore()
    {
        var request = await AddRequestAsync();
        await AddFailedSourceAsync(request.Id, "fulano", Nevermind);
        _slskd.Answer(
            "Nirvana Nevermind",
            Response("fulano", AlbumFiles(Nevermind, 12)),
            Response("beltrano", AlbumFiles(Nevermind, 12)));

        var searched = await ProcessAsync();

        Assert.Equal("beltrano", Assert.Single(searched!.Folders).Username);
        Assert.Equal(DownloadState.Downloading, request.State);
    }

    [Fact]
    public async Task ProcessNextAsync_MarksNotFoundWhenNoFolderFits()
    {
        var request = await AddRequestAsync();
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12, bitRate: 128)));

        var searched = await ProcessAsync();

        Assert.Empty(searched!.Folders);
        Assert.Equal((DownloadState.NotFound, "no-matching-folder", 1), (request.State, request.Failure, request.Attempts));
    }

    [Fact]
    public async Task ProcessNextAsync_TellsBusyApartFromNotFound()
    {
        var request = await AddRequestAsync();
        _slskd.BusyFor(3);
        var before = _time.GetUtcNow();

        await ProcessAsync();

        Assert.Equal((DownloadState.Waiting, null, 0), (request.State, request.Failure, request.Attempts));
        AssertScheduledIn(request, TimeSpan.FromMinutes(5), before);
    }

    [Fact]
    public async Task ProcessNextAsync_TellsAnUnfinishedSearchApartFromNotFound()
    {
        var request = await AddRequestAsync();
        _slskd.States = ["InProgress"];
        var before = _time.GetUtcNow();

        await ProcessAsync();

        Assert.Equal((DownloadState.Waiting, null, 0), (request.State, request.Failure, request.Attempts));
        AssertScheduledIn(request, TimeSpan.FromHours(1), before);
    }

    [Fact]
    public async Task ProcessNextAsync_PicksTheRequestDueLongestAgo()
    {
        await AddRequestAsync(releaseGroupId: "g-depois", nextAttemptIn: TimeSpan.FromDays(-1));
        var first = await AddRequestAsync(releaseGroupId: "g-antes", nextAttemptIn: TimeSpan.FromDays(-2));

        var searched = await ProcessAsync();

        Assert.Equal(first.Id, searched!.RequestId);
    }

    [Fact]
    public async Task ProcessNextAsync_PutsTheRequestBackWhenSlskdCannotBeReached()
    {
        var client = new SlskdClient(
            new HttpClient(new FakeHttpMessageHandler(_ => throw new HttpRequestException("fora do ar"))),
            "http://slskd.local:5030",
            "chave-de-teste");
        var queue = new SearchQueue(_db, new AlbumSearch(client, _time), _time);
        var request = await AddRequestAsync();

        var searched = await queue.ProcessNextAsync();

        Assert.Empty(searched!.Folders);
        Assert.Equal((DownloadState.Waiting, 0), (request.State, request.Attempts));
        Assert.Equal(_time.GetUtcNow().AddMinutes(5), request.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessNextAsync_DiscardsTheOutcomeOfARequestCancelledDuringTheSearch()
    {
        var request = await AddRequestAsync();
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var process = _queue.ProcessNextAsync();

        while (await StateAsync(request.Id) != DownloadState.Searching)
        {
            await Task.Delay(1);
        }

        await SetStateAsync(request.Id, DownloadState.Cancelled);

        while (!process.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        var searched = await process;

        Assert.Empty(searched!.Folders);
        Assert.Equal(DownloadState.Cancelled, await StateAsync(request.Id));
    }

    [Fact]
    public async Task RecoverAsync_PutsInFlightRequestsBackInTheQueue()
    {
        var searching = await AddRequestAsync(releaseGroupId: "g1", state: DownloadState.Searching, nextAttemptIn: TimeSpan.FromDays(1));
        var downloading = await AddRequestAsync(releaseGroupId: "g2", state: DownloadState.Downloading, nextAttemptIn: TimeSpan.FromDays(1));
        var downloaded = await AddRequestAsync(releaseGroupId: "g3", state: DownloadState.Downloaded);
        var importing = await AddRequestAsync(releaseGroupId: "g4", state: DownloadState.Importing);

        await _queue.RecoverAsync();

        Assert.Equal(DownloadState.Waiting, searching.State);
        Assert.Equal(DownloadState.Waiting, downloading.State);
        Assert.Equal(_time.GetUtcNow(), searching.NextAttemptAt);
        Assert.Equal(_time.GetUtcNow(), downloading.NextAttemptAt);
        Assert.Equal(DownloadState.Downloaded, downloaded.State);
        Assert.Equal(DownloadState.Downloaded, importing.State);
    }

    private async Task<DownloadRequest> AddRequestAsync(
        string releaseGroupId = "g1",
        DownloadState state = DownloadState.Waiting,
        TimeSpan nextAttemptIn = default)
    {
        var request = DownloadRequestTests.Request(releaseGroupId);
        request.State = state;
        request.NextAttemptAt = _time.GetUtcNow() + nextAttemptIn;
        _db.DownloadRequests.Add(request);
        await _db.SaveChangesAsync();

        return request;
    }

    private async Task AddFailedSourceAsync(int requestId, string username, string directory)
    {
        _db.FailedSources.Add(new FailedSource { RequestId = requestId, Username = username, Directory = directory });
        await _db.SaveChangesAsync();
    }

    private async Task<DownloadState> StateAsync(int id)
    {
        await using var db = NewContext();

        return await db.DownloadRequests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.State)
            .SingleAsync();
    }

    private async Task SetStateAsync(int id, DownloadState state)
    {
        await using var db = NewContext();
        await db.DownloadRequests
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.State, state));
    }

    private BuscarrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);

    private void AssertScheduledIn(DownloadRequest request, TimeSpan delay, DateTimeOffset before) =>
        Assert.InRange(request.NextAttemptAt, before + delay, _time.GetUtcNow() + delay);

    private async Task<SearchedRequest?> ProcessAsync()
    {
        var process = _queue.ProcessNextAsync();

        while (!process.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        return await process;
    }
}
