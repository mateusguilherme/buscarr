using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

public sealed class MusicBrainzClient(HttpClient http, TimeProvider time)
{
    private const string UserAgentProduct = "buscarr/0.1.0";
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CacheLife = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly ConcurrentDictionary<string, (long Timestamp, object Value)> _cache = new();
    private long? _lastRequest;

    public async Task<List<Artist>> FindArtistsAsync(string contact, string query)
    {
        var search = await GetAsync<ArtistSearch>(
            contact,
            "artist",
            [("query", query), ("dismax", "true"), ("limit", "10")]);
        var name = Names.Normalize(query);

        return search.Artists
            .Where(artist => Names.Normalize(artist.Name).Contains(name))
            .ToList();
    }

    public Task<Artist> ArtistAsync(string contact, string id, bool fresh = false) =>
        GetAsync<Artist>(contact, $"artist/{id}", [("inc", "url-rels")], fresh);

    public async Task<List<Album>> ReleasesAsync(string contact, string artistId)
    {
        var releases = new List<Release>();

        while (true)
        {
            List<(string, string)> parameters =
            [
                ("artist", artistId),
                ("status", "official"),
                ("inc", "release-groups media artist-credits"),
                ("limit", "100"),
                ("offset", releases.Count.ToString()),
            ];

            var page = await GetAsync<ReleaseBrowse>(contact, "release", parameters);
            releases.AddRange(page.Releases);

            if (page.Releases.Count == 0 || releases.Count >= page.ReleaseCount)
            {
                break;
            }
        }

        var albums = releases
            .Where(release => release.ReleaseGroup?.Id is not null)
            .GroupBy(release => release.ReleaseGroup!.Id)
            .Select(group => (
                First: group.Where(release => release.IsOriginalRelease).MinBy(release => release.Date, StringComparer.Ordinal)
                    ?? group.MinBy(release => release.Date ?? "9999", StringComparer.Ordinal)!,
                Editions: group.ToList()))
            .Where(album => album.First.Category is not null)
            .Select(album => new Album(album.First, album.Editions));

        return albums
            .GroupBy(album => (album.First.Category, Names.Normalize(album.First.Title)))
            .Select(group => group.MinBy(album => album.First.Date, StringComparer.Ordinal)!)
            .OrderBy(album => album.First.Date, StringComparer.Ordinal)
            .ThenBy(album => Names.Normalize(album.First.Title), StringComparer.Ordinal)
            .ToList();
    }

    public async Task<EditionTracks> ReleaseTracksAsync(string contact, string id)
    {
        var release = await GetAsync<Release>(
            contact,
            $"release/{id}",
            [("inc", "recordings artist-credits release-groups media")]);
        var tracks = release.Media
            .OrderBy(medium => medium.Position)
            .SelectMany(medium => medium.Tracks.OrderBy(track => track.Position))
            .Select((track, index) => new ReleaseTrack(
                index + 1,
                track.Title,
                track.Length ?? track.Recording?.Length,
                track.Recording?.Id))
            .ToList();

        return new(release.ArtistCredit.FirstOrDefault()?.Artist?.Id, release.ReleaseGroup?.Id, tracks);
    }

    private async Task<T> GetAsync<T>(
        string contact,
        string path,
        IEnumerable<(string Key, string Value)> parameters,
        bool fresh = false)
    {
        var query = string.Join(
            "&",
            parameters
                .Prepend((Key: "fmt", Value: "json"))
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var url = $"https://musicbrainz.org/ws/2/{path}?{query}";
        var userAgent = $"{UserAgentProduct} ( {contact} )";

        if (!fresh && _cache.TryGetValue(url, out var cached) && time.GetElapsedTime(cached.Timestamp) < CacheLife)
        {
            return (T)cached.Value;
        }

        HttpResponseMessage response;
        var attempts = 0;

        while (true)
        {
            attempts++;
            await WaitTurnAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            response = await http.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.ServiceUnavailable || attempts == MaxAttempts)
            {
                break;
            }

            response.Dispose();
            await Console.Error.WriteLineAsync($"MusicBrainz ocupado (503), tentativa {attempts} de {MaxAttempts}");
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var value = await response.Content.ReadFromJsonAsync<T>()
                ?? throw new JsonException($"resposta vazia do MusicBrainz em {path}");
            _cache[url] = (time.GetTimestamp(), value);

            return value;
        }
    }

    private async Task WaitTurnAsync()
    {
        await _turn.WaitAsync();

        try
        {
            if (_lastRequest is { } last)
            {
                var wait = Interval - time.GetElapsedTime(last);

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, time);
                }
            }

            _lastRequest = time.GetTimestamp();
        }
        finally
        {
            _turn.Release();
        }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class ArtistSearch
    {
        public List<Artist> Artists { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class ReleaseBrowse
    {
        [JsonPropertyName("release-count")]
        public int ReleaseCount { get; init; }

        public List<Release> Releases { get; init; } = [];
    }
}
