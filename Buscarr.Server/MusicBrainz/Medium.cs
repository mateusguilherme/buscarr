using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class Medium
{
    public string? Format { get; init; }

    public int Position { get; init; }

    [JsonPropertyName("track-count")]
    public int TrackCount { get; init; }

    public List<MediumTrack> Tracks { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class MediumTrack
{
    public int Position { get; init; }

    public required string Title { get; init; }

    public int? Length { get; init; }

    public Recording? Recording { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class Recording
{
    public string? Id { get; init; }

    public int? Length { get; init; }
}
