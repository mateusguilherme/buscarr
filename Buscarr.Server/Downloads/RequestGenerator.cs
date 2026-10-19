using Buscarr.Server.Data;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Subsonic;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Downloads;

public sealed class RequestGenerator(
    BuscarrDbContext db,
    MusicBrainzClient musicBrainz,
    ArtistImages images,
    AlbumCovers covers,
    SubsonicClient subsonic,
    TimeProvider time)
{
    public async Task GenerateAsync()
    {
        var now = time.GetUtcNow();
        var existing = await db.DownloadRequests.ToListAsync();
        var existingByKey = existing.ToLookup(Key);
        var wanted = new HashSet<(string, int, string)>();
        var complete = true;
        LibraryIndex? library = null;

        if (subsonic.IsConfigured)
        {
            try
            {
                library = new LibraryIndex(await subsonic.AlbumsAsync());
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"erro ao listar a biblioteca: {e.Message}");
                complete = false;
            }
        }

        foreach (var user in await db.Users.ToListAsync())
        {
            var quality = user.QualityPreference ?? Qualities.Mp3320;
            var edition = user.EditionPreference ?? Editions.Original;

            foreach (var artist in await db.ListedArtists.Where(a => a.UserId == user.Id).ToListAsync())
            {
                List<Album> albums;

                try
                {
                    albums = await musicBrainz.ReleasesAsync(user.Email, artist.ArtistId);
                }
                catch (Exception e)
                {
                    await Console.Error.WriteLineAsync($"erro ao buscar os lançamentos de {artist.Name}: {e.Message}");
                    complete = false;

                    continue;
                }

                if (string.IsNullOrEmpty(artist.Image))
                {
                    artist.Image = await ImageAsync(user.Email, artist) ?? artist.Image;
                }

                foreach (var album in albums)
                {
                    await AddAsync(artist, album, edition, quality, library, wanted, existingByKey, now);
                }
            }
        }

        if (complete)
        {
            db.DownloadRequests.RemoveRange(existing.Where(request =>
                !wanted.Contains(Key(request))
                && !request.Requested
                && request.AcceptedQuality == null
                && request.State != DownloadState.Searching
                && request.State != DownloadState.Downloading));
        }

        await db.SaveChangesAsync();
    }

    private async Task AddAsync(
        ListedArtist artist,
        Album album,
        string edition,
        string quality,
        LibraryIndex? library,
        HashSet<(string, int, string)> wanted,
        ILookup<(string, int, string), DownloadRequest> existing,
        DateTimeOffset now)
    {
        if (album.First.Category is not { } category || !artist.ReleaseTypes.Contains(category))
        {
            return;
        }

        var chosen = Editions.Choose(album.Editions, edition);

        if (chosen.TrackCount == 0)
        {
            return;
        }

        var ids = album.Editions
            .Select(release => release.Id)
            .OfType<string>()
            .Append(album.First.ReleaseGroup!.Id!);

        if (library?.TrackCount(ids, album.First.Title, artist.Name) >= chosen.TrackCount)
        {
            return;
        }

        var firstReleaseDate = album.First.ReleaseGroup!.FirstReleaseDate ?? album.First.Date;
        var key = (album.First.ReleaseGroup!.Id!, chosen.TrackCount, quality);

        if (!wanted.Add(key))
        {
            return;
        }

        var coverUrl = await covers.FindAsync(album, chosen, album.First.CreditedArtists.Append(artist.Name));

        if (existing.Contains(key))
        {
            foreach (var request in existing[key])
            {
                request.CoverUrl = coverUrl ?? request.CoverUrl;
                request.Credit = album.First.CreditedAs ?? artist.Name;
            }

            return;
        }

        db.DownloadRequests.Add(new DownloadRequest
        {
            ArtistId = artist.ArtistId,
            ArtistName = artist.Name,
            Credit = album.First.CreditedAs ?? artist.Name,
            ReleaseGroupId = album.First.ReleaseGroup!.Id!,
            Title = album.First.Title,
            Type = category,
            FirstReleaseDate = firstReleaseDate,
            EditionReleaseId = chosen.Id,
            TrackCount = chosen.TrackCount,
            Quality = quality,
            CoverUrl = coverUrl ?? chosen.CoverUrl ?? album.First.CoverUrl,
            State = DownloadState.Waiting,
            NextAttemptAt = RetrySchedule.FirstAttempt(now, firstReleaseDate),
        });
    }

    private async Task<string?> ImageAsync(string contact, ListedArtist artist)
    {
        try
        {
            return await images.FindAsync(await musicBrainz.ArtistAsync(contact, artist.ArtistId));
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao buscar a foto de {artist.Name}: {e.Message}");

            return null;
        }
    }

    private static (string, int, string) Key(DownloadRequest request) =>
        (request.ReleaseGroupId, request.TrackCount, request.Quality);
}
