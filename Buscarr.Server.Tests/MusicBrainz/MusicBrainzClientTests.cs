using System.Net;
using System.Web;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.MusicBrainzJson;

namespace Buscarr.Server.Tests.MusicBrainz;

public class MusicBrainzClientTests
{
    private const string Contact = "fulano@exemplo.com";

    [Fact]
    public async Task FindArtistsAsync_ReturnsArtistsWhoseNameContainsQuery()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(
            Artists(("1", "Something Else"), ("2", "Nirvana"), ("3", "Approaching Nirvana"))));

        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Equal(["2", "3"], artists.Select(a => a.Id));
    }

    [Fact]
    public async Task FindArtistsAsync_MatchesNamesIgnoringDiacritics()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(
            Artists(("1", "Motörhead"), ("2", "Lemmy Kilmister"), ("3", "MOTORHEADACHE"))));

        var artists = await client.FindArtistsAsync(Contact, "motorhead");

        Assert.Equal(["1", "3"], artists.Select(a => a.Id));
    }

    [Fact]
    public async Task FindArtistsAsync_ReturnsEmptyWhenNoNameContainsQuery()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("1", "Something Else"))));

        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Empty(artists);
    }

    [Fact]
    public async Task FindArtistsAsync_SendsContactInUserAgentAndAsksForJson()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists()));

        await client.FindArtistsAsync(Contact, "nirvana");

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("buscarr/", request.UserAgent);
        Assert.Contains(Contact, request.UserAgent);
        Assert.Contains("fmt=json", request.Uri.Query);
        Assert.Contains("dismax=true", request.Uri.Query);
    }

    [Fact]
    public async Task FindArtistsAsync_ReadsDisambiguationAndArea()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(SearchResults(
            SearchResult("2", "Rancore", "São Paulo", "Brazillian Rock Band"))));

        var artist = Assert.Single(await client.FindArtistsAsync(Contact, "rancore"));

        Assert.Equal("Brazillian Rock Band", artist.Disambiguation);
        Assert.Equal("São Paulo", artist.Area?.Name);
    }

    [Fact]
    public async Task Requests_AreRetriedWhenMusicBrainzIsBusy()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable),
            FakeHttpMessageHandler.Json(Artists(("2", "Nirvana"))),
        ]);
        var (client, handler) = CreateClient(_ => responses.Dequeue());

        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Equal("Nirvana", Assert.Single(artists).Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_GiveUpAfterThreeBusyResponses()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindArtistsAsync(Contact, "nirvana"));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_WaitOneSecondAfterThePreviousOne()
    {
        var time = new FakeTimeProvider();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists()), time);
        await client.FindArtistsAsync(Contact, "nirvana");

        var second = client.FindArtistsAsync(Contact, "korn");

        Assert.False(second.IsCompleted);
        Assert.Single(handler.Requests);

        time.Advance(TimeSpan.FromSeconds(1));
        await second;

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_AreAnsweredFromTheCacheWhenRepeated()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("2", "Nirvana"))));
        await client.FindArtistsAsync(Contact, "nirvana");

        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Equal("Nirvana", Assert.Single(artists).Name);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Requests_SkipTheWaitWhenAnsweredFromTheCache()
    {
        var time = new FakeTimeProvider();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("2", "Nirvana"))), time);
        await client.FindArtistsAsync(Contact, "nirvana");

        var second = client.FindArtistsAsync(Contact, "nirvana");

        Assert.True(second.IsCompleted);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Requests_AreRepeatedWhenTheCacheExpires()
    {
        var time = new FakeTimeProvider();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("2", "Nirvana"))), time);
        await client.FindArtistsAsync(Contact, "nirvana");

        time.Advance(TimeSpan.FromHours(24));
        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Equal("Nirvana", Assert.Single(artists).Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_AreNotCachedWhenTheyFail()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable),
            FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable),
            FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable),
            FakeHttpMessageHandler.Json(Artists(("2", "Nirvana"))),
        ]);
        var (client, handler) = CreateClient(_ => responses.Dequeue());

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindArtistsAsync(Contact, "nirvana"));
        var artists = await client.FindArtistsAsync(Contact, "nirvana");

        Assert.Equal("Nirvana", Assert.Single(artists).Name);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task ReleasesAsync_FetchesEveryPage()
    {
        var pages = new Queue<string>(
        [
            Releases(2, Release("Bleach", "1989-06-15", "1989")),
            Releases(2, Release("Nevermind", "1991-09-24", "1991-09-24")),
        ]);
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(pages.Dequeue()));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(["Bleach", "Nevermind"], releases.Select(r => r.First.Title));
        Assert.Contains("offset=0", handler.Requests[0].Uri.Query);
        Assert.Contains("offset=1", handler.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task ReleasesAsync_KeepsFirstOriginalEditionOfEachTitleOrderedByDate()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            7,
            Release("Nevermind", "2011-09-26", "1991-09-24"),
            Release("MTV Unplugged in New York", "1994-11-01", "1994-11-01", "Live"),
            Release("Nevermind", "1991-09-24", "1991-09-24"),
            Release("Incesticide", "1992-12-14", "1992-12-14", "Compilation"),
            Release("Bleach", "1989-06-15", "1989"),
            Release("NEVERMIND", "1991-09-24", "1991-09-24"),
            Release("Sliver", "1990", "1990"))));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(
            ["Bleach", "Sliver", "Nevermind", "Incesticide", "MTV Unplugged in New York"],
            releases.Select(r => r.First.Title));
        Assert.Equal(
            ["album", "album", "album", "compilation", "live"],
            releases.Select(r => r.First.Category));
    }

    [Fact]
    public async Task ReleasesAsync_KeepsADemoApartFromAnAlbumWithTheSameTitle()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            2,
            Release("Nevermind", "1991-09-24", "1991-09-24"),
            Release("Nevermind", "1991-01-01", "1991-01-01", "Demo"))));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(
            [("Nevermind", "demo"), ("Nevermind", "album")],
            releases.Select(r => (r.First.Title, r.First.Category)));
    }

    [Fact]
    public async Task ReleasesAsync_LeavesOutReleasesWithoutACategory()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            2,
            WithPrimaryType(Release("Live & Loud", "1993-12-13", "1993-12-13"), "Broadcast"),
            Release("Nevermind", "1991-09-24", "1991-09-24"))));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(["Nevermind"], releases.Select(r => r.First.Title));
    }

    [Fact]
    public async Task ReleasesAsync_KeepsReleasesOfDifferentTypesWithTheSameTitle()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            2,
            Release("Master of Puppets", "1986-03-03", "1986-03-03"),
            WithPrimaryType(Release("Master of Puppets", "1986-07-02", "1986-07-02"), "Single"))));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(["album", "single"], releases.Select(r => r.First.Type));
    }

    [Fact]
    public async Task ReleasesAsync_ReadsTheCoverArtOfEachRelease()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            2,
            WithCoverArt(Release("Bleach", "1989-06-15", "1989"), "r1", "g1", front: true),
            WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r2", "g2", front: false))));

        var releases = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(
            [
                "https://coverartarchive.org/release/r1/front-500",
                "https://coverartarchive.org/release-group/g2/front-500",
            ],
            releases.Select(r => r.First.CoverUrl));
    }

    [Fact]
    public async Task ReleasesAsync_KeepsEveryEditionOfEachAlbum()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(
            3,
            WithMedia(WithCoverArt(Release("Nevermind", "2011-09-26", "1991-09-24"), "r2", "g1", front: false), ("CD", 40)),
            WithMedia(WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: false), ("CD", 12)),
            WithMedia(WithCoverArt(Release("Bleach", "1989-06-15", "1989"), "r3", "g2", front: false), ("Vinyl", 11)))));

        var albums = await client.ReleasesAsync(Contact, "artist-id");

        Assert.Equal(
            [("r3", ["r3"]), ("r1", ["r2", "r1"])],
            albums.Select(album => (album.First.Id, album.Editions.Select(edition => edition.Id).ToArray())));
        Assert.Equal([40, 12], albums[1].Editions.Select(edition => edition.TrackCount));
    }

    [Fact]
    public async Task ReleasesAsync_AsksForOfficialReleasesOfEveryType()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(0)));

        await client.ReleasesAsync(Contact, "artist-id");

        var parameters = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("official", parameters["status"]);
        Assert.Equal("release-groups media artist-credits", parameters["inc"]);
        Assert.Null(parameters["type"]);
    }

    [Fact]
    public async Task ReleaseTracksAsync_ReturnsTracksInGlobalOrderAcrossMedia()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(ReleaseWithTracks(
            "r1",
            "a1",
            "g1",
            LookupMedium(
                2,
                LookupTrack(1, "Territorial Pissings", 143_000, "rec3")),
            LookupMedium(
                1,
                LookupTrack(2, "In Bloom", null, "rec2", 255_000),
                LookupTrack(1, "Smells Like Teen Spirit", 301_000, "rec1")))));

        var edition = await client.ReleaseTracksAsync(Contact, "r1");

        Assert.Equal(("a1", "g1"), (edition.ArtistId, edition.ReleaseGroupId));
        Assert.Equal(
            [
                (1, "Smells Like Teen Spirit", 301_000, "rec1"),
                (2, "In Bloom", 255_000, "rec2"),
                (3, "Territorial Pissings", 143_000, "rec3"),
            ],
            edition.Tracks.Select(track => (track.Position, track.Title, track.LengthMs, track.RecordingId)));
    }

    [Fact]
    public async Task ReleaseTracksAsync_AsksForRecordingsArtistsAndReleaseGroups()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(ReleaseWithTracks("r1", "a1", "g1")));

        await client.ReleaseTracksAsync(Contact, "r1");

        var request = Assert.Single(handler.Requests);
        Assert.Contains("/release/r1", request.Uri.AbsolutePath);
        var parameters = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal("recordings artist-credits release-groups media", parameters["inc"]);
    }

    private static (MusicBrainzClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        TimeProvider? time = null)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var client = new MusicBrainzClient(
            new HttpClient(handler),
            time ?? new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });

        return (client, handler);
    }
}
