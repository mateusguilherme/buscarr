using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class ReleaseGroup
{
    [JsonPropertyName("first-release-date")]
    public string? FirstReleaseDate { get; init; }

    [JsonPropertyName("secondary-types")]
    public List<string> SecondaryTypes { get; init; } = [];
}
