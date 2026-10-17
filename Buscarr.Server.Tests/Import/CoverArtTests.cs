using System.Net;
using Buscarr.Server.Import;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Import;

public sealed class CoverArtTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

    [Fact]
    public async Task DownloadAsync_FetchesTheFullSizeFrontCover()
    {
        var handler = new FakeHttpMessageHandler(_ => Image(Jpeg));
        var covers = new CoverArt(new HttpClient(handler));

        var cover = await covers.DownloadAsync("https://coverartarchive.org/release/r1/front-250");

        Assert.Equal("https://coverartarchive.org/release/r1/front", Assert.Single(handler.Requests).Uri.ToString());
        Assert.NotNull(cover);
        Assert.Equal(Jpeg, cover.Bytes);
        Assert.Equal(("image/jpeg", ".jpg"), (cover.MimeType, cover.Extension));
    }

    [Fact]
    public async Task DownloadAsync_DetectsAPngCover()
    {
        var covers = new CoverArt(new HttpClient(new FakeHttpMessageHandler(_ => Image(Png))));

        var cover = await covers.DownloadAsync("https://coverartarchive.org/release-group/g1/front-250");

        Assert.NotNull(cover);
        Assert.Equal(("image/png", ".png"), (cover.MimeType, cover.Extension));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNullWithoutAUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => Image(Jpeg));
        var covers = new CoverArt(new HttpClient(handler));

        Assert.Null(await covers.DownloadAsync(null));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNullWhenTheServerFails()
    {
        var covers = new CoverArt(new HttpClient(
            new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.NotFound))));

        Assert.Null(await covers.DownloadAsync("https://coverartarchive.org/release/r1/front-250"));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNullForAnUnknownImageFormat()
    {
        var covers = new CoverArt(new HttpClient(new FakeHttpMessageHandler(_ => Image([0x00, 0x01]))));

        Assert.Null(await covers.DownloadAsync("https://coverartarchive.org/release/r1/front-250"));
    }

    private static HttpResponseMessage Image(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
}
