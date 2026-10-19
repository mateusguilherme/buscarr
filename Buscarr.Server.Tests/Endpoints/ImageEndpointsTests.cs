using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

public class ImageEndpointsTests
{
    private const string Nirvana = $"/api/list/{BuscarrApiFactory.NirvanaId}";

    private static readonly ListSettings Settings = new(["album"]);

    private static string ImagePath => $"/api/images?url={Uri.EscapeDataString(BuscarrApiFactory.NirvanaImage)}";

    [Fact]
    public async Task GetImage_WithoutLogin_Returns401()
    {
        await using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync(ImagePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetImage_ServesAStoredPhotoFromTheSourceOnlyOnce()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await client.PutAsJsonAsync(Nirvana, Settings);

        var first = await client.GetAsync(ImagePath);
        var second = await client.GetAsync(ImagePath);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("image/jpeg", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3], await first.Content.ReadAsByteArrayAsync());
        Assert.Equal([1, 2, 3], await second.Content.ReadAsByteArrayAsync());
        Assert.Single(factory.Images.Requests);
    }

    [Fact]
    public async Task GetImage_RefusesAnUrlThatIsNotStored()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/images?url=https%3A%2F%2Fexemplo.com%2Fqualquer.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Images.Requests);
    }

    [Fact]
    public async Task GetImage_DoesNotKeepAFailedFetch()
    {
        await using var factory = new BuscarrApiFactory();
        factory.Images.Respond = _ => FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        var client = await factory.CreateLoggedInClientAsync();
        await client.PutAsJsonAsync(Nirvana, Settings);

        var failed = await client.GetAsync(ImagePath);
        factory.Images.Respond = _ => FakeHttpMessageHandler.Bytes([1, 2, 3], "image/jpeg");
        var recovered = await client.GetAsync(ImagePath);

        Assert.Equal(HttpStatusCode.NotFound, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, factory.Images.Requests.Count);
    }
}
