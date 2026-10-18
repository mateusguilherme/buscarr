using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class DeezerJson
{
    public static string Album(string artist, string title, string cover) =>
        JsonSerializer.Serialize(new
        {
            title,
            cover_medium = cover,
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
            data = albums.Select(album => new
            {
                title = album.Title,
                cover_medium = album.Cover,
                artist = new { name = album.Artist },
            }),
            total = albums.Length,
        });
}
