using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class ReleaseGroup
{
    public string? Id { get; init; }

    [JsonPropertyName("primary-type")]
    public string? PrimaryType { get; init; }

    [JsonPropertyName("first-release-date")]
    public string? FirstReleaseDate { get; init; }

    [JsonPropertyName("secondary-types")]
    public List<string> SecondaryTypes { get; init; } = [];
}
