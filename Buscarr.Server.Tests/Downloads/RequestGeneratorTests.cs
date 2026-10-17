using System.Net;
using System.Web;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Tests.Fakes;
using Buscarr.Server.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.MusicBrainzJson;

namespace Buscarr.Server.Tests.Downloads;

public sealed class RequestGeneratorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;
    private readonly Dictionary<string, string> _releases = [];
    private readonly HashSet<string> _failing = [];
    private readonly List<string> _libraryAlbums = [];
    private bool _libraryFailing;
    private readonly MusicBrainzClient _musicBrainz;
    private readonly RequestGenerator _generator;

    public RequestGeneratorTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        _musicBrainz = new MusicBrainzClient(
            new HttpClient(new FakeHttpMessageHandler(Respond)),
            new FakeTimeProvider(Now) { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });
        var subsonic = new SubsonicClient(new HttpClient(), null, null, null);
        _generator = new RequestGenerator(_db, _musicBrainz, subsonic, new FakeTimeProvider(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GenerateAsync_CreatesAWaitingRequestForEachAlbumOfTheChosenTypes()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album", "single"]);
        _releases["nirvana"] = NirvanaReleases();

        await _generator.GenerateAsync();

        var requests = await _db.DownloadRequests.OrderBy(r => r.ReleaseGroupId).ToListAsync();
        Assert.Equal([("g1", "album"), ("g2", "single")], requests.Select(r => (r.ReleaseGroupId, r.Type)));
        var nevermind = requests[0];
        Assert.Equal(DownloadState.Waiting, nevermind.State);
        Assert.Equal(("Nirvana", "Nevermind", "r1", 12, Qualities.Mp3320), (
            nevermind.ArtistName,
            nevermind.Title,
            nevermind.EditionReleaseId,
            nevermind.TrackCount,
            nevermind.Quality));
        Assert.Equal((0, Now, "1991-09-24"), (nevermind.Attempts, nevermind.NextAttemptAt, nevermind.FirstReleaseDate));
    }

    [Fact]
    public async Task GenerateAsync_PicksTheEditionOfTheUsersPreference()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", edition: Editions.Deluxe, types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.Where(r => r.ReleaseGroupId == "g1").ToListAsync());
        Assert.Equal(("r2", 40), (request.EditionReleaseId, request.TrackCount));
    }

    [Fact]
    public async Task GenerateAsync_DoesNotTouchAnExistingRequest()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _db.DownloadRequests.Add(Request("g1", trackCount: 12, state: DownloadState.Downloaded, attempts: 3));
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal((DownloadState.Downloaded, 3), (request.State, request.Attempts));
    }

    [Fact]
    public async Task GenerateAsync_SharesARequestBetweenUsersWithTheSamePreferences()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        await ListedUserAsync("beltrano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();

        await _generator.GenerateAsync();

        Assert.Single(await _db.DownloadRequests.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_CreatesARequestForEachQuality()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        await ListedUserAsync("beltrano@exemplo.com", "nirvana", quality: Qualities.Flac, types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();

        await _generator.GenerateAsync();

        var requests = await _db.DownloadRequests.OrderBy(r => r.Quality).ToListAsync();
        Assert.Equal([Qualities.Flac, Qualities.Mp3320], requests.Select(r => r.Quality));
    }

    [Fact]
    public async Task GenerateAsync_RemovesRequestsNobodyWantsAnymoreExceptActiveOnes()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _db.DownloadRequests.Add(Request("stale", state: DownloadState.NotFound));
        _db.DownloadRequests.Add(Request("active", state: DownloadState.Downloading));
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var groups = (await _db.DownloadRequests.ToListAsync()).Select(r => r.ReleaseGroupId);
        Assert.Equal(["active", "g1"], groups.Order());
    }

    [Fact]
    public async Task GenerateAsync_KeepsEveryRequestWhenAnArtistSearchFails()
    {
        var user = await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _db.ListedArtists.Add(Listed(user.Id, "mammoth", "album"));
        await _db.SaveChangesAsync();
        _releases["nirvana"] = NirvanaReleases();
        _failing.Add("mammoth");
        _db.DownloadRequests.Add(Request("stale", state: DownloadState.NotFound));
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var groups = (await _db.DownloadRequests.ToListAsync()).Select(r => r.ReleaseGroupId);
        Assert.Equal(["g1", "stale"], groups.Order());
    }

    [Fact]
    public async Task GenerateAsync_SchedulesAFutureReleaseForAfterItsDay()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "2026-11-05", "2026-11-05"), "r1", "g1", front: false), ("CD", 12)));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero), request.NextAttemptAt);
    }

    [Fact]
    public async Task GenerateAsync_SkipsAlbumsWithoutTracks()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(1, Release("Nevermind", "1991-09-24", "1991-09-24"));

        await _generator.GenerateAsync();

        Assert.Empty(await _db.DownloadRequests.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_SkipsAlbumsTheLibraryAlreadyHas()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album", "single"]);
        _releases["nirvana"] = NirvanaReleases();
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 12, "r1"));

        await WithLibrary().GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("g2", request.ReleaseGroupId);
    }

    [Fact]
    public async Task GenerateAsync_SkipsAnAlbumCoveredByTheDeluxeOnTheShelf()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind (Deluxe)", "Nirvana", 40, "r2"));

        await WithLibrary().GenerateAsync();

        Assert.Empty(await _db.DownloadRequests.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_KeepsAskingWhenTheLibraryCopyHasFewerTracks()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 5, "r1"));

        await WithLibrary().GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("g1", request.ReleaseGroupId);
    }

    [Fact]
    public async Task GenerateAsync_FindsAnUntaggedLibraryAlbumByNameAndArtist()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 12));

        await WithLibrary().GenerateAsync();

        Assert.Empty(await _db.DownloadRequests.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_RemovesARequestWhenTheLibraryGainsItsAlbum()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _db.DownloadRequests.Add(Request("g1", trackCount: 12, state: DownloadState.NotFound));
        await _db.SaveChangesAsync();
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 12, "r1"));

        await WithLibrary().GenerateAsync();

        Assert.Empty(await _db.DownloadRequests.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_GeneratesWithoutTheFilterAndRemovesNothingWhenTheLibraryFails()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album", "single"]);
        _releases["nirvana"] = NirvanaReleases();
        _libraryFailing = true;
        _libraryAlbums.Add(SubsonicJson.Album("al-1", "Nevermind", "Nirvana", 12, "r1"));
        _db.DownloadRequests.Add(Request("stale", state: DownloadState.NotFound));
        await _db.SaveChangesAsync();

        await WithLibrary().GenerateAsync();

        var groups = (await _db.DownloadRequests.ToListAsync()).Select(r => r.ReleaseGroupId);
        Assert.Equal(["g1", "g2", "stale"], groups.Order());
    }

    private RequestGenerator WithLibrary()
    {
        var subsonic = new SubsonicClient(
            new HttpClient(new FakeHttpMessageHandler(RespondLikeSubsonic)),
            "http://navidrome.local:4533",
            "fulano",
            "segredo123");

        return new RequestGenerator(_db, _musicBrainz, subsonic, new FakeTimeProvider(Now));
    }

    private HttpResponseMessage RespondLikeSubsonic(HttpRequestMessage request) =>
        _libraryFailing
            ? FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError)
            : FakeHttpMessageHandler.Json(SubsonicJson.AlbumList([.. _libraryAlbums]));

    private async Task<User> ListedUserAsync(
        string email,
        string artistId,
        string quality = Qualities.Mp3320,
        string edition = Editions.Original,
        params string[] types)
    {
        var user = new User
        {
            Email = email,
            Name = "Fulano",
            QualityPreference = quality,
            EditionPreference = edition,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ListedArtists.Add(Listed(user.Id, artistId, types));
        await _db.SaveChangesAsync();

        return user;
    }

    private static ListedArtist Listed(int userId, string artistId, params string[] types) =>
        new() { UserId = userId, ArtistId = artistId, Name = "Nirvana", ReleaseTypes = [.. types] };

    private static DownloadRequest Request(
        string releaseGroupId,
        int trackCount = 12,
        DownloadState state = DownloadState.Waiting,
        int attempts = 0)
    {
        var request = DownloadRequestTests.Request(releaseGroupId, trackCount);
        request.State = state;
        request.Attempts = attempts;

        return request;
    }

    private static string NirvanaReleases() =>
        Releases(
            4,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: true), ("CD", 12)),
            WithMedia(
                WithDisambiguation(
                    WithCoverArt(Release("Nevermind", "2011-09-26", "1991-09-24"), "r2", "g1", front: true),
                    "deluxe edition"),
                ("CD", 40)),
            WithPrimaryType(
                WithMedia(WithCoverArt(Release("Sliver", "1990-09-01", "1990-09-01"), "r3", "g2", front: true), ("CD", 2)),
                "Single"),
            WithMedia(
                WithCoverArt(Release("MTV Unplugged", "1994-11-01", "1994-11-01", "Live"), "r4", "g3", front: true),
                ("CD", 14)));

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var artist = HttpUtility.ParseQueryString(request.RequestUri!.Query)["artist"]!;

        if (_failing.Contains(artist))
        {
            return FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        }

        return FakeHttpMessageHandler.Json(_releases.GetValueOrDefault(artist, Releases(0)));
    }
}
