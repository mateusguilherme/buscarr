using System.Text.RegularExpressions;

namespace Buscarr.Server.Slskd;

public static partial class SearchText
{
    public static string For(string artist, string album) =>
        string.Join(' ', Word().Matches($"{artist} {album}").Select(word => word.Value));

    public static string Wildcard(string text) =>
        string.Join(' ', text.Split(' ').Select(word => word.Length >= 3 ? $"*{word[1..]}" : word));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
