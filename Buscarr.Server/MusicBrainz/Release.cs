using System.Text.Json.Serialization;
using JetBrains.Annotations;

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

    [JsonPropertyName("artist-credit")]
    [UsedImplicitly]
    public List<ArtistCredit> ArtistCredit { get; init; } = [];

    [JsonPropertyName("cover-art-archive")]
    public CoverArtArchive? CoverArtArchive { get; init; }

    public bool HasFrontCover => CoverArtArchive is { Front: true } && Id is not null;

    public List<string> CreditedArtists =>
        [.. ArtistCredit.Select(credit => credit.Name ?? credit.Artist?.Name).OfType<string>()];

    public string? CreditedAs =>
        ArtistCredit.Count == 0
            ? null
            : string.Concat(ArtistCredit.Select(credit => $"{credit.Name ?? credit.Artist?.Name}{credit.JoinPhrase}"));

    public string? CoverUrl =>
        HasFrontCover
            ? $"{CoverArtArchiveUrl}/release/{Id}/front-250"
            : ReleaseGroup?.Id is { } releaseGroupId
                ? $"{CoverArtArchiveUrl}/release-group/{releaseGroupId}/front-250"
                : null;

    private static readonly string[] SecondaryCategories = ["compilation", "soundtrack", "remix", "demo", "live"];

    private static readonly string[] PrimaryCategories = ["album", "ep", "single"];

    public string? Type => ReleaseGroup?.PrimaryType?.ToLowerInvariant();

    public string? Category
    {
        get
        {
            var secondary = SecondaryCategories.FirstOrDefault(category =>
                ReleaseGroup?.SecondaryTypes.Contains(category, StringComparer.OrdinalIgnoreCase) == true);

            return secondary ?? (PrimaryCategories.Contains(Type) ? Type : null);
        }
    }

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
        && ReleaseGroup is { FirstReleaseDate: { Length: > 0 } firstRelease }
        && date.StartsWith(firstRelease, StringComparison.Ordinal);
}
