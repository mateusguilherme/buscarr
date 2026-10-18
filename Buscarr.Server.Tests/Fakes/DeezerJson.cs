using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class DeezerJson
{
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
