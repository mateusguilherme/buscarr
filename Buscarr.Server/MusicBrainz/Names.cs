namespace Buscarr.Server.MusicBrainz;

public static class Names
{
    public static string Normalize(string name) =>
        name.Trim().ToLowerInvariant().Replace('\u2010', '-').Replace('\u2019', '\'');
}
