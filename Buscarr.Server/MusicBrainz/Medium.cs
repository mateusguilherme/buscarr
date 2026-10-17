using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class Medium
{
    public string? Format { get; init; }

    [JsonPropertyName("track-count")]
    public int TrackCount { get; init; }
}
