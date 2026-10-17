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
    public async Task FindArtistAsync_ReturnsFirstArtistWhoseNameContainsQuery()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(
            Artists(("1", "Something Else"), ("2", "Nirvana"), ("3", "Approaching Nirvana"))));

        var artist = await client.FindArtistAsync(Contact, "nirvana");

        Assert.Equal(new Artist("2", "Nirvana"), artist);
    }

    [Fact]
    public async Task FindArtistAsync_ReturnsNullWhenNoNameContainsQuery()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("1", "Something Else"))));

        var artist = await client.FindArtistAsync(Contact, "nirvana");

        Assert.Null(artist);
    }

    [Fact]
    public async Task FindArtistAsync_SendsContactInUserAgentAndAsksForJson()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists()));

        await client.FindArtistAsync(Contact, "nirvana");

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("buscarr/", request.UserAgent);
        Assert.Contains(Contact, request.UserAgent);
        Assert.Contains("fmt=json", request.Uri.Query);
        Assert.Contains("dismax=true", request.Uri.Query);
    }

    [Fact]
    public async Task FindArtistAsync_WithCountry_FiltersByCountryInsteadOfDismax()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists(("1", "Sigur Rós"))));

        await client.FindArtistAsync(Contact, "Sigur Rós", "IS");

        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("(sigur rós) AND country:IS", query["query"]);
        Assert.Null(query["dismax"]);
    }

    [Fact]
    public async Task FindArtistAsync_WithCountry_EscapesSearchSyntaxInName()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists()));

        await client.FindArtistAsync(Contact, "AC/DC: Live!", "AU");

        var query = HttpUtility.ParseQueryString(handler.Requests[0].Uri.Query);
        Assert.Equal(@"(ac\/dc\: live\!) AND country:AU", query["query"]);
    }

    [Fact]
    public async Task FindArtistAsync_WithCountry_LooksUpArtistsWhoseAreaIsInsideACountry()
    {
        var (client, handler) = CreateClient(request => request.RequestUri!.AbsolutePath switch
        {
            "/ws/2/artist" when request.RequestUri.Query.Contains("dismax=true") =>
                FakeHttpMessageHandler.Json(ArtistsWithArea(
                    ("1", "Rancore", "IT", "Italy"),
                    ("2", "Rancore", null, "São Paulo"))),
            "/ws/2/artist" => FakeHttpMessageHandler.Json(Artists()),
            "/ws/2/artist/2" => FakeHttpMessageHandler.Json(Artist("2", "Rancore", "BR")),
            _ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound),
        });

        var artist = await client.FindArtistAsync(Contact, "rancore", "BR");

        Assert.Equal("2", artist?.Id);
        Assert.Equal(
            ["/ws/2/artist", "/ws/2/artist", "/ws/2/artist/2"],
            handler.Requests.Select(r => r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task FindArtistAsync_WithCountry_ReturnsNullWithoutLookingUpArtistsWithoutArea()
    {
        var (client, handler) = CreateClient(request => request.RequestUri!.AbsolutePath switch
        {
            "/ws/2/artist" when request.RequestUri.Query.Contains("dismax=true") =>
                FakeHttpMessageHandler.Json(ArtistsWithArea(
                    ("1", "Rancore", null, null),
                    ("2", "Rancore", null, "Roma"))),
            "/ws/2/artist" => FakeHttpMessageHandler.Json(Artists()),
            "/ws/2/artist/2" => FakeHttpMessageHandler.Json(Artist("2", "Rancore", "IT")),
            _ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound),
        });

        var artist = await client.FindArtistAsync(Contact, "rancore", "BR");

        Assert.Null(artist);
        Assert.Equal(
            ["/ws/2/artist", "/ws/2/artist", "/ws/2/artist/2"],
            handler.Requests.Select(r => r.Uri.AbsolutePath));
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

        var artist = await client.FindArtistAsync(Contact, "nirvana");

        Assert.Equal("Nirvana", artist?.Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_GiveUpAfterThreeBusyResponses()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindArtistAsync(Contact, "nirvana"));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Requests_WaitOneSecondAfterThePreviousOne()
    {
        var time = new FakeTimeProvider();
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Artists()), time);
        await client.FindArtistAsync(Contact, "nirvana");

        var second = client.FindArtistAsync(Contact, "nirvana");

        Assert.False(second.IsCompleted);
        Assert.Single(handler.Requests);

        time.Advance(TimeSpan.FromSeconds(1));
        await second;

        Assert.Equal(2, handler.Requests.Count);
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

        var releases = await client.ReleasesAsync(Contact, "artist-id", null);

        Assert.Equal(["Bleach", "Nevermind"], releases.Select(r => r.Title));
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

        var releases = await client.ReleasesAsync(Contact, "artist-id", null);

        Assert.Equal(
            ["Bleach", "Nevermind", "MTV Unplugged in New York"],
            releases.Select(r => r.Title));
    }

    [Theory]
    [InlineData("album", true)]
    [InlineData(null, false)]
    public async Task ReleasesAsync_SendsTypeFilterOnlyWhenGiven(string? releaseType, bool sendsType)
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(Releases(0)));

        await client.ReleasesAsync(Contact, "artist-id", releaseType);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(sendsType, request.Uri.Query.Contains("type=album"));
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
