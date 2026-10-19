using System.Collections.Concurrent;
using Buscarr.Server.Deezer;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Spotify;

namespace Buscarr.Server.Downloads;

public sealed class AlbumCovers(DeezerClient deezer, SpotifyClient spotify, HttpClient http)
{
    private readonly ConcurrentDictionary<string, bool> _verified = new();

    public async Task<string?> FindAsync(Album album, Release chosen, IEnumerable<string> artistNames)
    {
        if (album.Editions.Any(release => release.HasFrontCover)
            && (chosen.CoverUrl ?? album.First.CoverUrl) is { } archive
            && await ServesAsync(archive))
        {
            return archive;
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

    private async Task<bool> ServesAsync(string url)
    {
        if (_verified.TryGetValue(url, out var known))
        {
            return known;
        }

        bool serves;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await http.SendAsync(request);
            serves = response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao verificar a capa {url}: {e.Message}");
            serves = false;
        }

        _verified[url] = serves;

        return serves;
    }

    private static List<string> Barcodes(Release chosen, Album album) =>
        [.. album.Editions
            .Prepend(chosen)
            .Select(release => release.Barcode)
            .OfType<string>()
            .Where(barcode => barcode.Length > 0)
            .Distinct()];
}
