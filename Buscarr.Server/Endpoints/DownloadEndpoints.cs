using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Users;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record DownloadItem(
    int Id,
    string ArtistName,
    string Credit,
    string Title,
    string Type,
    string? CoverUrl,
    int TrackCount,
    string Quality,
    string State,
    string? Failure,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    int TracksDone,
    long BytesTransferred,
    long BytesTotal,
    int? PlaceInQueue,
    string? Username,
    string? FirstReleaseDate,
    string? AcceptedQuality);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record SourceCandidate(
    int Id,
    string Username,
    string Folder,
    int FileCount,
    string Format,
    int? BitRate,
    bool HasFreeUploadSlot,
    int QueueLength,
    string? Rejection);

public record PickRequest(int CandidateId);

public record WishRequest(string ArtistId, string ReleaseGroupId);

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/downloads").RequireAuthorization();

        group.MapGet("/", GetDownloads);
        group.MapPost("/", RequestAlbum);
        group.MapPost("/{id:int}/cancel", CancelDownload);
        group.MapPost("/{id:int}/retry", RetryDownload);
        group.MapGet("/{id:int}/candidates", GetCandidates);
        group.MapPost("/{id:int}/pick", PickCandidate);
        group.MapGet("/events", StreamEvents);
    }

    private static Task<List<DownloadItem>> GetDownloads(BuscarrDbContext db) => ItemsAsync(db);

    private static async Task<IResult> RequestAlbum(
        BuscarrDbContext db,
        MusicBrainzClient musicBrainz,
        UserStore users,
        TimeProvider time,
        ClaimsPrincipal user,
        WishRequest wish)
    {
        List<Album> albums;
        Artist artist;

        try
        {
            albums = await musicBrainz.ReleasesAsync(ArtistEndpoints.Contact(user), wish.ArtistId);
            artist = await musicBrainz.ArtistAsync(ArtistEndpoints.Contact(user), wish.ArtistId);
        }
        catch (Exception e) when (ArtistEndpoints.IsUpstreamError(e))
        {
            return ArtistEndpoints.BadGateway(e);
        }

        var album = albums.FirstOrDefault(a => a.First.ReleaseGroup?.Id == wish.ReleaseGroupId);

        if (album is null)
        {
            return Results.NotFound();
        }

        var edition = await users.EditionPreferenceAsync(user.UserId());
        var quality = await users.QualityPreferenceAsync(user.UserId());
        var chosen = Editions.Choose(album.Editions, edition);

        if (chosen.TrackCount == 0)
        {
            return Results.Conflict();
        }

        var existing = await db.DownloadRequests.FirstOrDefaultAsync(request =>
            request.ReleaseGroupId == wish.ReleaseGroupId
            && request.TrackCount == chosen.TrackCount
            && request.Quality == quality);

        if (existing is not null)
        {
            existing.Requested = true;

            if (existing.State == DownloadState.Cancelled)
            {
                existing.State = DownloadState.Waiting;
                existing.NextAttemptAt = time.GetUtcNow();
            }

            await db.SaveChangesAsync();

            return Results.NoContent();
        }

        var firstReleaseDate = album.First.ReleaseGroup!.FirstReleaseDate ?? album.First.Date;
        db.DownloadRequests.Add(new DownloadRequest
        {
            ArtistId = wish.ArtistId,
            ArtistName = artist.Name,
            Credit = album.First.CreditedAs ?? artist.Name,
            ReleaseGroupId = wish.ReleaseGroupId,
            Title = album.First.Title,
            Type = album.First.Category ?? "album",
            FirstReleaseDate = firstReleaseDate,
            EditionReleaseId = chosen.Id,
            TrackCount = chosen.TrackCount,
            Quality = quality,
            CoverUrl = chosen.CoverUrl ?? album.First.CoverUrl,
            Requested = true,
            State = DownloadState.Waiting,
            NextAttemptAt = RetrySchedule.FirstAttempt(time.GetUtcNow(), firstReleaseDate),
        });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> CancelDownload(BuscarrDbContext db, DownloadRunner runner, int id)
    {
        var request = await db.DownloadRequests.FindAsync(id);

        if (request is null)
        {
            return Results.NotFound();
        }

        if (request.AcceptedQuality is not null
            && request.State is DownloadState.Searching or DownloadState.Downloading)
        {
            if (request.State == DownloadState.Searching)
            {
                request.State = DownloadState.Imported;
                request.AcceptedQuality = null;
                await db.SaveChangesAsync();
            }
            else
            {
                runner.Cancel(id);
            }

            return Results.NoContent();
        }

        if (request.State
            is DownloadState.Downloaded
            or DownloadState.Cancelled
            or DownloadState.Importing
            or DownloadState.Imported)
        {
            return Results.Conflict();
        }

        var downloading = request.State == DownloadState.Downloading;
        request.State = DownloadState.Cancelled;
        await db.SaveChangesAsync();

        if (downloading)
        {
            runner.Cancel(id);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RetryDownload(BuscarrDbContext db, TimeProvider time, int id)
    {
        var request = await db.DownloadRequests.FindAsync(id);

        if (request is null)
        {
            return Results.NotFound();
        }

        if (request.State is not (DownloadState.Waiting or DownloadState.NotFound or DownloadState.Failed))
        {
            return Results.Conflict();
        }

        request.State = DownloadState.Waiting;
        request.NextAttemptAt = time.GetUtcNow();
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetCandidates(BuscarrDbContext db, int id)
    {
        if (await db.DownloadRequests.FindAsync(id) is null)
        {
            return Results.NotFound();
        }

        var candidates = await db.SearchCandidates
            .AsNoTracking()
            .Where(candidate => candidate.RequestId == id)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync();

        return Results.Json(candidates
            .Select(candidate => new SourceCandidate(
                candidate.Id,
                candidate.Username,
                Name(candidate.Directory),
                candidate.FileCount,
                candidate.Format,
                candidate.BitRate,
                candidate.HasFreeUploadSlot,
                candidate.QueueLength,
                candidate.Rejection))
            .ToList());
    }

    private static async Task<IResult> PickCandidate(BuscarrDbContext db, TimeProvider time, int id, PickRequest pick)
    {
        var request = await db.DownloadRequests.FindAsync(id);

        if (request is null)
        {
            return Results.NotFound();
        }

        if (request.State is not (DownloadState.Waiting or DownloadState.NotFound or DownloadState.Failed))
        {
            return Results.Conflict();
        }

        var candidate = await db.SearchCandidates.FindAsync(pick.CandidateId);

        if (candidate is null || candidate.RequestId != id)
        {
            return Results.NotFound();
        }

        if (candidate.Rejection == "track-count")
        {
            return Results.Conflict();
        }

        request.PinnedUsername = candidate.Username;
        request.PinnedDirectory = candidate.Directory;
        request.State = DownloadState.Waiting;
        request.NextAttemptAt = time.GetUtcNow();
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static string Name(string directory) =>
        directory[(directory.LastIndexOfAny(['\\', '/']) + 1)..];

    private static readonly TimeSpan EventInterval = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web);

    private static async Task StreamEvents(HttpContext context, IServiceScopeFactory scopes, TimeProvider time)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        string? last = null;

        try
        {
            while (true)
            {
                string snapshot;

                using (var scope = scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
                    snapshot = JsonSerializer.Serialize(await ItemsAsync(db), EventJson);
                }

                if (snapshot != last)
                {
                    last = snapshot;
                    await context.Response.WriteAsync($"data: {snapshot}\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }

                await Task.Delay(EventInterval, time, context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<List<DownloadItem>> ItemsAsync(BuscarrDbContext db)
    {
        var requests = await db.DownloadRequests.AsNoTracking().ToListAsync();

        return [.. requests
            .OrderBy(Rank)
            .ThenBy(request => request.NextAttemptAt)
            .ThenBy(request => request.Id)
            .Select(Item)];
    }

    private static int Rank(DownloadRequest request) =>
        request.State switch
        {
            DownloadState.Downloading or DownloadState.Importing => 0,
            DownloadState.Searching => 1,
            DownloadState.Waiting or DownloadState.NotFound or DownloadState.Failed => 2,
            DownloadState.Downloaded or DownloadState.Imported => 3,
            _ => 4,
        };

    private static DownloadItem Item(DownloadRequest request) =>
        new(
            request.Id,
            request.ArtistName,
            request.Credit ?? request.ArtistName,
            request.Title,
            request.Type,
            request.CoverUrl,
            request.TrackCount,
            request.Quality,
            StateName(request.State),
            request.Failure,
            request.Attempts,
            request.NextAttemptAt,
            request.TracksDone,
            request.BytesTransferred,
            request.BytesTotal,
            request.PlaceInQueue,
            request.Username,
            request.FirstReleaseDate,
            request.AcceptedQuality);

    private static string StateName(DownloadState state) =>
        string.Concat(state.ToString().Select((letter, index) =>
            char.IsUpper(letter) && index > 0
                ? $"-{char.ToLowerInvariant(letter)}"
                : $"{char.ToLowerInvariant(letter)}"));
}
