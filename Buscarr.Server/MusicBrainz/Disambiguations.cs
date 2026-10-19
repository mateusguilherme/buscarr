namespace Buscarr.Server.MusicBrainz;

public static class Disambiguations
{
    private static readonly Dictionary<string, string> IrregularDemonyms = new()
    {
        ["american"] = "united states",
        ["us"] = "united states",
        ["usa"] = "united states",
        ["british"] = "united kingdom",
        ["uk"] = "united kingdom",
        ["english"] = "england",
        ["scottish"] = "scotland",
        ["welsh"] = "wales",
        ["irish"] = "ireland",
        ["french"] = "france",
        ["spanish"] = "spain",
        ["dutch"] = "netherlands",
        ["danish"] = "denmark",
        ["polish"] = "poland",
        ["finnish"] = "finland",
        ["swiss"] = "switzerland",
        ["greek"] = "greece",
    };

    public static string? Clean(string? disambiguation, string? area)
    {
        if (string.IsNullOrWhiteSpace(disambiguation) || string.IsNullOrWhiteSpace(area))
        {
            return disambiguation;
        }

        var normalizedArea = Names.Normalize(area);
        var words = disambiguation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();

        foreach (var word in words)
        {
            if (Matches(Names.Normalize(word.Trim(',', '.', ';', '(', ')')), normalizedArea))
            {
                if (kept.Count > 0 && kept[^1].Equals("from", StringComparison.OrdinalIgnoreCase))
                {
                    kept.RemoveAt(kept.Count - 1);
                }

                continue;
            }

            kept.Add(word);
        }

        if (kept.Count == words.Length)
        {
            return disambiguation;
        }

        if (kept.Count == 0)
        {
            return null;
        }

        var cleaned = string.Join(' ', kept);

        return kept[0] == words[0] ? cleaned : char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }

    private static bool Matches(string word, string area)
    {
        if (word.Length == 0)
        {
            return false;
        }

        if (IrregularDemonyms.TryGetValue(word, out var country))
        {
            return country == area;
        }

        var threshold = Math.Min(area.Length, Math.Max(4, area.Length - 2));

        return CommonPrefix(word, area) >= threshold
            && (word.Length > area.Length || area.StartsWith(word, StringComparison.Ordinal));
    }

    private static int CommonPrefix(string a, string b)
    {
        var length = 0;

        while (length < a.Length && length < b.Length && a[length] == b[length])
        {
            length++;
        }

        return length;
    }
}
