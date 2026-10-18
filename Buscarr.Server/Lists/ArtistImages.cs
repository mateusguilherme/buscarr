using Buscarr.Server.Deezer;
using Buscarr.Server.FanartTv;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Spotify;

namespace Buscarr.Server.Lists;

public sealed class ArtistImages(FanartTvClient fanartTv, DeezerClient deezer, SpotifyClient spotify)
{
    public async Task<string?> FindAsync(Artist artist)
    {
        if (await FanartTvImageAsync(artist.Id) is { } image)
        {
            return image;
        }

        if (artist.DeezerId is { } deezerId && await deezer.ArtistImageAsync(deezerId) is { } picture)
        {
            return picture;
        }

        if (artist.SpotifyId is { } spotifyId && await spotify.ArtistImageAsync(spotifyId) is { } photo)
        {
            return photo;
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
