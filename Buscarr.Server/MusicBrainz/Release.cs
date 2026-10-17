using System.Text.Json.Serialization;

namespace Buscarr.Server.MusicBrainz;

public sealed class Release
{
    private const string CoverArtArchiveUrl = "https://coverartarchive.org";

    public string? Id { get; init; }

    public required string Title { get; init; }
    public string? Date { get; init; }

    public string? Disambiguation { get; init; }

    public List<Medium> Media { get; init; } = [];

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

    public int TrackCount => Media.Sum(medium => medium.TrackCount);

    public bool IsCdOrDigital =>
        Media.Count > 0
        && Media.All(medium =>
            medium.Format == "Digital Media" || medium.Format?.Contains("CD", StringComparison.Ordinal) == true);

    public EditionKind Kind
    {
        get
        {
            var text = $"{Title} {Disambiguation}";

            if (text.Contains("deluxe", StringComparison.OrdinalIgnoreCase))
            {
                return EditionKind.Deluxe;
            }

            return text.Contains("remaster", StringComparison.OrdinalIgnoreCase)
                ? EditionKind.Remastered
                : EditionKind.Original;
        }
    }

    public bool IsLive => ReleaseGroup?.SecondaryTypes.Contains("Live") == true;

    public bool IsOriginalRelease =>
        Date is { Length: 10 } date
        && ReleaseGroup is { FirstReleaseDate: { Length: > 0 } firstRelease } group
        && group.SecondaryTypes.All(type => type == "Live")
        && date.StartsWith(firstRelease, StringComparison.Ordinal);
}
