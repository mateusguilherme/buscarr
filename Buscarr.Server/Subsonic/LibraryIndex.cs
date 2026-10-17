using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Subsonic;

public sealed class LibraryIndex
{
    private readonly Dictionary<string, int> _byId = [];
    private readonly Dictionary<(string Name, string Artist), int> _byName = [];

    public LibraryIndex(IEnumerable<SubsonicAlbum> albums)
    {
        foreach (var album in albums)
        {
            if (album.MusicBrainzId is { Length: > 0 } id)
            {
                Keep(_byId, id.ToLowerInvariant(), album.SongCount);
            }
            else
            {
                Keep(_byName, (Names.Normalize(album.Name), Names.Normalize(album.Artist)), album.SongCount);
            }
        }
    }

    public int? TrackCount(IEnumerable<string> musicBrainzIds, string name, string artist)
    {
        var counts = musicBrainzIds
            .Select(id => id.ToLowerInvariant())
            .Where(_byId.ContainsKey)
            .Select(id => _byId[id])
            .ToList();

        if (counts.Count > 0)
        {
            return counts.Max();
        }

        return _byName.TryGetValue((Names.Normalize(name), Names.Normalize(artist)), out var count)
            ? count
            : null;
    }

    private static void Keep<TKey>(Dictionary<TKey, int> index, TKey key, int songCount)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var existing) || songCount > existing)
        {
            index[key] = songCount;
        }
    }
}
