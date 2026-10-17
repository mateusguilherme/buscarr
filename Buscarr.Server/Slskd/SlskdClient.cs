using System.Text.Json;

namespace Buscarr.Server.Slskd;

public record SlskdServerState(bool IsConnected, bool IsLoggedIn);

public sealed class SlskdClient(HttpClient http, string? url, string? apiKey)
{
    private const string UserAgent = "buscarr/0.1.0";

    public async Task<SlskdServerState?> ServerStateAsync()
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var application = await GetAsync<ApplicationState>(url, apiKey, "application");

        return application.Server;
    }

    private async Task<T> GetAsync<T>(string baseUrl, string key, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/v0/{path}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-API-Key", key);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new JsonException($"resposta vazia do slskd em {path}");
    }

    private sealed class ApplicationState
    {
        public required SlskdServerState Server { get; init; }
    }
}
