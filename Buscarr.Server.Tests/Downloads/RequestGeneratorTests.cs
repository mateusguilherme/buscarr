using System.Net;
using System.Web;
using Buscarr.Server.Data;
using Buscarr.Server.Deezer;
using Buscarr.Server.Downloads;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Spotify;
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
    private readonly List<(string Artist, string Title, string Cover)> _deezerAlbums = [];
    private readonly List<(string Artist, string Title, string Cover)> _spotifyAlbums = [];
    private readonly Dictionary<string, (string Artist, string Title, string Cover)> _deezerByBarcode = [];
    private readonly Dictionary<string, (string Artist, string Title, string Cover)> _spotifyByBarcode = [];
    private readonly Dictionary<string, string> _artists = [];
    private readonly Dictionary<string, string> _deezerArtists = [];
    private bool _libraryFailing;
    private string? _photoSavedWhenNirvanaWasFetched;
    private readonly MusicBrainzClient _musicBrainz;
    private readonly ArtistImages _images;
    private readonly FakeHttpMessageHandler _deezerHandler;
    private readonly DeezerClient _deezer;
    private readonly FakeHttpMessageHandler _spotifyHandler;
    private readonly SpotifyClient _spotify;
    private readonly RequestGenerator _generator;

    public RequestGeneratorTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        _musicBrainz = new MusicBrainzClient(
            new HttpClient(new FakeHttpMessageHandler(Respond)),
            new FakeTimeProvider(Now) { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });
        _deezerHandler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.StartsWith("/album/upc:", StringComparison.Ordinal))
            {
                return _deezerByBarcode.TryGetValue(path["/album/upc:".Length..], out var album)
                    ? FakeHttpMessageHandler.Json(DeezerJson.Album(album.Artist, album.Title, album.Cover))
                    : FakeHttpMessageHandler.Json(DeezerJson.NotFound());
            }

            if (path.StartsWith("/artist/", StringComparison.Ordinal))
            {
                return _deezerArtists.TryGetValue(path["/artist/".Length..], out var picture)
                    ? FakeHttpMessageHandler.Json(DeezerJson.ArtistProfile("AJULLIACOSTA", picture))
                    : FakeHttpMessageHandler.Json(DeezerJson.NotFound());
            }

            return FakeHttpMessageHandler.Json(DeezerJson.Albums([.. _deezerAlbums]));
        });
        _deezer = new DeezerClient(new HttpClient(_deezerHandler));
        _spotifyHandler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host == "accounts.spotify.com")
            {
                return FakeHttpMessageHandler.Json(SpotifyJson.Token());
            }

            var query = HttpUtility.ParseQueryString(request.RequestUri.Query)["q"] ?? "";

            if (query.StartsWith("upc:", StringComparison.Ordinal))
            {
                return _spotifyByBarcode.TryGetValue(query["upc:".Length..], out var album)
                    ? FakeHttpMessageHandler.Json(SpotifyJson.Albums(album))
                    : FakeHttpMessageHandler.Json(SpotifyJson.Albums());
            }

            return FakeHttpMessageHandler.Json(SpotifyJson.Albums([.. _spotifyAlbums]));
        });
        _spotify = new SpotifyClient(new HttpClient(_spotifyHandler), "id-1", "segredo-1", new FakeTimeProvider(Now));
        var fanartTv = new FanartTvClient(
            new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound))),
            "chave-de-teste");
        _images = new ArtistImages(fanartTv, _deezer, _spotify);
        var subsonic = new SubsonicClient(new HttpClient(), null, null, null);
        _generator = new RequestGenerator(
            _db,
            _musicBrainz,
            _images,
            new AlbumCovers(_deezer, _spotify),
            subsonic,
            new FakeTimeProvider(Now));
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
    public async Task GenerateAsync_KeepsARequestWaitingForItsQualityUpgrade()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        var upgrade = Request("stale", state: DownloadState.Imported);
        upgrade.AcceptedQuality = Qualities.Mp3320;
        _db.DownloadRequests.Add(upgrade);
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var groups = (await _db.DownloadRequests.ToListAsync()).Select(r => r.ReleaseGroupId);
        Assert.Equal(["g1", "stale"], groups.Order());
    }

    [Fact]
    public async Task GenerateAsync_KeepsARequestTheUserAskedForDirectly()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        var wished = Request("stale", state: DownloadState.NotFound);
        wished.Requested = true;
        _db.DownloadRequests.Add(wished);
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var groups = (await _db.DownloadRequests.ToListAsync()).Select(r => r.ReleaseGroupId);
        Assert.Equal(["g1", "stale"], groups.Order());
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

    [Fact]
    public async Task GenerateAsync_FallsBackToTheDeezerCoverWhenTheArchiveHasNone()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));
        _deezerAlbums.Add(("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_KeepsTheArchiveCoverWithoutAskingDeezerWhenAnEditionHasOne()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = NirvanaReleases();
        _deezerAlbums.Add(("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://coverartarchive.org/release/r1/front-500", request.CoverUrl);
        Assert.Empty(_deezerHandler.Requests);
    }

    [Fact]
    public async Task GenerateAsync_KeepsTheArchiveFallbackWhenDeezerAndSpotifyHaveNothing()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://coverartarchive.org/release-group/g1/front-500", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_AsksSpotifyWhenDeezerAlsoHasNoCover()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));
        _spotifyAlbums.Add(("Nirvana", "Nevermind", "https://i.scdn.co/image/n1"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://i.scdn.co/image/n1", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotAskSpotifyWhenDeezerHasTheCover()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));
        _deezerAlbums.Add(("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg"));
        _spotifyAlbums.Add(("Nirvana", "Nevermind", "https://i.scdn.co/image/n1"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg", request.CoverUrl);
        Assert.Empty(_spotifyHandler.Requests);
    }

    [Fact]
    public async Task GenerateAsync_FindsTheCoverOfACollabByTheMainCreditOnDeezer()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithArtistCredit(
                WithMedia(WithCoverArt(Release("Do Corre", "2025-03-27", "2025-03-27"), "r1", "g1", front: false), ("CD", 1)),
                "Maru2D",
                "Nirvana"));
        _deezerAlbums.Add(("Maru2D", "Do Corre", "https://cdn-images.dzcdn.net/images/cover/m1/250x250-000000-80-0-0.jpg"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/m1/250x250-000000-80-0-0.jpg", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_FindsTheCoverOfACollabByTheMainCreditOnSpotify()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithArtistCredit(
                WithMedia(WithCoverArt(Release("Do Corre", "2025-03-27", "2025-03-27"), "r1", "g1", front: false), ("CD", 1)),
                "Maru2D",
                "Nirvana"));
        _spotifyAlbums.Add(("Maru2D", "Do Corre", "https://i.scdn.co/image/m1"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://i.scdn.co/image/m1", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_FindsTheCoverByTheBarcodeOnDeezer()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["single"]);
        _releases["nirvana"] = Releases(
            1,
            WithBarcode(
                WithPrimaryType(
                    WithMedia(WithCoverArt(Release("#SV 03", "2024-10-11", "2024-10-11"), "r1", "g1", front: false), ("Digital Media", 1)),
                    "Single"),
                "198748762851"));
        _deezerByBarcode["198748762851"] =
            ("AJULLIACOSTA", "SALA VERMELHA #03", "https://cdn-images.dzcdn.net/images/cover/sv/250x250-000000-80-0-0.jpg");

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/sv/250x250-000000-80-0-0.jpg", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_FindsTheCoverByTheBarcodeOnSpotify()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["single"]);
        _releases["nirvana"] = Releases(
            1,
            WithBarcode(
                WithPrimaryType(
                    WithMedia(WithCoverArt(Release("#SV 03", "2024-10-11", "2024-10-11"), "r1", "g1", front: false), ("Digital Media", 1)),
                    "Single"),
                "198748762851"));
        _spotifyByBarcode["198748762851"] = ("AJULLIACOSTA", "SALA VERMELHA #03", "https://i.scdn.co/image/sv");

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://i.scdn.co/image/sv", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_FillsTheMissingPhotoOfAListedArtist()
    {
        await ListedUserAsync("fulano@exemplo.com", "ajulliacosta", types: ["album"]);
        _artists["ajulliacosta"] = Artist("ajulliacosta", "AJULLIACOSTA", "https://www.deezer.com/artist/52654632");
        _deezerArtists["52654632"] = "https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg";

        await _generator.GenerateAsync();

        var listed = Assert.Single(await _db.ListedArtists.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg", listed.Image);
    }

    [Fact]
    public async Task GenerateAsync_LeavesTheArtistPhotoAloneWhenNothingIsFound()
    {
        await ListedUserAsync("fulano@exemplo.com", "project46", types: ["album"]);

        await _generator.GenerateAsync();

        var listed = Assert.Single(await _db.ListedArtists.ToListAsync());
        Assert.Null(listed.Image);
    }

    [Fact]
    public async Task GenerateAsync_SavesAFoundPhotoBeforeFinishingTheCycle()
    {
        var user = await ListedUserAsync("fulano@exemplo.com", "ajulliacosta", types: ["album"]);
        _db.ListedArtists.Add(Listed(user.Id, "nirvana", "album"));
        await _db.SaveChangesAsync();
        _artists["ajulliacosta"] = Artist("ajulliacosta", "AJULLIACOSTA", "https://www.deezer.com/artist/52654632");
        _deezerArtists["52654632"] = "https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg";
        _releases["nirvana"] = NirvanaReleases();

        await _generator.GenerateAsync();

        Assert.Equal(
            "https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg",
            _photoSavedWhenNirvanaWasFetched);
    }

    [Fact]
    public async Task GenerateAsync_LooksPastTheCacheWhenRefreshingAPhoto()
    {
        await ListedUserAsync("fulano@exemplo.com", "ajulliacosta", types: ["album"]);
        _artists["ajulliacosta"] = Artist("ajulliacosta", "AJULLIACOSTA");
        await _musicBrainz.ArtistAsync("fulano@exemplo.com", "ajulliacosta");
        _artists["ajulliacosta"] = Artist("ajulliacosta", "AJULLIACOSTA", "https://www.deezer.com/artist/52654632");
        _deezerArtists["52654632"] = "https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg";

        await _generator.GenerateAsync();

        var listed = Assert.Single(await _db.ListedArtists.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/artist/abc/250x250-000000-80-0-0.jpg", listed.Image);
    }

    [Fact]
    public async Task GenerateAsync_StoresTheFullCreditOfTheRelease()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            2,
            WithArtistCredit(
                WithMedia(WithCoverArt(Release("Do Corre", "2025-03-27", "2025-03-27"), "r1", "g1", front: true), ("CD", 1)),
                "Maru2D",
                "Nirvana"),
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r2", "g2", front: true), ("CD", 12)));

        await _generator.GenerateAsync();

        var requests = await _db.DownloadRequests.OrderBy(r => r.ReleaseGroupId).ToListAsync();
        Assert.Equal(["Maru2D & Nirvana", "Nirvana"], requests.Select(r => r.Credit));
    }

    [Fact]
    public async Task GenerateAsync_FillsTheCreditOfAnExistingRequest()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithArtistCredit(
                WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: true), ("CD", 12)),
                "Maru2D",
                "Nirvana"));
        _db.DownloadRequests.Add(Request("g1", trackCount: 12, state: DownloadState.NotFound));
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("Maru2D & Nirvana", request.Credit);
    }

    [Fact]
    public async Task GenerateAsync_KeepsTheStoredCoverWhenTheLookupsFindNothing()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));
        var existing = Request("g1", trackCount: 12, state: DownloadState.NotFound);
        existing.CoverUrl = "https://cdn-images.dzcdn.net/images/cover/boa/250x250-000000-80-0-0.jpg";
        _db.DownloadRequests.Add(existing);
        await _db.SaveChangesAsync();

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/boa/250x250-000000-80-0-0.jpg", request.CoverUrl);
    }

    [Fact]
    public async Task GenerateAsync_FillsTheCoverOfAnExistingRequest()
    {
        await ListedUserAsync("fulano@exemplo.com", "nirvana", types: ["album"]);
        _releases["nirvana"] = Releases(
            1,
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)));
        var existing = Request("g1", trackCount: 12, state: DownloadState.NotFound, attempts: 2);
        existing.CoverUrl = "https://coverartarchive.org/release-group/g1/front-250";
        _db.DownloadRequests.Add(existing);
        await _db.SaveChangesAsync();
        _deezerAlbums.Add(("Nirvana", "Nevermind", "https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg"));

        await _generator.GenerateAsync();

        var request = Assert.Single(await _db.DownloadRequests.ToListAsync());
        Assert.Equal("https://cdn-images.dzcdn.net/images/cover/n1/250x250-000000-80-0-0.jpg", request.CoverUrl);
        Assert.Equal((DownloadState.NotFound, 2), (request.State, request.Attempts));
    }

    private RequestGenerator WithLibrary()
    {
        var subsonic = new SubsonicClient(
            new HttpClient(new FakeHttpMessageHandler(RespondLikeSubsonic)),
            "http://navidrome.local:4533",
            "fulano",
            "segredo123");

        return new RequestGenerator(
            _db,
            _musicBrainz,
            _images,
            new AlbumCovers(_deezer, _spotify),
            subsonic,
            new FakeTimeProvider(Now));
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
        var path = request.RequestUri!.AbsolutePath;

        if (path.StartsWith("/ws/2/artist/", StringComparison.Ordinal))
        {
            var id = path["/ws/2/artist/".Length..];

            return FakeHttpMessageHandler.Json(_artists.GetValueOrDefault(id, Artist(id, "Nirvana")));
        }

        var artist = HttpUtility.ParseQueryString(request.RequestUri!.Query)["artist"]!;

        if (artist == "nirvana")
        {
            using var probe = new BuscarrDbContext(
                new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
            _photoSavedWhenNirvanaWasFetched = probe.ListedArtists.AsNoTracking()
                .SingleOrDefault(listed => listed.ArtistId == "ajulliacosta")?.Image;
        }

        if (_failing.Contains(artist))
        {
            return FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        }

        return FakeHttpMessageHandler.Json(_releases.GetValueOrDefault(artist, Releases(0)));
    }
}
