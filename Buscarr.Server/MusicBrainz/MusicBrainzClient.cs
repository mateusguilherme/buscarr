using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Buscarr.Server.MusicBrainz;

public sealed class MusicBrainzClient(HttpClient http, TimeProvider time)
{
    private const string UserAgentProduct = "buscarr/0.1.0";
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _turn = new(1, 1);
    private long? _lastRequest;

    public async Task<Artist?> FindArtistAsync(string contact, string query, string? country = null)
    {
        List<(string, string)> parameters = country is null
            ? [("query", query), ("dismax", "true"), ("limit", "10")]
            : [("query", $"({EscapeSearchSyntax(query.ToLowerInvariant())}) AND country:{country}"), ("limit", "10")];

        var search = await GetAsync<ArtistSearch>(contact, "artist", parameters);
        var q = Names.Normalize(query);

        return search.Artists.FirstOrDefault(artist => Names.Normalize(artist.Name).Contains(q));
    }

    public Task<Artist> ArtistAsync(string contact, string id) =>
        GetAsync<Artist>(contact, $"artist/{id}", []);

    public async Task<List<Release>> ReleasesAsync(string contact, string artistId, string? releaseType)
    {
        var releases = new List<Release>();

        while (true)
        {
            List<(string, string)> parameters =
            [
                ("artist", artistId),
                ("status", "official"),
                ("inc", "release-groups"),
                ("limit", "100"),
                ("offset", releases.Count.ToString()),
            ];

            if (releaseType is not null)
            {
                parameters.Add(("type", releaseType));
            }

            var page = await GetAsync<ReleaseBrowse>(contact, "release", parameters);
            releases.AddRange(page.Releases);

            if (page.Releases.Count == 0 || releases.Count >= page.ReleaseCount)
            {
                break;
            }
        }

        return releases
            .Where(release => release.IsOriginalRelease)
            .GroupBy(release => Names.Normalize(release.Title))
            .Select(group => group.OrderBy(release => release.Date, StringComparer.Ordinal).First())
            .OrderBy(release => release.Date, StringComparer.Ordinal)
            .ThenBy(release => Names.Normalize(release.Title), StringComparer.Ordinal)
            .ToList();
    }

    private async Task<T> GetAsync<T>(
        string contact,
        string path,
        IEnumerable<(string Key, string Value)> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Prepend((Key: "fmt", Value: "json"))
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var url = $"https://musicbrainz.org/ws/2/{path}?{query}";
        var userAgent = $"{UserAgentProduct} ( {contact} )";

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
            Console.Error.WriteLine($"MusicBrainz ocupado (503), tentativa {attempts} de {MaxAttempts}");
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new JsonException($"resposta vazia do MusicBrainz em {path}");
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

    private static string EscapeSearchSyntax(string text) =>
        Regex.Replace(text, @"[+\-&|!(){}\[\]^""~*?:\\/]", @"\$0");

    private sealed class ArtistSearch
    {
        public List<Artist> Artists { get; init; } = [];
    }

    private sealed class ReleaseBrowse
    {
        [JsonPropertyName("release-count")]
        public int ReleaseCount { get; init; }

        public List<Release> Releases { get; init; } = [];
    }
}
