using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record Artist(string Id, string Name, Area? Area = null, string? Disambiguation = null)
{
    [JsonPropertyName("relations")]
    public List<UrlRelation> Relations { get; init; } = [];

    public IEnumerable<string> DeezerIds => StreamingIds("deezer.com/artist/");

    public IEnumerable<string> SpotifyIds => StreamingIds("open.spotify.com/artist/");

    private IEnumerable<string> StreamingIds(string marker)
    {
        foreach (var relation in Relations)
        {
            var url = relation.Url?.Resource ?? "";
            var start = url.IndexOf(marker, StringComparison.Ordinal);

            if (start < 0)
            {
                continue;
            }

            var id = url[(start + marker.Length)..];
            var end = id.IndexOfAny(['/', '?']);

            yield return end < 0 ? id : id[..end];
        }
    }
}
