using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class Release
{
    public required string Title { get; init; }
    public string? Date { get; init; }

    [JsonPropertyName("release-group")]
    public ReleaseGroup? ReleaseGroup { get; init; }

    public string? Type => ReleaseGroup?.PrimaryType?.ToLowerInvariant();

    public bool IsLive => ReleaseGroup?.SecondaryTypes.Contains("Live") == true;

    public bool IsOriginalRelease =>
        Date is { Length: 10 } date
        && ReleaseGroup is { FirstReleaseDate: { Length: > 0 } firstRelease } group
        && group.SecondaryTypes.All(type => type == "Live")
        && date.StartsWith(firstRelease, StringComparison.Ordinal);
}
