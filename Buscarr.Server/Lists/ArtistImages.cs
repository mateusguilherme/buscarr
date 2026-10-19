using Buscarr.Server.Deezer;
using Buscarr.Server.FanartTv;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Spotify;

namespace Buscarr.Server.Lists;

public sealed class ArtistImages(FanartTvClient fanartTv, DeezerClient deezer, SpotifyClient spotify)
{
    public async Task<string?> FindAsync(Artist artist)
    {
        var pictures = new List<(string Url, int Popularity)>();

        foreach (var deezerId in artist.DeezerIds)
        {
            if (await deezer.ArtistImageAsync(deezerId) is { } picture)
            {
                pictures.Add(picture);
            }
        }

        if (pictures.Count == 0)
        {
            foreach (var spotifyId in artist.SpotifyIds)
            {
                if (await spotify.ArtistImageAsync(spotifyId) is { } photo)
                {
                    pictures.Add(photo);
                }
            }
        }

        if (pictures.Count > 0)
        {
            return pictures.MaxBy(picture => picture.Popularity).Url;
        }

        if (await FanartTvImageAsync(artist.Id) is { } image)
        {
            return image;
        }

        return null;
    }

    private async Task<string?> FanartTvImageAsync(string id)
    {
        try
        {
            return await fanartTv.ArtistImageAsync(id);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro no fanart.tv: {e.Message}");

            return null;
        }
    }
}
