using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Buscarr.Server.FanartTv;

public sealed partial class FanartTvClient(HttpClient http, string? apiKey)
{
    private const string UserAgent = "buscarr/0.1.0";

    public async Task<string?> ArtistImageAsync(string musicBrainzId)
    {
        if (string.IsNullOrEmpty(apiKey) || !Guid.TryParseExact(musicBrainzId, "D", out _))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://webservice.fanart.tv/v3/music/{musicBrainzId}?api_key={Uri.EscapeDataString(apiKey)}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var artist = await response.Content.ReadFromJsonAsync<FanartTvArtist>()
            ?? throw new JsonException("resposta vazia do fanart.tv");
        var image = MostLiked(artist.ArtistThumb) ?? MostLiked(artist.ArtistBackground);

        return image is null ? null : FullSizeAsset().Replace(image.Url, "https://assets.fanart.tv/preview/");
    }

    private static FanartTvImage? MostLiked(List<FanartTvImage> images) =>
        images.MaxBy(image => image.Likes);

    [GeneratedRegex("^https?://assets\\.fanart\\.tv/fanart/")]
    private static partial Regex FullSizeAsset();

    private sealed class FanartTvArtist
    {
        public List<FanartTvImage> ArtistThumb { get; init; } = [];

        public List<FanartTvImage> ArtistBackground { get; init; } = [];
    }

    private sealed class FanartTvImage
    {
        public required string Url { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Likes { get; init; }
    }
}
