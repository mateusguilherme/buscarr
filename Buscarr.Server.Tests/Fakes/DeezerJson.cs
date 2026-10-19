using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class DeezerJson
{
    public static string ArtistProfile(string name, string? picture, string? pictureBig = null) =>
        JsonSerializer.Serialize(new
        {
            name,
            picture_medium = picture,
            picture_big = pictureBig,
        });

    public static string Album(string artist, string title, string cover, string? coverBig = null) =>
        JsonSerializer.Serialize(new
        {
            title,
            cover_medium = cover,
            cover_big = coverBig,
            artist = new { name = artist },
        });

    public static string NotFound() =>
        JsonSerializer.Serialize(new
        {
            error = new { type = "DataException", message = "no data", code = 800 },
        });

    public static string Albums(params (string Artist, string Title, string Cover)[] albums) =>
        JsonSerializer.Serialize(new
        {
            data = albums.Select((album, index) => new
            {
                id = index + 1,
                title = album.Title,
                cover_medium = album.Cover,
                artist = new { name = album.Artist },
            }),
            total = albums.Length,
        });

    public static string AlbumsWithBigCovers(params (string Artist, string Title, string Cover, string CoverBig)[] albums) =>
        JsonSerializer.Serialize(new
        {
            data = albums.Select((album, index) => new
            {
                id = index + 1,
                title = album.Title,
                cover_medium = album.Cover,
                cover_big = album.CoverBig,
                artist = new { name = album.Artist },
            }),
            total = albums.Length,
        });

    public static string AlbumTracks(params int[] durations) =>
        JsonSerializer.Serialize(new
        {
            data = durations.Select(duration => new { duration }),
            total = durations.Length,
        });
}
