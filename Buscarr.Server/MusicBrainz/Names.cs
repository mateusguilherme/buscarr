using System.Globalization;
using System.Text;

namespace Buscarr.Server.MusicBrainz;

public static class Names
{
    public static string Normalize(string name)
    {
        var folded = name.Trim().ToLowerInvariant().Replace('‐', '-').Replace('’', '\'');
        var letters = folded
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

        return string.Concat(letters).Normalize(NormalizationForm.FormC);
    }
}
