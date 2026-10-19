using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class SpotifyJson
{
    public static string Token(string accessToken = "token-1", int expiresIn = 3600) =>
        JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = expiresIn,
        });

    public static string Artist(string name, string image, int followers = 0) =>
        JsonSerializer.Serialize(new
        {
            name,
            followers = new { total = followers },
            images = new object[]
            {
                new { url = $"{image}?size=640", width = 640, height = 640 },
                new { url = image, width = 300, height = 300 },
                new { url = $"{image}?size=64", width = 64, height = 64 },
            },
        });

    public static string Artist(string name, params (string Url, int Width)[] images) =>
        JsonSerializer.Serialize(new
        {
            name,
            images = images.Select(image => new { url = image.Url, width = image.Width, height = image.Width }),
        });

    public static string Albums(params (string Artist, string Title, string Cover)[] albums) =>
        JsonSerializer.Serialize(new
        {
            albums = new
            {
                items = albums.Select(album => new
                {
                    name = album.Title,
                    artists = new[] { new { name = album.Artist } },
                    images = new object[]
                    {
                        new { url = $"{album.Cover}?size=640", width = 640, height = 640 },
                        new { url = album.Cover, width = 300, height = 300 },
                        new { url = $"{album.Cover}?size=64", width = 64, height = 64 },
                    },
                }),
            },
        });
}
