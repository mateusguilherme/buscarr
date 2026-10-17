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
    SubsonicClient subsonic,
    TimeProvider time)
{
    public async Task GenerateAsync()
    {
        var now = time.GetUtcNow();
        var existing = await db.DownloadRequests.ToListAsync();
        var keys = existing.Select(Key).ToHashSet();
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
                Console.Error.WriteLine($"erro ao listar a biblioteca: {e.Message}");
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
                    Console.Error.WriteLine($"erro ao buscar os lançamentos de {artist.Name}: {e.Message}");
                    complete = false;

                    continue;
                }

                foreach (var album in albums)
                {
                    Add(artist, album, edition, quality, library, wanted, keys, now);
                }
            }
        }

        if (complete)
        {
            db.DownloadRequests.RemoveRange(existing.Where(request =>
                !wanted.Contains(Key(request))
                && request.State != DownloadState.Searching
                && request.State != DownloadState.Downloading));
        }

        await db.SaveChangesAsync();
    }

    private void Add(
        ListedArtist artist,
        Album album,
        string edition,
        string quality,
        LibraryIndex? library,
        HashSet<(string, int, string)> wanted,
        HashSet<(string, int, string)> keys,
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

        if (!wanted.Add(key) || keys.Contains(key))
        {
            return;
        }

        db.DownloadRequests.Add(new DownloadRequest
        {
            ArtistId = artist.ArtistId,
            ArtistName = artist.Name,
            ReleaseGroupId = album.First.ReleaseGroup!.Id!,
            Title = album.First.Title,
            Type = category,
            FirstReleaseDate = firstReleaseDate,
            EditionReleaseId = chosen.Id,
            TrackCount = chosen.TrackCount,
            Quality = quality,
            CoverUrl = chosen.CoverUrl ?? album.First.CoverUrl,
            State = DownloadState.Waiting,
            NextAttemptAt = RetrySchedule.FirstAttempt(now, firstReleaseDate),
        });
    }

    private static (string, int, string) Key(DownloadRequest request) =>
        (request.ReleaseGroupId, request.TrackCount, request.Quality);
}
