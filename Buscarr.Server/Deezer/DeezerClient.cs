using System.Text.Json.Serialization;
using Buscarr.Server.MusicBrainz;
using JetBrains.Annotations;

namespace Buscarr.Server.Deezer;

public sealed class DeezerClient(HttpClient http)
{
    private const string SearchUrl = "https://api.deezer.com/search/album";
    private const string AlbumUrl = "https://api.deezer.com/album";
    private const string ArtistUrl = "https://api.deezer.com/artist";

    public async Task<string?> ArtistImageAsync(string id)
    {
        try
        {
            var artist = await http.GetFromJsonAsync<DeezerArtist>($"{ArtistUrl}/{Uri.EscapeDataString(id)}");

            return string.IsNullOrEmpty(artist?.PictureMedium) ? null : artist.PictureMedium;
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
            var album = await http.GetFromJsonAsync<UpcAlbum>($"{AlbumUrl}/upc:{Uri.EscapeDataString(barcode)}");

            return album?.CoverMedium;
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
            var search = await http.GetFromJsonAsync<AlbumSearch>($"{SearchUrl}?q={query}");

            return search?.Albums
                .FirstOrDefault(result =>
                    Names.Normalize(result.Artist?.Name ?? "") == Names.Normalize(artist)
                    && Names.Normalize(result.Title) == Names.Normalize(album))
                ?.CoverMedium;
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
            var search = await http.GetFromJsonAsync<AlbumSearch>($"{SearchUrl}?q={query}");
            var match = search?.Albums
                .FirstOrDefault(result =>
                    Names.Normalize(result.Artist?.Name ?? "") == Names.Normalize(artist)
                    && Names.Normalize(result.Title) == Names.Normalize(album));

            if (match is null)
            {
                return null;
            }

            var tracks = await http.GetFromJsonAsync<AlbumTrackList>($"{AlbumUrl}/{match.Id}/tracks?limit=100");

            return tracks is { Tracks.Count: > 0 } ? [.. tracks.Tracks.Select(track => track.Duration)] : null;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar as durações de {artist} - {album} no Deezer: {e.Message}");

            return null;
        }
    }

    private static string Term(string value) => value.Replace("\"", "");

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class DeezerArtist
    {
        [JsonPropertyName("picture_medium")]
        public string? PictureMedium { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class UpcAlbum
    {
        [JsonPropertyName("cover_medium")]
        public string? CoverMedium { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumSearch
    {
        [JsonPropertyName("data")]
        public List<AlbumResult> Albums { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumResult
    {
        public long Id { get; init; }

        public required string Title { get; init; }

        [JsonPropertyName("cover_medium")]
        public string? CoverMedium { get; init; }

        public AlbumArtist? Artist { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumTrackList
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
