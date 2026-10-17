namespace Buscarr.Server.Slskd;

public enum AlbumSearchOutcome
{
    Found,
    NoMatchingFolder,
    NothingFound,
    Busy,
    Unfinished,
}

public record AlbumSearchResult(AlbumSearchOutcome Outcome, string SearchText, List<SoulseekFolder> Folders);

public sealed class AlbumSearch(SlskdClient slskd, TimeProvider time)
{
    private const int StartAttempts = 3;
    private static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    public async Task<AlbumSearchResult> SearchAsync(string artist, string album, int trackCount, string quality)
    {
        var text = SearchText.For(artist, album);
        var result = await SearchOnceAsync(text, trackCount, quality);
        var wildcard = SearchText.Wildcard(text);

        if (result.Outcome != AlbumSearchOutcome.NothingFound || wildcard == text)
        {
            return result;
        }

        return await SearchOnceAsync(wildcard, trackCount, quality);
    }

    private async Task<AlbumSearchResult> SearchOnceAsync(string text, int trackCount, string quality)
    {
        var id = await StartAsync(text);

        if (id is null)
        {
            return new(AlbumSearchOutcome.Busy, text, []);
        }

        if (!await EndedWellAsync(id.Value))
        {
            return new(AlbumSearchOutcome.Unfinished, text, []);
        }

        var responses = await slskd.SearchResponsesAsync(id.Value);

        if (responses.Count == 0)
        {
            return new(AlbumSearchOutcome.NothingFound, text, []);
        }

        var folders = Folders.Choose(responses, trackCount, quality);
        var outcome = folders.Count > 0 ? AlbumSearchOutcome.Found : AlbumSearchOutcome.NoMatchingFolder;

        return new(outcome, text, folders);
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
