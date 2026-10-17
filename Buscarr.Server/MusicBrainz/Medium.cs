using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class Medium
{
    public string? Format { get; init; }

    public int Position { get; init; }

    [JsonPropertyName("track-count")]
    public int TrackCount { get; init; }

    public List<MediumTrack> Tracks { get; init; } = [];
}

public sealed class MediumTrack
{
    public int Position { get; init; }

    public required string Title { get; init; }

    public int? Length { get; init; }

    public Recording? Recording { get; init; }
}

public sealed class Recording
{
    public string? Id { get; init; }

    public int? Length { get; init; }
}
