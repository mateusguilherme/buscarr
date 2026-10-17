using Buscarr.Server.Data;
using Buscarr.Server.MusicBrainz;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Lists;

public class ArtistListStore(BuscarrDbContext db)
{
    public async Task<List<ListedArtist>> ArtistsAsync(int userId)
    {
        var artists = await db.ListedArtists.Where(a => a.UserId == userId).ToListAsync();

        return [.. artists.OrderBy(a => Names.Normalize(a.Name), StringComparer.Ordinal)];
    }

    public Task<ListedArtist?> EntryAsync(int userId, string artistId) =>
        db.ListedArtists.SingleOrDefaultAsync(a => a.UserId == userId && a.ArtistId == artistId);

    public async Task<HashSet<string>> ListedAsync(int userId, IEnumerable<string> artistIds)
    {
        var ids = artistIds.ToList();

        return [.. await db.ListedArtists
            .Where(a => a.UserId == userId && ids.Contains(a.ArtistId))
            .Select(a => a.ArtistId)
            .ToListAsync()];
    }

    public async Task AddAsync(ListedArtist artist)
    {
        var existing = await db.ListedArtists.FindAsync(artist.UserId, artist.ArtistId);

        if (existing is null)
        {
            db.ListedArtists.Add(artist);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(artist);
        }

        await db.SaveChangesAsync();
    }

    public Task RemoveAsync(int userId, string artistId) =>
        db.ListedArtists.Where(a => a.UserId == userId && a.ArtistId == artistId).ExecuteDeleteAsync();
}
