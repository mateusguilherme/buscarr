using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarr.Server.Tests.Endpoints;

public sealed class DownloadEndpointsTests
{
    [Fact]
    public async Task GetDownloads_WithoutLogin_Returns401()
    {
        using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/downloads");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloads_ListsTheActiveRequestsFirstWithKebabCaseStates()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await SeedAsync(factory, Request("g-waiting", DownloadState.Waiting, nextAttemptInDays: 2));
        await SeedAsync(factory, Request("g-notfound", DownloadState.NotFound, nextAttemptInDays: 1));
        await SeedAsync(factory, Request("g-downloaded", DownloadState.Downloaded));
        await SeedAsync(factory, Request("g-downloading", DownloadState.Downloading));
        await SeedAsync(factory, Request("g-cancelled", DownloadState.Cancelled));

        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

        Assert.Equal(
            ["downloading", "not-found", "waiting", "downloaded", "cancelled"],
            downloads!.Select(download => download.State));
    }

    [Fact]
    public async Task GetDownloads_DescribesTheRequest()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = Request("g1", DownloadState.Downloading);
        request.Failure = null;
        request.TracksDone = 7;
        request.BytesTransferred = 70;
        request.BytesTotal = 120;
        request.PlaceInQueue = 3;
        request.Username = "fulano";
        request.CoverUrl = "https://coverartarchive.org/release/r1/front-250";
        await SeedAsync(factory, request);

        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");
        var download = Assert.Single(downloads!);

        Assert.Equal(
            new DownloadItem(
                request.Id,
                "Nirvana",
                "Nevermind",
                "album",
                "https://coverartarchive.org/release/r1/front-250",
                12,
                "mp3-320",
                "downloading",
                null,
                0,
                request.NextAttemptAt,
                7,
                70,
                120,
                3,
                "fulano"),
            download);
    }

    [Fact]
    public async Task CancelDownload_OfAMissingRequest_Returns404()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync("/api/downloads/123/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelDownload_StopsAWaitingRequestForGood()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Waiting));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("cancelled", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task CancelDownload_OfADownloadingRequest_MarksItCancelled()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Downloading));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("cancelled", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task CancelDownload_OfADownloadedRequest_Returns409()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Downloaded));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("downloaded", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task Events_WithoutLogin_Returns401()
    {
        using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/downloads/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_StreamTheSnapshotAndThenEachChange()
    {
        using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Searching));

        using var response = await client.GetAsync("/api/downloads/events", HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("\"state\":\"searching\"", await ReadEventAsync(reader));

        await SetStateAsync(factory, request.Id, DownloadState.Downloaded);

        Assert.Contains("\"state\":\"downloaded\"", await NextEventAsync(factory, reader));
    }

    private static async Task<string> ReadEventAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return line["data: ".Length..];
            }
        }

        throw new InvalidOperationException("o stream acabou sem um evento");
    }

    private static async Task<string> NextEventAsync(BuscarrApiFactory factory, StreamReader reader)
    {
        var read = ReadEventAsync(reader);

        while (!read.IsCompleted)
        {
            factory.Time.Advance(TimeSpan.FromSeconds(3));
            await Task.Delay(10);
        }

        return await read;
    }

    private static async Task SetStateAsync(BuscarrApiFactory factory, int id, DownloadState state)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var request = await db.DownloadRequests.FindAsync(id);
        request!.State = state;
        await db.SaveChangesAsync();
    }

    private static DownloadRequest Request(string releaseGroupId, DownloadState state, int nextAttemptInDays = 0)
    {
        var request = DownloadRequestTests.Request(releaseGroupId);
        request.State = state;
        request.NextAttemptAt = DateTimeOffset.UnixEpoch.AddDays(nextAttemptInDays);

        return request;
    }

    private static async Task<DownloadRequest> SeedAsync(BuscarrApiFactory factory, DownloadRequest request)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        db.DownloadRequests.Add(request);
        await db.SaveChangesAsync();

        return request;
    }

    private static async Task<string> StateAsync(HttpClient client, int id)
    {
        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

        return downloads!.Single(download => download.Id == id).State;
    }
}
