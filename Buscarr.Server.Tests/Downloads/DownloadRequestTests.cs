using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Slskd;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Tests.Downloads;

public sealed class DownloadRequestTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;

    public DownloadRequestTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Requests_AreUniqueByReleaseGroupTrackCountAndQuality()
    {
        _db.DownloadRequests.Add(Request());
        await _db.SaveChangesAsync();

        _db.DownloadRequests.Add(Request());

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Requests_ForAnotherEditionOrQualityLiveTogether()
    {
        _db.DownloadRequests.Add(Request());
        _db.DownloadRequests.Add(Request(trackCount: 40));
        _db.DownloadRequests.Add(Request(quality: Qualities.Flac));

        await _db.SaveChangesAsync();

        Assert.Equal(3, await _db.DownloadRequests.CountAsync());
    }

    public static DownloadRequest Request(
        string releaseGroupId = "g1",
        int trackCount = 12,
        string quality = Qualities.Mp3320) =>
        new()
        {
            ArtistId = "artist-1",
            ArtistName = "Nirvana",
            ReleaseGroupId = releaseGroupId,
            Title = "Nevermind",
            Type = "album",
            FirstReleaseDate = "1991-09-24",
            EditionReleaseId = "r1",
            TrackCount = trackCount,
            Quality = quality,
            NextAttemptAt = DateTimeOffset.UnixEpoch,
        };
}
