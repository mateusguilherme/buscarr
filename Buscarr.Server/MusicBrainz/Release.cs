using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class Release
{
    private const string CoverArtArchiveUrl = "https://coverartarchive.org";

    public string? Id { get; init; }

    public required string Title { get; init; }
    public string? Date { get; init; }

    [JsonPropertyName("release-group")]
    public ReleaseGroup? ReleaseGroup { get; init; }

    [JsonPropertyName("cover-art-archive")]
    public CoverArtArchive? CoverArtArchive { get; init; }

    public string? CoverUrl =>
        CoverArtArchive is { Front: true } && Id is not null
            ? $"{CoverArtArchiveUrl}/release/{Id}/front-250"
            : ReleaseGroup?.Id is { } releaseGroupId
                ? $"{CoverArtArchiveUrl}/release-group/{releaseGroupId}/front-250"
                : null;

    public string? Type => ReleaseGroup?.PrimaryType?.ToLowerInvariant();

    public bool IsLive => ReleaseGroup?.SecondaryTypes.Contains("Live") == true;

    public bool IsOriginalRelease =>
        Date is { Length: 10 } date
        && ReleaseGroup is { FirstReleaseDate: { Length: > 0 } firstRelease } group
        && group.SecondaryTypes.All(type => type == "Live")
        && date.StartsWith(firstRelease, StringComparison.Ordinal);
}
