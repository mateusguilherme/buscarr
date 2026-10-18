using System.Text.Json.Serialization;
using Buscarr.Server.MusicBrainz;
using JetBrains.Annotations;

namespace Buscarr.Server.Deezer;

public sealed class DeezerClient(HttpClient http)
{
    private const string SearchUrl = "https://api.deezer.com/search/album";

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

    private static string Term(string value) => value.Replace("\"", "");

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumSearch
    {
        [JsonPropertyName("data")]
        public List<AlbumResult> Albums { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumResult
    {
        public required string Title { get; init; }

        [JsonPropertyName("cover_medium")]
        public string? CoverMedium { get; init; }

        public AlbumArtist? Artist { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class AlbumArtist
    {
        public required string Name { get; init; }
    }
}
