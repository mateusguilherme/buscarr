using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarr.Server.Tests.Endpoints;

public sealed class DownloadEndpointsTests
{
    [Fact]
    public async Task GetDownloads_WithoutLogin_Returns401()
    {
        await using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/downloads");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloads_ListsTheActiveRequestsFirstWithKebabCaseStates()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await SeedAsync(factory, Request("g-waiting", DownloadState.Waiting, nextAttemptInDays: 2));
        await SeedAsync(factory, Request("g-notfound", DownloadState.NotFound, nextAttemptInDays: 1));
        await SeedAsync(factory, Request("g-downloaded", DownloadState.Downloaded));
        await SeedAsync(factory, Request("g-downloading", DownloadState.Downloading));
        await SeedAsync(factory, Request("g-cancelled", DownloadState.Cancelled));
        await SeedAsync(factory, Request("g-importing", DownloadState.Importing));
        await SeedAsync(factory, Request("g-imported", DownloadState.Imported));

        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

        Assert.Equal(
            ["downloading", "importing", "not-found", "waiting", "downloaded", "imported", "cancelled"],
            downloads!.Select(download => download.State));
    }

    [Fact]
    public async Task CancelDownload_RefusesImportingAndImported()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var importing = await SeedAsync(factory, Request("g-importing", DownloadState.Importing));
        var imported = await SeedAsync(factory, Request("g-imported", DownloadState.Imported));

        var one = await client.PostAsync($"/api/downloads/{importing.Id}/cancel", null);
        var two = await client.PostAsync($"/api/downloads/{imported.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, one.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, two.StatusCode);
        Assert.Equal("importing", await StateAsync(client, importing.Id));
        Assert.Equal("imported", await StateAsync(client, imported.Id));
    }

    [Fact]
    public async Task GetDownloads_DescribesTheRequest()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = Request("g1", DownloadState.Downloading);
        request.Credit = "Maru2D & Nirvana";
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
                "Maru2D & Nirvana",
                "Nevermind",
                "album",
                "/api/images?url=https%3A%2F%2Fcoverartarchive.org%2Frelease%2Fr1%2Ffront-250",
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
                "fulano",
                "1991-09-24",
                null),
            download);
    }

    [Fact]
    public async Task GetDownloads_FallsBackToTheArtistNameWithoutACredit()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await SeedAsync(factory, Request("g1", DownloadState.Waiting));

        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

        Assert.Equal("Nirvana", Assert.Single(downloads!).Credit);
    }

    [Fact]
    public async Task CancelDownload_OfAMissingRequest_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync("/api/downloads/123/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelDownload_StopsAWaitingRequestForGood()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Waiting));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("cancelled", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task CancelDownload_OfADownloadingRequest_MarksItCancelled()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Downloading));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("cancelled", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task CancelDownload_OfADownloadedRequest_Returns409()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Downloaded));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("downloaded", await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task CancelDownload_StopsAnUpgradeAndKeepsTheAlbumImported()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = Request("g1", DownloadState.Searching);
        request.AcceptedQuality = Qualities.Mp3320;
        await SeedAsync(factory, request);

        var response = await client.PostAsync($"/api/downloads/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");
        var download = Assert.Single(downloads!);
        Assert.Equal(("imported", null), (download.State, download.AcceptedQuality));
    }

    [Fact]
    public async Task RetryDownload_OfAMissingRequest_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync("/api/downloads/123/retry", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetryDownload_BringsAFailedRequestForwardToNow()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Failed, nextAttemptInDays: 7));
        var ticks = factory.Time.GetUtcNow().Ticks;
        var now = new DateTimeOffset(ticks - ticks % TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond, TimeSpan.Zero);
        factory.Time.SetUtcNow(now);

        var response = await client.PostAsync($"/api/downloads/{request.Id}/retry", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");
        var download = Assert.Single(downloads!);
        Assert.Equal("waiting", download.State);
        Assert.Equal(now, download.NextAttemptAt);
    }

    [Theory]
    [InlineData(DownloadState.Waiting)]
    [InlineData(DownloadState.NotFound)]
    public async Task RetryDownload_AcceptsRequestsWaitingForARetry(DownloadState state)
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", state, nextAttemptInDays: 7));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/retry", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("waiting", await StateAsync(client, request.Id));
    }

    [Theory]
    [InlineData(DownloadState.Searching, "searching")]
    [InlineData(DownloadState.Downloading, "downloading")]
    [InlineData(DownloadState.Downloaded, "downloaded")]
    [InlineData(DownloadState.Cancelled, "cancelled")]
    [InlineData(DownloadState.Importing, "importing")]
    [InlineData(DownloadState.Imported, "imported")]
    public async Task RetryDownload_RefusesRequestsNotWaitingForARetry(DownloadState state, string name)
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", state));

        var response = await client.PostAsync($"/api/downloads/{request.Id}/retry", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(name, await StateAsync(client, request.Id));
    }

    [Fact]
    public async Task GetCandidates_ListsWhatTheLastSearchFound()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.NotFound));
        await SeedCandidateAsync(factory, Candidate(request.Id, "fulano", rejection: null));
        await SeedCandidateAsync(factory, Candidate(request.Id, "faixas", rejection: "track-count"));

        var candidates = await client.GetFromJsonAsync<List<SourceCandidate>>($"/api/downloads/{request.Id}/candidates");

        Assert.NotNull(candidates);
        Assert.Equal(["fulano", "faixas"], candidates.Select(candidate => candidate.Username));
        var first = candidates[0];
        Assert.Equal(
            ("1991 - Nevermind", 12, "mp3", 320, true, 0, null),
            (first.Folder, first.FileCount, first.Format, first.BitRate, first.HasFreeUploadSlot, first.QueueLength, first.Rejection));
    }

    [Fact]
    public async Task GetCandidates_OfAMissingRequest_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.GetAsync("/api/downloads/123/candidates");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PickCandidate_PinsTheSourceAndWakesTheRequest()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Failed, nextAttemptInDays: 7));
        var candidate = await SeedCandidateAsync(factory, Candidate(request.Id, "fulano", rejection: "quality"));

        var response = await client.PostAsJsonAsync($"/api/downloads/{request.Id}/pick", new PickRequest(candidate.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("waiting", await StateAsync(client, request.Id));
        using var scope = factory.Services.CreateScope();
        var pinned = await scope.ServiceProvider.GetRequiredService<BuscarrDbContext>()
            .DownloadRequests.FindAsync(request.Id);
        Assert.Equal(("fulano", candidate.Directory), (pinned!.PinnedUsername, pinned.PinnedDirectory));
    }

    [Fact]
    public async Task PickCandidate_RefusesAFolderWithTheWrongTrackCount()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.NotFound));
        var candidate = await SeedCandidateAsync(factory, Candidate(request.Id, "faixas", rejection: "track-count"));

        var response = await client.PostAsJsonAsync($"/api/downloads/{request.Id}/pick", new PickRequest(candidate.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PickCandidate_WhileTheRequestIsBusy_Returns409()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.Downloading));
        var candidate = await SeedCandidateAsync(factory, Candidate(request.Id, "fulano", rejection: null));

        var response = await client.PostAsJsonAsync($"/api/downloads/{request.Id}/pick", new PickRequest(candidate.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PickCandidate_OfAnotherRequestsCandidate_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var request = await SeedAsync(factory, Request("g1", DownloadState.NotFound));
        var other = await SeedAsync(factory, Request("g2", DownloadState.NotFound));
        var candidate = await SeedCandidateAsync(factory, Candidate(other.Id, "fulano", rejection: null));

        var response = await client.PostAsJsonAsync($"/api/downloads/{request.Id}/pick", new PickRequest(candidate.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RequestAlbum_CreatesAWaitingRequestWithTheUsersPreferences()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new WishRequest(BuscarrApiFactory.NirvanaId, "g1"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");
        var download = Assert.Single(downloads!);
        Assert.Equal(
            ("Nevermind", 12, "mp3-320", "waiting"),
            (download.Title, download.TrackCount, download.Quality, download.State));
        using var scope = factory.Services.CreateScope();
        var request = await scope.ServiceProvider.GetRequiredService<BuscarrDbContext>()
            .DownloadRequests.SingleAsync();
        Assert.Equal((true, "r1", "Nirvana"), (request.Requested, request.EditionReleaseId, request.ArtistName));
    }

    [Fact]
    public async Task RequestAlbum_MarksAnExistingRequestInstead()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        await SeedAsync(factory, Request("g1", DownloadState.NotFound));

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new WishRequest(BuscarrApiFactory.NirvanaId, "g1"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var request = await scope.ServiceProvider.GetRequiredService<BuscarrDbContext>()
            .DownloadRequests.SingleAsync();
        Assert.Equal((true, DownloadState.NotFound), (request.Requested, request.State));
    }

    [Fact]
    public async Task RequestAlbum_RevivesACancelledRequest()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();
        var cancelled = await SeedAsync(factory, Request("g1", DownloadState.Cancelled, nextAttemptInDays: 7));

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new WishRequest(BuscarrApiFactory.NirvanaId, "g1"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("waiting", await StateAsync(client, cancelled.Id));
    }

    [Fact]
    public async Task RequestAlbum_OfAnAlbumTheArtistDoesNotHave_Returns404()
    {
        await using var factory = new BuscarrApiFactory();
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/downloads",
            new WishRequest(BuscarrApiFactory.NirvanaId, "g-nao-existe"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Events_WithoutLogin_Returns401()
    {
        await using var factory = new BuscarrApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/downloads/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_StreamTheSnapshotAndThenEachChange()
    {
        await using var factory = new BuscarrApiFactory();
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

    private static SearchCandidate Candidate(int requestId, string username, string? rejection) =>
        new()
        {
            RequestId = requestId,
            Username = username,
            Directory = @"@@musica\Nirvana\1991 - Nevermind",
            FileCount = 12,
            Format = "mp3",
            BitRate = 320,
            HasFreeUploadSlot = true,
            QueueLength = 0,
            Rejection = rejection,
        };

    private static async Task<SearchCandidate> SeedCandidateAsync(BuscarrApiFactory factory, SearchCandidate candidate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        db.SearchCandidates.Add(candidate);
        await db.SaveChangesAsync();

        return candidate;
    }

    private static async Task<string> StateAsync(HttpClient client, int id)
    {
        var downloads = await client.GetFromJsonAsync<List<DownloadItem>>("/api/downloads");

        return downloads!.Single(download => download.Id == id).State;
    }
}
