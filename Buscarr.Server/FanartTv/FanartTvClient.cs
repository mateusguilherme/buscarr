using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Buscarr.Server.Settings;
using JetBrains.Annotations;

namespace Buscarr.Server.FanartTv;

public sealed partial class FanartTvClient(HttpClient http, Func<ServerSettings> settings, string projectKey = FanartTvClient.ProjectKey)
{
    public const string ProjectKey = "32914d5190f57cf5626d009a7e46998e";

    private const string UserAgent = "buscarr/0.1.0";
    private const string ProbeArtistId = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";

    public FanartTvClient(HttpClient http, string? apiKey)
        : this(http, () => new ServerSettings { FanartTvApiKey = apiKey }, "")
    {
    }

    public bool IsConfigured => Keys().ApiKey is not null;

    public async Task PingAsync()
    {
        var (apiKey, clientKey) = Keys();

        if (apiKey is null)
        {
            throw new InvalidOperationException("fanart.tv sem chave de API");
        }

        using var response = await GetArtistAsync(ProbeArtistId, apiKey, clientKey);

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
        var (apiKey, clientKey) = Keys();

        if (apiKey is null || !Guid.TryParseExact(musicBrainzId, "D", out _))
        {
            return null;
        }

        using var response = await GetArtistAsync(musicBrainzId, apiKey, clientKey);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var artist = await response.Content.ReadFromJsonAsync<FanartTvArtist>()
            ?? throw new JsonException("resposta vazia do fanart.tv");
        var image = MostLiked(artist.ArtistThumb) ?? MostLiked(artist.ArtistBackground);

        return image is null ? null : Insecure().Replace(image.Url, "https://");
    }

    private async Task<HttpResponseMessage> GetArtistAsync(string musicBrainzId, string apiKey, string? clientKey)
    {
        var url = $"https://webservice.fanart.tv/v3/music/{musicBrainzId}?api_key={Uri.EscapeDataString(apiKey)}";

        if (clientKey is not null)
        {
            url += $"&client_key={Uri.EscapeDataString(clientKey)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        return await http.SendAsync(request);
    }

    private (string? ApiKey, string? ClientKey) Keys()
    {
        var personal = settings().FanartTvApiKey;

        if (projectKey.Length > 0)
        {
            return (projectKey, string.IsNullOrEmpty(personal) ? null : personal);
        }

        return (string.IsNullOrEmpty(personal) ? null : personal, null);
    }

    private static FanartTvImage? MostLiked(List<FanartTvImage> images) =>
        images.MaxBy(image => image.Likes);

    [GeneratedRegex("^http://")]
    private static partial Regex Insecure();

    private sealed class FanartTvArtist
    {
        public List<FanartTvImage> ArtistThumb { get; init; } = [];

        public List<FanartTvImage> ArtistBackground { get; init; } = [];
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class FanartTvImage
    {
        public required string Url { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Likes { get; init; }
    }
}
