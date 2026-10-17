using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Buscarr.Server.Settings;

namespace Buscarr.Server.Subsonic;

public record SubsonicAlbum(string Id, string Name, string Artist, int SongCount, string? MusicBrainzId);

public sealed class SubsonicClient(HttpClient http, Func<ServerSettings> settings)
{
    private const string UserAgent = "buscarr/0.1.0";
    private const int PageSize = 500;

    public SubsonicClient(HttpClient http, string? url, string? user, string? password)
        : this(http, () => new ServerSettings { SubsonicUrl = url, SubsonicUser = user, SubsonicPassword = password })
    {
    }

    public bool IsConfigured => IsComplete(settings());

    public async Task PingAsync()
    {
        await GetAsync("ping", []);
    }

    public async Task<List<SubsonicAlbum>> AlbumsAsync()
    {
        var albums = new List<SubsonicAlbum>();

        while (true)
        {
            var body = await GetAsync(
                "getAlbumList2",
                [("type", "alphabeticalByName"), ("size", PageSize.ToString()), ("offset", albums.Count.ToString())]);
            var page = body.AlbumList2?.Album ?? [];
            albums.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return albums;
    }

    private async Task<SubsonicBody> GetAsync(string path, IEnumerable<(string Key, string Value)> parameters)
    {
        var current = settings();

        if (!IsComplete(current))
        {
            throw new InvalidOperationException("servidor de música sem endereço, usuário ou senha");
        }

        var salt = RandomNumberGenerator.GetHexString(16, lowercase: true);
        var token = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(current.SubsonicPassword + salt)));
        var query = string.Join(
            "&",
            parameters
                .Concat(
                [
                    (Key: "u", Value: current.SubsonicUser!),
                    (Key: "t", Value: token),
                    (Key: "s", Value: salt),
                    (Key: "v", Value: "1.16.1"),
                    (Key: "c", Value: "buscarr"),
                    (Key: "f", Value: "json"),
                ])
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{current.SubsonicUrl!.TrimEnd('/')}/rest/{path}?{query}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<SubsonicEnvelope>()
            ?? throw new JsonException($"resposta vazia do servidor de música em {path}");
        var body = envelope.Response
            ?? throw new JsonException($"resposta sem subsonic-response do servidor de música em {path}");

        if (body.Status != "ok")
        {
            throw new HttpRequestException($"servidor de música: {body.Error?.Message ?? body.Status}");
        }

        return body;
    }

    private static bool IsComplete(ServerSettings current) =>
        !string.IsNullOrEmpty(current.SubsonicUrl)
        && !string.IsNullOrEmpty(current.SubsonicUser)
        && !string.IsNullOrEmpty(current.SubsonicPassword);

    private sealed class SubsonicEnvelope
    {
        [JsonPropertyName("subsonic-response")]
        public SubsonicBody? Response { get; init; }
    }

    private sealed class SubsonicBody
    {
        public string Status { get; init; } = "";

        public SubsonicError? Error { get; init; }

        public SubsonicAlbumList? AlbumList2 { get; init; }
    }

    private sealed class SubsonicError
    {
        public string? Message { get; init; }
    }

    private sealed class SubsonicAlbumList
    {
        public List<SubsonicAlbum> Album { get; init; } = [];
    }
}
