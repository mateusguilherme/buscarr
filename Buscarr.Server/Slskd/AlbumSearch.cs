namespace Buscarr.Server.Slskd;

public enum AlbumSearchOutcome
{
    Found,
    NoMatchingFolder,
    NothingFound,
    Busy,
    Unfinished,
}

public record AlbumSearchResult(
    AlbumSearchOutcome Outcome,
    string SearchText,
    List<SoulseekFolder> Folders,
    List<FolderCandidate> Candidates);

public sealed class AlbumSearch(SlskdClient slskd, TimeProvider time)
{
    private const int StartAttempts = 3;
    private static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    public async Task<AlbumSearchResult> SearchAsync(
        string artist,
        string album,
        int trackCount,
        string quality,
        string? fallbackQuality = null)
    {
        var text = SearchText.For(artist, album);
        var result = await SearchOnceAsync(text, trackCount, quality, fallbackQuality);
        var wildcard = SearchText.Wildcard(text);

        if (result.Outcome != AlbumSearchOutcome.NothingFound || wildcard == text)
        {
            return result;
        }

        return await SearchOnceAsync(wildcard, trackCount, quality, fallbackQuality);
    }

    private async Task<AlbumSearchResult> SearchOnceAsync(
        string text,
        int trackCount,
        string quality,
        string? fallbackQuality)
    {
        var id = await StartAsync(text);

        if (id is null)
        {
            return new(AlbumSearchOutcome.Busy, text, [], []);
        }

        if (!await EndedWellAsync(id.Value))
        {
            return new(AlbumSearchOutcome.Unfinished, text, [], []);
        }

        var responses = await slskd.SearchResponsesAsync(id.Value);

        if (responses.Count == 0)
        {
            return new(AlbumSearchOutcome.NothingFound, text, [], []);
        }

        var candidates = Folders.Candidates(responses, trackCount, quality, fallbackQuality);
        var folders = candidates
            .Where(candidate => candidate.Rejection is null)
            .Select(candidate => candidate.Folder)
            .ToList();
        var outcome = folders.Count > 0 ? AlbumSearchOutcome.Found : AlbumSearchOutcome.NoMatchingFolder;

        return new(outcome, text, folders, candidates);
    }

    private async Task<Guid?> StartAsync(string text)
    {
        for (var attempt = 1; ; attempt++)
        {
            var id = await slskd.StartSearchAsync(text);

            if (id is not null || attempt == StartAttempts)
            {
                return id;
            }

            await Task.Delay(BusyWait, time);
        }
    }

    private async Task<bool> EndedWellAsync(Guid id)
    {
        var started = time.GetTimestamp();

        while (true)
        {
            var search = await slskd.SearchAsync(id);

            if (search.IsComplete)
            {
                return !search.State.Contains("Errored") && !search.State.Contains("Cancelled");
            }

            if (time.GetElapsedTime(started) >= MaxWait)
            {
                return false;
            }

            await Task.Delay(PollInterval, time);
        }
    }
}
