using Buscarr.Server.Deezer;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Spotify;

namespace Buscarr.Server.Downloads;

public sealed class AlbumCovers(DeezerClient deezer, SpotifyClient spotify)
{
    public async Task<string?> FindAsync(Album album, Release chosen, IEnumerable<string> artistNames)
    {
        if (album.Editions.Any(release => release.HasFrontCover))
        {
            return chosen.CoverUrl ?? album.First.CoverUrl;
        }

        var credited = artistNames.Distinct().ToList();
        var barcodes = Barcodes(chosen, album);

        foreach (var barcode in barcodes)
        {
            if (await deezer.AlbumCoverByBarcodeAsync(barcode) is { } cover)
            {
                return cover;
            }
        }

        foreach (var name in credited)
        {
            if (await deezer.AlbumCoverAsync(name, album.First.Title) is { } cover)
            {
                return cover;
            }
        }

        foreach (var barcode in barcodes)
        {
            if (await spotify.AlbumCoverByBarcodeAsync(barcode) is { } cover)
            {
                return cover;
            }
        }

        foreach (var name in credited)
        {
            if (await spotify.AlbumCoverAsync(name, album.First.Title) is { } cover)
            {
                return cover;
            }
        }

        return null;
    }

    private static List<string> Barcodes(Release chosen, Album album) =>
        [.. album.Editions
            .Prepend(chosen)
            .Select(release => release.Barcode)
            .OfType<string>()
            .Where(barcode => barcode.Length > 0)
            .Distinct()];
}
