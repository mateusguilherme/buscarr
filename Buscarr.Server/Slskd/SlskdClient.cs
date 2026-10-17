using System.Net;
using System.Text.Json;

namespace Buscarr.Server.Slskd;

public record SlskdServerState(bool IsConnected, bool IsLoggedIn);

public record SlskdSearch(Guid Id, string State, bool IsComplete, int ResponseCount);

public record SlskdResponse(
    string Username,
    bool HasFreeUploadSlot,
    int QueueLength,
    int UploadSpeed,
    List<SlskdFile> Files);

public record SlskdFile(string Filename, long Size, int? BitRate, int? BitDepth, int? Length);

public sealed class SlskdClient(HttpClient http, string? url, string? apiKey)
{
    private const string UserAgent = "buscarr/0.1.0";

    private bool IsConfigured => !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey);

    public async Task<SlskdServerState?> ServerStateAsync()
    {
        if (!IsConfigured)
        {
            return null;
        }

        var application = await GetAsync<ApplicationState>("application");

        return application.Server;
    }

    public async Task<Guid?> StartSearchAsync(string searchText)
    {
        var id = Guid.NewGuid();
        using var request = Request(HttpMethod.Post, "searches");
        request.Content = JsonContent.Create(new { id, searchText });
        using var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return id;
    }

    public Task<SlskdSearch> SearchAsync(Guid id) =>
        GetAsync<SlskdSearch>($"searches/{id}");

    public Task<List<SlskdResponse>> SearchResponsesAsync(Guid id) =>
        GetAsync<List<SlskdResponse>>($"searches/{id}/responses");

    private async Task<T> GetAsync<T>(string path)
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new JsonException($"resposta vazia do slskd em {path}");
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("slskd sem endereço ou chave de API");
        }

        var request = new HttpRequestMessage(method, $"{url!.TrimEnd('/')}/api/v0/{path}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);

        return request;
    }

    private sealed class ApplicationState
    {
        public required SlskdServerState Server { get; init; }
    }
}
