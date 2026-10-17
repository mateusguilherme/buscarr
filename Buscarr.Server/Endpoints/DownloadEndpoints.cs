using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

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
    }

    private static Task<List<DownloadItem>> GetDownloads(BuscarrDbContext db) => ItemsAsync(db);

    private static async Task<IResult> CancelDownload(BuscarrDbContext db, DownloadRunner runner, int id)
    {
        var request = await db.DownloadRequests.FindAsync(id);

        if (request is null)
        {
            return Results.NotFound();
        }

        if (request.State is DownloadState.Downloaded or DownloadState.Cancelled)
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

    internal static async Task<List<DownloadItem>> ItemsAsync(BuscarrDbContext db)
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
            DownloadState.Downloading => 0,
            DownloadState.Searching => 1,
            DownloadState.Waiting or DownloadState.NotFound or DownloadState.Failed => 2,
            DownloadState.Downloaded => 3,
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
