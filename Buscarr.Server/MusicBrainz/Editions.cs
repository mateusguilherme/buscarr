namespace Buscarr.Server.MusicBrainz;

public static class Editions
{
    public const string Original = "original";
    public const string Remastered = "remastered";
    public const string Deluxe = "deluxe";

    public static readonly string[] Preferences = [Original, Remastered, Deluxe];

    public static Release Choose(IReadOnlyCollection<Release> editions, string preference)
    {
        var eligible = editions.Where(edition => edition.IsCdOrDigital).ToList();

        if (eligible.Count == 0)
        {
            eligible = [.. editions];
        }

        var preferred = preference switch
        {
            Remastered => eligible
                .Where(edition => edition.Kind == EditionKind.Remastered)
                .OrderByDescending(edition => edition.Date, StringComparer.Ordinal)
                .FirstOrDefault(),
            Deluxe => MostCommon(eligible.Where(edition => edition.Kind == EditionKind.Deluxe)),
            _ => null,
        };

        return preferred
            ?? MostCommon(eligible.Where(edition => edition.Kind == EditionKind.Original))
            ?? MostCommon(eligible)!;
    }

    private static Release? MostCommon(IEnumerable<Release> editions) =>
        editions
            .OrderBy(edition => edition.Date ?? "9999", StringComparer.Ordinal)
            .GroupBy(edition => edition.TrackCount)
            .OrderByDescending(group => group.Count())
            .Select(group => group.First())
            .FirstOrDefault();
}
