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
            request.State = request.AcceptedQuality is null ? DownloadState.Waiting : DownloadState.Imported;
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

    private const int FallbackAttempts = 4;

    public async Task<SearchedRequest?> ProcessNextAsync()
    {
        var now = time.GetUtcNow();
        var request = await db.DownloadRequests
            .Where(r =>
                (r.State == DownloadState.Waiting
                    || r.State == DownloadState.NotFound
                    || r.State == DownloadState.Failed
                    || (r.State == DownloadState.Imported && r.AcceptedQuality != null))
                && r.NextAttemptAt <= now)
            .OrderBy(r => r.NextAttemptAt)
            .FirstOrDefaultAsync();

        if (request is null)
        {
            return null;
        }

        var upgrade = request.AcceptedQuality is not null;
        request.State = DownloadState.Searching;
        await db.SaveChangesAsync();

        AlbumSearchResult? result = null;

        try
        {
            result = await search.SearchAsync(
                request.ArtistName,
                request.Title,
                request.TrackCount,
                request.Quality,
                Fallback(request, upgrade));
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
            PutBack(request, RetrySchedule.AfterBusy(time.GetUtcNow()), upgrade);
            await db.SaveChangesAsync();

            return new SearchedRequest(request.Id, []);
        }

        var blocked = await db.FailedSources.Where(source => source.RequestId == request.Id).ToListAsync();
        var searchRan = result.Outcome
            is AlbumSearchOutcome.Found
            or AlbumSearchOutcome.NoMatchingFolder
            or AlbumSearchOutcome.NothingFound;

        if (searchRan)
        {
            await ReplaceCandidatesAsync(request.Id, result.Candidates, blocked);
        }

        List<SoulseekFolder> folders;
        AlbumSearchOutcome outcome;

        if (request.PinnedUsername is not null && searchRan)
        {
            var pinned = result.Candidates.FirstOrDefault(candidate =>
                candidate.Folder.Username == request.PinnedUsername
                && candidate.Folder.Directory == request.PinnedDirectory);
            folders = pinned is null ? [] : [Folders.Pin(pinned.Folder, request.Quality)];
            outcome = folders.Count > 0
                ? AlbumSearchOutcome.Found
                : result.Outcome == AlbumSearchOutcome.NothingFound
                    ? AlbumSearchOutcome.NothingFound
                    : AlbumSearchOutcome.NoMatchingFolder;
            request.PinnedUsername = null;
            request.PinnedDirectory = null;
        }
        else
        {
            folders = [.. result.Folders.Where(folder => !IsBlocked(folder, blocked))];
            outcome = result.Outcome == AlbumSearchOutcome.Found && folders.Count == 0
                ? AlbumSearchOutcome.NoMatchingFolder
                : result.Outcome;
        }

        switch (outcome)
        {
            case AlbumSearchOutcome.Found:
                request.State = DownloadState.Downloading;
                request.Failure = null;
                break;
            case AlbumSearchOutcome.NothingFound:
                NotFound(request, "nothing-found", upgrade);
                break;
            case AlbumSearchOutcome.NoMatchingFolder:
                NotFound(request, "no-matching-folder", upgrade);
                break;
            case AlbumSearchOutcome.Busy:
                PutBack(request, RetrySchedule.AfterBusy(time.GetUtcNow()), upgrade);
                break;
            case AlbumSearchOutcome.Unfinished:
                PutBack(request, RetrySchedule.AfterUnfinished(time.GetUtcNow()), upgrade);
                break;
        }

        await db.SaveChangesAsync();

        return new SearchedRequest(request.Id, folders);
    }

    private const int MaxCandidates = 20;

    private async Task ReplaceCandidatesAsync(int requestId, List<FolderCandidate> candidates, List<FailedSource> blocked)
    {
        var old = await db.SearchCandidates.Where(candidate => candidate.RequestId == requestId).ToListAsync();
        db.SearchCandidates.RemoveRange(old);

        foreach (var candidate in candidates.Take(MaxCandidates))
        {
            db.SearchCandidates.Add(new SearchCandidate
            {
                RequestId = requestId,
                Username = candidate.Folder.Username,
                Directory = candidate.Folder.Directory,
                FileCount = candidate.Folder.Files.Count,
                Format = Folders.Format(candidate.Folder.Files),
                BitRate = candidate.Folder.Files.Min(file => file.BitRate),
                HasFreeUploadSlot = candidate.HasFreeUploadSlot,
                QueueLength = candidate.QueueLength,
                Rejection = candidate.Rejection ?? (IsBlocked(candidate.Folder, blocked) ? "failed-before" : null),
            });
        }
    }

    private static bool IsBlocked(SoulseekFolder folder, List<FailedSource> blocked) =>
        blocked.Any(source => source.Username == folder.Username && source.Directory == folder.Directory);

    private static string? Fallback(DownloadRequest request, bool upgrade) =>
        !upgrade && request.Quality == Qualities.Flac && request.Attempts >= FallbackAttempts
            ? Qualities.Mp3320
            : null;

    private void NotFound(DownloadRequest request, string failure, bool upgrade)
    {
        if (upgrade)
        {
            request.State = DownloadState.Imported;
            request.NextAttemptAt = RetrySchedule.UpgradeRetry(time.GetUtcNow());

            return;
        }

        request.State = DownloadState.NotFound;
        request.Failure = failure;
        request.Attempts++;
        request.NextAttemptAt = RetrySchedule.NextRetry(time.GetUtcNow(), request.Attempts);
    }

    private static void PutBack(DownloadRequest request, DateTimeOffset nextAttemptAt, bool upgrade)
    {
        request.State = upgrade ? DownloadState.Imported : DownloadState.Waiting;
        request.NextAttemptAt = nextAttemptAt;
    }
}
