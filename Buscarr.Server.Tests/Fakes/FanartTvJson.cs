using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class FanartTvJson
{
    public static string Artist(params (string Type, string Url, int Likes)[] images) =>
        JsonSerializer.Serialize(images
            .GroupBy(image => image.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Select((image, index) => new
                {
                    id = index.ToString(),
                    url = image.Url,
                    likes = image.Likes.ToString(),
                })));

    public static string NotFound() =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["status"] = "error",
            ["error message"] = "Not found",
        });
}
