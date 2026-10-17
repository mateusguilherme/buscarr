using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Buscarr.Server.Settings;

namespace Buscarr.Server.FanartTv;

public sealed partial class FanartTvClient(HttpClient http, Func<ServerSettings> settings)
{
    private const string UserAgent = "buscarr/0.1.0";
    private const string ProbeArtistId = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";

    public FanartTvClient(HttpClient http, string? apiKey)
        : this(http, () => new ServerSettings { FanartTvApiKey = apiKey })
    {
    }

    public bool IsConfigured => !string.IsNullOrEmpty(settings().FanartTvApiKey);

    public async Task PingAsync()
    {
        var apiKey = settings().FanartTvApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("fanart.tv sem chave de API");
        }

        using var response = await GetArtistAsync(ProbeArtistId, apiKey);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException("chave inválida");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> ArtistImageAsync(string musicBrainzId)
    {
        var apiKey = settings().FanartTvApiKey;

        if (string.IsNullOrEmpty(apiKey) || !Guid.TryParseExact(musicBrainzId, "D", out _))
        {
            return null;
        }

        using var response = await GetArtistAsync(musicBrainzId, apiKey);

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

    private async Task<HttpResponseMessage> GetArtistAsync(string musicBrainzId, string apiKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://webservice.fanart.tv/v3/music/{musicBrainzId}?api_key={Uri.EscapeDataString(apiKey)}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        return await http.SendAsync(request);
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
