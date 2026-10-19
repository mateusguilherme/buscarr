using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Settings;
using JetBrains.Annotations;

namespace Buscarr.Server.Spotify;

public sealed class SpotifyClient(HttpClient http, Func<ServerSettings> settings, TimeProvider time)
{
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string SearchUrl = "https://api.spotify.com/v1/search";
    private const string ArtistsUrl = "https://api.spotify.com/v1/artists";
    private const string AlbumArtAvatar = "/image/ab67616d";
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private (string Value, long Timestamp, TimeSpan Life)? _token;

    public SpotifyClient(HttpClient http, string? clientId, string? clientSecret, TimeProvider time)
        : this(http, () => new ServerSettings { SpotifyClientId = clientId, SpotifyClientSecret = clientSecret }, time)
    {
    }

    public bool IsConfigured =>
        settings() is { SpotifyClientId.Length: > 0, SpotifyClientSecret.Length: > 0 };

    public async Task PingAsync()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Spotify sem credenciais");
        }

        await TokenAsync();
    }

    public async Task<string?> AlbumCoverAsync(string artist, string album)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var query = Uri.EscapeDataString($"artist:\"{Term(artist)}\" album:\"{Term(album)}\"");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{SearchUrl}?q={query}&type=album&limit=10");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var search = await response.Content.ReadFromJsonAsync<AlbumSearch>();
            var match = search?.Albums?.Items.FirstOrDefault(item =>
                Names.Normalize(item.Name) == Names.Normalize(album)
                && item.Artists.Any(credited => Names.Normalize(credited.Name) == Names.Normalize(artist)));

            return match is null ? null : Cover(match);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a capa de {artist} - {album} no Spotify: {e.Message}");

            return null;
        }
    }

    public async Task<string?> AlbumCoverByBarcodeAsync(string barcode)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var query = Uri.EscapeDataString($"upc:{barcode}");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{SearchUrl}?q={query}&type=album&limit=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var search = await response.Content.ReadFromJsonAsync<AlbumSearch>();
            var match = search?.Albums?.Items.FirstOrDefault();

            return match is null ? null : Cover(match);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a capa do código {barcode} no Spotify: {e.Message}");

            return null;
        }
    }

    public async Task<(string Url, int Followers)?> ArtistImageAsync(string id)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ArtistsUrl}/{Uri.EscapeDataString(id)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var artist = await response.Content.ReadFromJsonAsync<ArtistProfile>();
            var url = artist is null ? null : Large(artist.Images);

            return url is null || url.Contains(AlbumArtAvatar, StringComparison.Ordinal)
                ? null
                : (url, artist!.Followers?.Total ?? 0);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a foto do artista {id} no Spotify: {e.Message}");

            return null;
        }
    }

    private static string? Cover(AlbumItem match) => Smallest(match.Images);

    private static string? Smallest(List<AlbumImage> images)
    {
        var sorted = images.OrderByDescending(image => image.Width).ToList();

        return (sorted.FirstOrDefault(image => image.Width <= 300) ?? sorted.LastOrDefault())?.Url;
    }

    private static string? Large(List<AlbumImage> images)
    {
        var sorted = images.OrderBy(image => image.Width).ToList();

        return (sorted.FirstOrDefault(image => image.Width >= 500) ?? sorted.LastOrDefault())?.Url;
    }

    private async Task<string> TokenAsync()
    {
        if (_token is { } token && time.GetElapsedTime(token.Timestamp) < token.Life - ExpiryMargin)
        {
            return token.Value;
        }

        var current = settings();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{current.SpotifyClientId}:{current.SpotifyClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);

        using var response = await http.SendAsync(request);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("credenciais inválidas");
        }

        response.EnsureSuccessStatusCode();
        var granted = await response.Content.ReadFromJsonAsync<TokenGrant>()
            ?? throw new HttpRequestException("resposta vazia do Spotify");
        _token = (granted.AccessToken, time.GetTimestamp(), TimeSpan.FromSeconds(granted.ExpiresIn));

        return granted.AccessToken;
    }

    private static string Term(string value) => value.Replace("\"", "");

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class TokenGrant
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class ArtistProfile
    {
        public List<AlbumImage> Images { get; init; } = [];

        public FollowerCount? Followers { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class FollowerCount
    {
        public int Total { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumSearch
    {
        public AlbumPage? Albums { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumPage
    {
        public List<AlbumItem> Items { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumItem
    {
        public required string Name { get; init; }

        public List<AlbumImage> Images { get; init; } = [];

        public List<AlbumArtist> Artists { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumImage
    {
        public required string Url { get; init; }

        public int Width { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumArtist
    {
        public required string Name { get; init; }
    }
}
