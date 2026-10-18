using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Import;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record QuarantineItem(
    int Id,
    string ArtistName,
    string Title,
    string Reason,
    string? Username,
    DateTimeOffset At,
    bool CanImport);

public static class QuarantineEndpoints
{
    public static void MapQuarantineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quarantine").RequireAuthorization();

        group.MapGet("/", GetQuarantine);
        group.MapPost("/{id:int}/import", ImportQuarantined);
        group.MapDelete("/{id:int}", DiscardQuarantined);
    }

    private static async Task<List<QuarantineItem>> GetQuarantine(BuscarrDbContext db, ImportPipeline pipeline)
    {
        var albums = await db.QuarantinedAlbums.AsNoTracking().ToListAsync();

        return [.. albums
            .OrderByDescending(album => album.At)
            .ThenByDescending(album => album.Id)
            .Select(album => new QuarantineItem(
                album.Id,
                album.ArtistName,
                album.Title,
                album.Reason,
                album.Username,
                album.At,
                album.RequestId is not null && pipeline.Enabled && Directory.Exists(album.Path)))];
    }

    private static async Task<IResult> ImportQuarantined(
        BuscarrDbContext db,
        ImportPipeline pipeline,
        DownloadRunner runner,
        int id)
    {
        var quarantined = await db.QuarantinedAlbums.FindAsync(id);

        if (quarantined is null)
        {
            return Results.NotFound();
        }

        if (!pipeline.Enabled || quarantined.RequestId is null)
        {
            return Results.Conflict();
        }

        var request = await db.DownloadRequests.FindAsync(quarantined.RequestId.Value);

        if (request is null)
        {
            return Results.Conflict();
        }

        if (request.State
            is not (DownloadState.Waiting or DownloadState.NotFound or DownloadState.Failed or DownloadState.Cancelled))
        {
            return Results.Conflict();
        }

        if (!Directory.Exists(quarantined.Path))
        {
            return Results.Conflict();
        }

        request.State = DownloadState.Importing;
        await db.SaveChangesAsync();
        runner.StartQuarantineImport(request.Id, quarantined.Id);

        return Results.NoContent();
    }

    private static async Task<IResult> DiscardQuarantined(BuscarrDbContext db, int id)
    {
        var quarantined = await db.QuarantinedAlbums.FindAsync(id);

        if (quarantined is null)
        {
            return Results.NotFound();
        }

        db.QuarantinedAlbums.Remove(quarantined);
        await db.SaveChangesAsync();
        DeleteFiles(quarantined.Path);

        return Results.NoContent();
    }

    private static void DeleteFiles(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            File.Delete($"{path}.reason.txt");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao apagar a quarentena {path}: {e.Message}");
        }
    }
}
