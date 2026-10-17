using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Tests.Fakes;
using static Buscarr.Server.Tests.Fakes.SubsonicJson;

namespace Buscarr.Server.Tests.Subsonic;

public class SubsonicClientTests
{
    private const string Url = "http://navidrome.local:4533";
    private const string User = "fulano";
    private const string Password = "segredo123";

    [Fact]
    public async Task AlbumsAsync_AsksForTheAlbumsWithTokenAuthentication()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(EmptyAlbumList()));

        await client.AlbumsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith($"{Url}/rest/getAlbumList2?", request.Uri.ToString());
        Assert.StartsWith("buscarr/", request.UserAgent);
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal(("alphabeticalByName", "500", "0"), (query["type"], query["size"], query["offset"]));
        Assert.Equal((User, "1.16.1", "buscarr", "json"), (query["u"], query["v"], query["c"], query["f"]));
        var salt = query["s"]!;
        Assert.NotEmpty(salt);
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(Password + salt))), query["t"]);
    }

    [Fact]
    public async Task AlbumsAsync_ReadsEveryAlbumOfThePage()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(AlbumList(
            Album("al-1", "Nevermind", "Nirvana", 12, "r1"),
            Album("al-2", "Respiro", "ConeCrewDiretoria", 13))));

        var albums = await client.AlbumsAsync();

        Assert.Equal(
        [
            new SubsonicAlbum("al-1", "Nevermind", "Nirvana", 12, "r1"),
            new SubsonicAlbum("al-2", "Respiro", "ConeCrewDiretoria", 13, null),
        ], albums);
    }

    [Fact]
    public async Task AlbumsAsync_FetchesEveryPageWithAFreshSalt()
    {
        var first = Enumerable.Range(0, 500).Select(i => Album($"al-{i}", $"Album {i}", "Nirvana", 10));
        var (client, handler) = CreateClient(request =>
            HttpUtility.ParseQueryString(request.RequestUri!.Query)["offset"] == "0"
                ? FakeHttpMessageHandler.Json(AlbumList([.. first]))
                : FakeHttpMessageHandler.Json(AlbumList(Album("al-500", "Album 500", "Nirvana", 10))));

        var albums = await client.AlbumsAsync();

        Assert.Equal(501, albums.Count);
        Assert.Equal(2, handler.Requests.Count);
        var offsets = handler.Requests.Select(r => HttpUtility.ParseQueryString(r.Uri.Query)["offset"]);
        Assert.Equal(["0", "500"], offsets);
        var salts = handler.Requests.Select(r => HttpUtility.ParseQueryString(r.Uri.Query)["s"]).ToList();
        Assert.NotEqual(salts[0], salts[1]);
    }

    [Fact]
    public async Task AlbumsAsync_AcceptsAUrlEndingInASlash()
    {
        var (client, handler) = CreateClient(_ => FakeHttpMessageHandler.Json(EmptyAlbumList()), $"{Url}/");

        await client.AlbumsAsync();

        Assert.StartsWith($"{Url}/rest/getAlbumList2?", Assert.Single(handler.Requests).Uri.ToString());
    }

    [Fact]
    public async Task AlbumsAsync_ThrowsWithTheServerMessageWhenTheServerRefuses()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Json(Failed("Wrong username or password")));

        var error = await Assert.ThrowsAsync<HttpRequestException>(client.AlbumsAsync);

        Assert.Contains("Wrong username or password", error.Message);
    }

    [Fact]
    public async Task AlbumsAsync_ThrowsWhenTheServerIsDown()
    {
        var (client, _) = CreateClient(_ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(client.AlbumsAsync);
    }

    [Theory]
    [InlineData(null, User, Password)]
    [InlineData(Url, null, Password)]
    [InlineData(Url, User, null)]
    [InlineData("", "", "")]
    public void IsConfigured_NeedsUrlUserAndPassword(string? url, string? user, string? password)
    {
        var client = new SubsonicClient(new HttpClient(), url, user, password);

        Assert.False(client.IsConfigured);
    }

    [Fact]
    public async Task AlbumsAsync_ThrowsWhenNotConfigured()
    {
        var client = new SubsonicClient(new HttpClient(), null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(client.AlbumsAsync);
    }

    private static (SubsonicClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? url = Url)
    {
        var handler = new FakeHttpMessageHandler(respond);

        return (new SubsonicClient(new HttpClient(handler), url, User, Password), handler);
    }
}
