using System.Text.Json.Serialization;
using Buscarr.Server.MusicBrainz;
using JetBrains.Annotations;

namespace Buscarr.Server.Deezer;

public sealed class DeezerClient(HttpClient http, TimeProvider time)
{
    private const string SearchUrl = "https://api.deezer.com/search/album";
    private const string AlbumUrl = "https://api.deezer.com/album";
    private const string ArtistUrl = "https://api.deezer.com/artist";
    private const string PlaceholderHash = "d41d8cd98f00b204e9800998ecf8427e";
    private const int QuotaCode = 4;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan QuotaPause = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _turn = new(1, 1);
    private long? _lastRequest;

    public DeezerClient(HttpClient http)
        : this(http, TimeProvider.System)
    {
    }

    public async Task<(string Url, int Fans)?> ArtistImageAsync(string id)
    {
        try
        {
            var artist = await FetchAsync<DeezerArtist>($"{ArtistUrl}/{Uri.EscapeDataString(id)}");
            var picture = Biggest(artist?.PictureBig, artist?.PictureMedium);

            return picture is null ? null : (picture, artist!.Fans);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a foto do artista {id} no Deezer: {e.Message}");

            return null;
        }
    }

    public async Task<string?> AlbumCoverByBarcodeAsync(string barcode)
    {
        try
        {
            var album = await FetchAsync<UpcAlbum>($"{AlbumUrl}/upc:{Uri.EscapeDataString(barcode)}");

            return Biggest(album?.CoverBig, album?.CoverMedium);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a capa do código {barcode} no Deezer: {e.Message}");

            return null;
        }
    }

    public async Task<string?> AlbumCoverAsync(string artist, string album)
    {
        var query = Uri.EscapeDataString($"artist:\"{Term(artist)}\" album:\"{Term(album)}\"");

        try
        {
            var search = await FetchAsync<AlbumSearch>($"{SearchUrl}?q={query}");
            var match = search?.Albums
                .FirstOrDefault(result =>
                    Names.Normalize(result.Artist?.Name ?? "") == Names.Normalize(artist)
                    && Names.Normalize(result.Title) == Names.Normalize(album));

            return match is null ? null : Biggest(match.CoverBig, match.CoverMedium);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a capa de {artist} - {album} no Deezer: {e.Message}");

            return null;
        }
    }

    public async Task<IReadOnlyList<int>?> AlbumDurationsAsync(string artist, string album)
    {
        var query = Uri.EscapeDataString($"artist:\"{Term(artist)}\" album:\"{Term(album)}\"");

        try
        {
            var search = await FetchAsync<AlbumSearch>($"{SearchUrl}?q={query}");
            var match = search?.Albums
                .FirstOrDefault(result =>
                    Names.Normalize(result.Artist?.Name ?? "") == Names.Normalize(artist)
                    && Names.Normalize(result.Title) == Names.Normalize(album));

            if (match is null)
            {
                return null;
            }

            var tracks = await FetchAsync<AlbumTrackList>($"{AlbumUrl}/{match.Id}/tracks?limit=100");

            return tracks is { Tracks.Count: > 0 } ? [.. tracks.Tracks.Select(track => track.Duration)] : null;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar as durações de {artist} - {album} no Deezer: {e.Message}");

            return null;
        }
    }

    private async Task<T?> FetchAsync<T>(string url) where T : DeezerResponse
    {
        for (var attempts = 1; ; attempts++)
        {
            await WaitTurnAsync();
            var payload = await http.GetFromJsonAsync<T>(url);

            if (payload?.Error is not { } error)
            {
                return payload;
            }

            if (error.Code != QuotaCode)
            {
                return null;
            }

            if (attempts == MaxAttempts)
            {
                throw new HttpRequestException("quota do Deezer estourada");
            }

            await Task.Delay(QuotaPause, time);
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

    private static string Term(string value) => value.Replace("\"", "");

    private static string? Biggest(string? big, string? medium)
    {
        var picture = string.IsNullOrEmpty(big) ? medium : big;

        return picture is null || picture.Contains(PlaceholderHash, StringComparison.Ordinal) ? null : picture;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private class DeezerResponse
    {
        [JsonPropertyName("error")]
        public DeezerError? Error { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class DeezerError
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class DeezerArtist : DeezerResponse
    {
        [JsonPropertyName("picture_big")]
        public string? PictureBig { get; init; }

        [JsonPropertyName("picture_medium")]
        public string? PictureMedium { get; init; }

        [JsonPropertyName("nb_fan")]
        public int Fans { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class UpcAlbum : DeezerResponse
    {
        [JsonPropertyName("cover_big")]
        public string? CoverBig { get; init; }

        [JsonPropertyName("cover_medium")]
        public string? CoverMedium { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumSearch : DeezerResponse
    {
        [JsonPropertyName("data")]
        public List<AlbumResult> Albums { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumResult
    {
        public long Id { get; init; }

        public required string Title { get; init; }

        [JsonPropertyName("cover_big")]
        public string? CoverBig { get; init; }

        [JsonPropertyName("cover_medium")]
        public string? CoverMedium { get; init; }

        public AlbumArtist? Artist { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumTrackList : DeezerResponse
    {
        [JsonPropertyName("data")]
        public List<AlbumTrack> Tracks { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumTrack
    {
        public int Duration { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumArtist
    {
        public required string Name { get; init; }
    }
}
