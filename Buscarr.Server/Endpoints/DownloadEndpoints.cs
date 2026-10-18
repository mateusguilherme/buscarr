using System.Text.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record DownloadItem(
    int Id,
    string ArtistName,
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
    string? Username);

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/downloads").RequireAuthorization();

        group.MapGet("/", GetDownloads);
        group.MapPost("/{id:int}/cancel", CancelDownload);
        group.MapPost("/{id:int}/retry", RetryDownload);
        group.MapGet("/events", StreamEvents);
    }

    private static Task<List<DownloadItem>> GetDownloads(BuscarrDbContext db) => ItemsAsync(db);

    private static async Task<IResult> CancelDownload(BuscarrDbContext db, DownloadRunner runner, int id)
    {
        var request = await db.DownloadRequests.FindAsync(id);

        if (request is null)
        {
            return Results.NotFound();
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
            request.Username);

    private static string StateName(DownloadState state) =>
        string.Concat(state.ToString().Select((letter, index) =>
            char.IsUpper(letter) && index > 0
                ? $"-{char.ToLowerInvariant(letter)}"
                : $"{char.ToLowerInvariant(letter)}"));
}
