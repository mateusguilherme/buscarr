using Buscarr.Server.Data;
using Buscarr.Server.Slskd;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Downloads;

public sealed record SearchedRequest(int RequestId, List<SoulseekFolder> Folders);

public sealed class SearchQueue(BuscarrDbContext db, AlbumSearch search, TimeProvider time)
{
    public async Task RecoverAsync()
    {
        var now = time.GetUtcNow();
        var inFlight = await db.DownloadRequests
            .Where(r => r.State == DownloadState.Searching || r.State == DownloadState.Downloading)
            .ToListAsync();

        foreach (var request in inFlight)
        {
            request.State = DownloadState.Waiting;
            request.NextAttemptAt = now;
        }

        var importing = await db.DownloadRequests
            .Where(r => r.State == DownloadState.Importing)
            .ToListAsync();

        foreach (var request in importing)
        {
            request.State = DownloadState.Downloaded;
        }

        await db.SaveChangesAsync();
    }

    public async Task<SearchedRequest?> ProcessNextAsync()
    {
        var now = time.GetUtcNow();
        var request = await db.DownloadRequests
            .Where(r =>
                (r.State == DownloadState.Waiting || r.State == DownloadState.NotFound || r.State == DownloadState.Failed)
                && r.NextAttemptAt <= now)
            .OrderBy(r => r.NextAttemptAt)
            .FirstOrDefaultAsync();

        if (request is null)
        {
            return null;
        }

        request.State = DownloadState.Searching;
        await db.SaveChangesAsync();

        AlbumSearchResult? result = null;

        try
        {
            result = await search.SearchAsync(request.ArtistName, request.Title, request.TrackCount, request.Quality);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar {request.ArtistName} {request.Title}: {e.Message}");
        }

        await db.Entry(request).ReloadAsync();

        if (request.State != DownloadState.Searching)
        {
            return new SearchedRequest(request.Id, []);
        }

        if (result is null)
        {
            PutBack(request, RetrySchedule.AfterBusy(time.GetUtcNow()));
            await db.SaveChangesAsync();

            return new SearchedRequest(request.Id, []);
        }

        var folders = await WithoutFailedSourcesAsync(request.Id, result.Folders);
        var outcome = result.Outcome == AlbumSearchOutcome.Found && folders.Count == 0
            ? AlbumSearchOutcome.NoMatchingFolder
            : result.Outcome;

        switch (outcome)
        {
            case AlbumSearchOutcome.Found:
                request.State = DownloadState.Downloading;
                request.Failure = null;
                break;
            case AlbumSearchOutcome.NothingFound:
                NotFound(request, "nothing-found");
                break;
            case AlbumSearchOutcome.NoMatchingFolder:
                NotFound(request, "no-matching-folder");
                break;
            case AlbumSearchOutcome.Busy:
                PutBack(request, RetrySchedule.AfterBusy(time.GetUtcNow()));
                break;
            case AlbumSearchOutcome.Unfinished:
                PutBack(request, RetrySchedule.AfterUnfinished(time.GetUtcNow()));
                break;
        }

        await db.SaveChangesAsync();

        return new SearchedRequest(request.Id, folders);
    }

    private async Task<List<SoulseekFolder>> WithoutFailedSourcesAsync(int requestId, List<SoulseekFolder> folders)
    {
        if (folders.Count == 0)
        {
            return folders;
        }

        var failed = await db.FailedSources.Where(source => source.RequestId == requestId).ToListAsync();

        return [.. folders.Where(folder =>
            !failed.Any(source => source.Username == folder.Username && source.Directory == folder.Directory))];
    }

    private void NotFound(DownloadRequest request, string failure)
    {
        request.State = DownloadState.NotFound;
        request.Failure = failure;
        request.Attempts++;
        request.NextAttemptAt = RetrySchedule.NextRetry(time.GetUtcNow(), request.Attempts);
    }

    private static void PutBack(DownloadRequest request, DateTimeOffset nextAttemptAt)
    {
        request.State = DownloadState.Waiting;
        request.NextAttemptAt = nextAttemptAt;
    }
}
