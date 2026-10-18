using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Import;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Downloads;

public sealed class ImportQueueTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;
    private readonly ImportQueue _queue;

    public ImportQueueTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        _queue = new ImportQueue(_db, EnabledPipeline());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ClaimNextAsync_ClaimsTheOldestDownloadedRequestAndMarksItImporting()
    {
        var first = await AddRequestAsync("g1");
        await AddRequestAsync("g2");

        var claimed = await _queue.ClaimNextAsync();

        Assert.Equal(first.Id, claimed);
        Assert.Equal(DownloadState.Importing, first.State);
    }

    [Fact]
    public async Task ClaimNextAsync_ReturnsNullWhenThereIsNothingDownloaded()
    {
        await AddRequestAsync("g1", state: DownloadState.Waiting);

        Assert.Null(await _queue.ClaimNextAsync());
    }

    [Fact]
    public async Task ClaimNextAsync_DoesNothingWhileTheImportIsDisabled()
    {
        var request = await AddRequestAsync("g1");
        var queue = new ImportQueue(_db, DownloadRunnerTests.DisabledPipeline());

        Assert.Null(await queue.ClaimNextAsync());
        Assert.Equal(DownloadState.Downloaded, request.State);
    }

    [Fact]
    public async Task ClaimNextAsync_SkipsRequestsWithoutEditionOrFolder()
    {
        var noEdition = await AddRequestAsync("g1");
        noEdition.EditionReleaseId = null;
        await AddRequestAsync("g2", directory: null);
        await _db.SaveChangesAsync();

        Assert.Null(await _queue.ClaimNextAsync());
    }

    private async Task<DownloadRequest> AddRequestAsync(
        string releaseGroupId,
        DownloadState state = DownloadState.Downloaded,
        string? directory = @"@@fulano\Nirvana\Nevermind")
    {
        var request = DownloadRequestTests.Request(releaseGroupId);
        request.State = state;
        request.Directory = directory;
        _db.DownloadRequests.Add(request);
        await _db.SaveChangesAsync();

        return request;
    }

    private static ImportPipeline EnabledPipeline()
    {
        var musicBrainz = new MusicBrainzClient(
            new HttpClient(new FakeHttpMessageHandler(_ => throw new HttpRequestException("não deveria ser chamado"))),
            new FakeTimeProvider());

        return new ImportPipeline(
            musicBrainz,
            new FakeAudioTools(),
            new CoverArt(new HttpClient()),
            new ImportOptions("/music", "/downloads", null));
    }
}
